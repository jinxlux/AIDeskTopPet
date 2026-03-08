#!/usr/bin/env python3
"""
Batch background removal tool.

Usage:
  python batch_cutout.py --command cutout --input <input_dir> --output <output_dir>
  python batch_cutout.py --command pet-pack --input <input_dir> --output <output_dir>
  python batch_cutout.py --command copy-only --input <input_dir> --output <output_dir>
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Tuple

from PIL import Image

try:
    from rembg import remove, new_session
except Exception:  # pragma: no cover
    remove = None
    new_session = None

SUPPORTED_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}


@dataclass
class ToolConfig:
    """Configuration used by processing pipeline."""

    canvas_size: int = 512
    padding_ratio: float = 0.08
    alpha_threshold: int = 12
    model_name: str = "u2net"


@dataclass
class RunStats:
    """Execution counters and failure records."""

    total: int = 0
    succeeded: int = 0
    failed: int = 0
    failures: List[Tuple[str, str]] = None

    def __post_init__(self) -> None:
        if self.failures is None:
            self.failures = []


def parse_args() -> argparse.Namespace:
    """Parses command line arguments."""
    parser = argparse.ArgumentParser(description="Batch cutout utility")
    parser.add_argument("--command", required=True, choices=["cutout", "pet-pack", "copy-only"], help="Processing mode")
    parser.add_argument("--input", required=True, help="Input root directory")
    parser.add_argument("--output", required=True, help="Output root directory")
    parser.add_argument("--config", required=False, default=None, help="Optional config json path")
    parser.add_argument("--canvas-size", type=int, default=None, help="Override output canvas size")
    parser.add_argument("--padding-ratio", type=float, default=None, help="Override padding ratio")
    parser.add_argument("--alpha-threshold", type=int, default=None, help="Override alpha threshold")
    parser.add_argument("--model", default=None, help="Override rembg model name")
    return parser.parse_args()


def load_config(args: argparse.Namespace) -> ToolConfig:
    """Loads config from file and CLI overrides."""
    config = ToolConfig()

    if args.config:
        config_path = Path(args.config)
        if not config_path.exists():
            raise FileNotFoundError(f"Config file not found: {config_path}")
        payload = json.loads(config_path.read_text(encoding="utf-8"))
        config = ToolConfig(
            canvas_size=int(payload.get("canvas_size", config.canvas_size)),
            padding_ratio=float(payload.get("padding_ratio", config.padding_ratio)),
            alpha_threshold=int(payload.get("alpha_threshold", config.alpha_threshold)),
            model_name=str(payload.get("model_name", config.model_name)),
        )

    if args.canvas_size is not None:
        config.canvas_size = args.canvas_size
    if args.padding_ratio is not None:
        config.padding_ratio = args.padding_ratio
    if args.alpha_threshold is not None:
        config.alpha_threshold = args.alpha_threshold
    if args.model is not None:
        config.model_name = args.model

    if config.canvas_size <= 0:
        raise ValueError("canvas_size must be > 0")
    if not (0 <= config.padding_ratio < 0.5):
        raise ValueError("padding_ratio must be >= 0 and < 0.5")
    if not (0 <= config.alpha_threshold <= 255):
        raise ValueError("alpha_threshold must be in [0,255]")

    return config


def collect_images(root: Path) -> List[Path]:
    """Recursively collects image files."""
    files: List[Path] = []
    for file in root.rglob("*"):
        if file.is_file() and file.suffix.lower() in SUPPORTED_EXTENSIONS:
            files.append(file)
    files.sort()
    return files


def ensure_rembg_available() -> None:
    """Raises with clear message when rembg is unavailable."""
    if remove is None or new_session is None:
        raise RuntimeError(
            "rembg is not available. Please install dependencies from requirements.txt first."
        )


def remove_background(image: Image.Image, session) -> Image.Image:
    """Returns RGBA image with removed background."""
    output = remove(image, session=session)
    if not isinstance(output, Image.Image):
        output = Image.open(output)
    return output.convert("RGBA")


def apply_alpha_threshold(image: Image.Image, threshold: int) -> Image.Image:
    """Drops weak alpha values to reduce edge noise."""
    rgba = image.convert("RGBA")
    data = bytearray(rgba.tobytes())

    for i in range(3, len(data), 4):
        if data[i] < threshold:
            data[i] = 0

    return Image.frombytes("RGBA", rgba.size, bytes(data))


def fit_to_canvas_bottom_center(image: Image.Image, canvas_size: int, padding_ratio: float) -> Image.Image:
    """Fits image to square canvas aligned to bottom-center."""
    bbox = image.getbbox()
    if not bbox:
        return Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))

    cropped = image.crop(bbox)
    available = int(canvas_size * (1.0 - 2 * padding_ratio))
    available = max(1, available)

    scale = min(available / cropped.width, available / cropped.height)
    target_size = (max(1, int(cropped.width * scale)), max(1, int(cropped.height * scale)))
    resized = cropped.resize(target_size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    x = (canvas_size - resized.width) // 2
    y = canvas_size - int(canvas_size * padding_ratio) - resized.height
    y = max(0, y)
    canvas.alpha_composite(resized, dest=(x, y))
    return canvas


def save_png(image: Image.Image, output_path: Path) -> None:
    """Saves image as PNG ensuring parent directories exist."""
    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(output_path, format="PNG")


def safe_relative(path: Path, root: Path) -> Path:
    """Gets relative path from root."""
    return path.relative_to(root)


def frame_sort_key(path: Path) -> Tuple[int, str]:
    """Sort key for frame-like file names."""
    stem = path.stem
    digits = "".join(ch for ch in stem if ch.isdigit())
    if digits:
        return int(digits), stem
    return 10**9, stem


def normalize_to_pet_layout(input_root: Path, relative: Path) -> Path:
    """Maps source relative path into pet output layout.

    Expected inputs include:
      idle/<group>/<frame>.*
      interact/<group>/<frame>.*

    Fallback keeps full relative path under `misc`.
    """
    parts = relative.parts
    if len(parts) >= 3 and parts[0].lower() == "idle":
        return Path("idleRes") / parts[1] / (Path(parts[-1]).stem + ".png")
    if len(parts) >= 3 and parts[0].lower() == "interact":
        return Path("interactRes") / parts[1] / (Path(parts[-1]).stem + ".png")
    return Path("misc") / relative.with_suffix(".png")


def renumber_pet_frames(output_root: Path) -> None:
    """Renumbers frames inside each pet action folder to 01.png, 02.png..."""
    for category in ["idleRes", "interactRes"]:
        category_dir = output_root / category
        if not category_dir.exists():
            continue

        for group_dir in sorted([d for d in category_dir.iterdir() if d.is_dir()]):
            frames = sorted(
                [f for f in group_dir.iterdir() if f.is_file() and f.suffix.lower() == ".png"],
                key=frame_sort_key,
            )
            if not frames:
                continue

            temp_paths = []
            for idx, frame in enumerate(frames, start=1):
                temp = group_dir / f"__tmp_{idx:04d}.png"
                frame.rename(temp)
                temp_paths.append(temp)

            width = max(2, len(str(len(temp_paths))))
            for idx, temp in enumerate(temp_paths, start=1):
                final = group_dir / f"{idx:0{width}d}.png"
                temp.rename(final)


def process_copy_only(files: Iterable[Path], input_root: Path, output_root: Path, stats: RunStats) -> None:
    """Copies files only, preserving relative paths."""
    for source in files:
        stats.total += 1
        try:
            rel = safe_relative(source, input_root)
            target = output_root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            stats.succeeded += 1
        except Exception as exc:  # pylint: disable=broad-except
            stats.failed += 1
            stats.failures.append((str(source), str(exc)))


def process_cutout(files: Iterable[Path], input_root: Path, output_root: Path, config: ToolConfig, stats: RunStats) -> None:
    """Removes background and preserves relative paths."""
    ensure_rembg_available()
    session = new_session(config.model_name)

    for source in files:
        stats.total += 1
        try:
            rel = safe_relative(source, input_root)
            target = output_root / rel.with_suffix(".png")

            with Image.open(source) as img:
                rgba = img.convert("RGBA")
                cut = remove_background(rgba, session)
                cut = apply_alpha_threshold(cut, config.alpha_threshold)

            save_png(cut, target)
            stats.succeeded += 1
        except Exception as exc:  # pylint: disable=broad-except
            stats.failed += 1
            stats.failures.append((str(source), str(exc)))


def process_pet_pack(files: Iterable[Path], input_root: Path, output_root: Path, config: ToolConfig, stats: RunStats) -> None:
    """Removes background and exports into pet animation folder layout."""
    ensure_rembg_available()
    session = new_session(config.model_name)

    for source in files:
        stats.total += 1
        try:
            rel = safe_relative(source, input_root)
            mapped = normalize_to_pet_layout(input_root, rel)
            target = output_root / mapped.with_suffix(".png")

            with Image.open(source) as img:
                rgba = img.convert("RGBA")
                cut = remove_background(rgba, session)
                cut = apply_alpha_threshold(cut, config.alpha_threshold)
                packed = fit_to_canvas_bottom_center(cut, config.canvas_size, config.padding_ratio)

            save_png(packed, target)
            stats.succeeded += 1
        except Exception as exc:  # pylint: disable=broad-except
            stats.failed += 1
            stats.failures.append((str(source), str(exc)))

    renumber_pet_frames(output_root)


def print_summary(stats: RunStats, output_root: Path) -> None:
    """Prints execution summary and writes error log when needed."""
    print("\n=== Batch Cutout Summary ===")
    print(f"Total files : {stats.total}")
    print(f"Succeeded   : {stats.succeeded}")
    print(f"Failed      : {stats.failed}")
    print(f"Output dir  : {output_root}")

    if stats.failed > 0:
        log_path = output_root / "failed_files.log"
        log_path.parent.mkdir(parents=True, exist_ok=True)
        with log_path.open("w", encoding="utf-8") as fp:
            for file_path, reason in stats.failures:
                fp.write(f"{file_path}\t{reason}\n")
        print(f"Failure log : {log_path}")


def main() -> int:
    """Program entry point."""
    args = parse_args()
    config = load_config(args)

    input_root = Path(args.input).resolve()
    output_root = Path(args.output).resolve()

    if not input_root.exists() or not input_root.is_dir():
        print(f"Input directory not found: {input_root}", file=sys.stderr)
        return 2

    output_root.mkdir(parents=True, exist_ok=True)
    files = collect_images(input_root)
    if not files:
        print("No image files found under input directory.")
        return 0

    stats = RunStats()

    if args.command == "copy-only":
        process_copy_only(files, input_root, output_root, stats)
    elif args.command == "cutout":
        process_cutout(files, input_root, output_root, config, stats)
    elif args.command == "pet-pack":
        process_pet_pack(files, input_root, output_root, config, stats)
    else:  # pragma: no cover
        print(f"Unsupported command: {args.command}", file=sys.stderr)
        return 2

    print_summary(stats, output_root)
    return 0 if stats.failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
