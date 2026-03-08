#!/usr/bin/env python3
"""Video frame extraction pipeline with optional background removal.

Examples:
  python video_pipeline.py --video "input.mp4" --output "out_frames"
  python video_pipeline.py --video "input.mp4" --output "out_cut" --cutbg
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


def parse_args() -> argparse.Namespace:
    """Parses command-line arguments."""
    parser = argparse.ArgumentParser(description="Extract frames from a video with optional cutout.")
    parser.add_argument("--video", required=True, help="Input video file path")
    parser.add_argument("--output", required=True, help="Output directory path")
    parser.add_argument("--fps", type=float, default=12.0, help="Output frame rate, default 12")
    parser.add_argument("--start", default=None, help="Optional start time, e.g. 00:00:02.5")
    parser.add_argument("--duration", default=None, help="Optional duration, e.g. 00:00:05")
    parser.add_argument("--scale", default=None, help="Optional ffmpeg scale, e.g. 512:-1")
    parser.add_argument("--cutbg", action="store_true", help="If set, remove frame backgrounds via batch-cutout tool")
    return parser.parse_args()


def resolve_ffmpeg_executable() -> str | None:
    """Resolves ffmpeg executable path from PATH or imageio-ffmpeg."""
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg:
        return ffmpeg

    try:
        import imageio_ffmpeg  # type: ignore

        return imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        return None


def ensure_ffmpeg() -> None:
    """Ensures ffmpeg is available in PATH or fallback package."""
    if resolve_ffmpeg_executable() is None:
        raise RuntimeError(
            "ffmpeg not found in PATH and imageio-ffmpeg fallback unavailable. "
            "Install ffmpeg or pip install imageio-ffmpeg."
        )


def locate_cutout_tool() -> Path:
    """Finds batch_cutout.py using a path relative to this script."""
    script_dir = Path(__file__).resolve().parent
    tool_path = script_dir.parent / "batch-cutout" / "batch_cutout.py"
    if not tool_path.exists():
        raise FileNotFoundError(f"Cannot find cutout tool: {tool_path}")
    return tool_path


def run_subprocess(cmd: list[str]) -> None:
    """Runs a command and raises on failure."""
    result = subprocess.run(cmd, check=False)
    if result.returncode != 0:
        raise RuntimeError(f"Command failed ({result.returncode}): {' '.join(cmd)}")


def build_ffmpeg_cmd(video: Path, frame_dir: Path, fps: float, start: str | None, duration: str | None, scale: str | None) -> list[str]:
    """Builds ffmpeg command for frame extraction."""
    ffmpeg = resolve_ffmpeg_executable()
    if ffmpeg is None:
        raise RuntimeError("Unable to resolve ffmpeg executable.")

    vf_parts = [f"fps={fps}"]
    if scale:
        vf_parts.append(f"scale={scale}")
    vf = ",".join(vf_parts)

    cmd = [ffmpeg, "-y"]
    if start:
        cmd.extend(["-ss", start])
    cmd.extend(["-i", str(video)])
    if duration:
        cmd.extend(["-t", duration])
    cmd.extend(["-vf", vf, "-start_number", "1", str(frame_dir / "%04d.png")])
    return cmd


def extract_frames(video: Path, frame_dir: Path, fps: float, start: str | None, duration: str | None, scale: str | None) -> None:
    """Extracts frames from input video to frame directory."""
    frame_dir.mkdir(parents=True, exist_ok=True)
    cmd = build_ffmpeg_cmd(video, frame_dir, fps, start, duration, scale)
    run_subprocess(cmd)


def run_cutbg(input_dir: Path, output_dir: Path) -> None:
    """Runs existing batch-cutout script in cutout mode."""
    cutout_tool = locate_cutout_tool()
    cmd = [
        sys.executable,
        str(cutout_tool),
        "--command",
        "cutout",
        "--input",
        str(input_dir),
        "--output",
        str(output_dir),
    ]
    run_subprocess(cmd)


def main() -> int:
    """Program entry point."""
    args = parse_args()
    ensure_ffmpeg()

    video = Path(args.video).resolve()
    output = Path(args.output).resolve()

    if not video.exists() or not video.is_file():
        print(f"Video file not found: {video}", file=sys.stderr)
        return 2

    output.mkdir(parents=True, exist_ok=True)

    if args.cutbg:
        with tempfile.TemporaryDirectory(prefix="video_frames_") as temp_dir:
            temp_path = Path(temp_dir)
            extract_frames(video, temp_path, args.fps, args.start, args.duration, args.scale)
            run_cutbg(temp_path, output)
    else:
        extract_frames(video, output, args.fps, args.start, args.duration, args.scale)

    print("Done.")
    print(f"Video : {video}")
    print(f"Output: {output}")
    print(f"CutBG : {args.cutbg}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
