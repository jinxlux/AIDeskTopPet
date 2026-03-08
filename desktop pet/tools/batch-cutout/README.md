# batch-cutout

A CLI tool for batch image background removal.

## Commands

- `copy-only`: copy images only, keep relative paths.
- `cutout`: remove background, keep relative paths, output png.
- `pet-pack`: remove background + fit to square canvas + export to desktop-pet layout.

## Required Arguments

- `--command` : `copy-only | cutout | pet-pack`
- `--input` : input root directory containing all source images
- `--output` : output root directory

## Optional Arguments

- `--config` : config file path (json)
- `--canvas-size` : override canvas size (default 512)
- `--padding-ratio` : override padding ratio (default 0.08)
- `--alpha-threshold` : override alpha cleanup threshold (default 12)
- `--model` : override rembg model name (default u2net)

## Install

```powershell
cd "D:\ai项目\desktop pet\tools\batch-cutout"
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## Run Examples

```powershell
python .\batch_cutout.py --command copy-only --input "D:\raw" --output "D:\out"
python .\batch_cutout.py --command cutout --input "D:\raw" --output "D:\out"
python .\batch_cutout.py --command pet-pack --input "D:\raw" --output "D:\out"
python .\batch_cutout.py --command pet-pack --input "D:\raw" --output "D:\out" --config .\config.example.json
```

## Input Structure for `pet-pack` (recommended)

```text
<input>
  idle
    idle1
      01.png
      02.png
  interact
    interact1
      01.png
      02.png
```

## Output Structure for `pet-pack`

```text
<output>
  idleRes
    idle1
      01.png
      02.png
  interactRes
    interact1
      01.png
      02.png
```

Any source path not matching `idle/<group>/...` or `interact/<group>/...` goes to `misc/`.
