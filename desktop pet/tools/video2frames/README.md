# video2frames

Extract frames from a video, with optional background removal.

## Arguments

- `--video` (required): input video path
- `--output` (required): output directory
- `--fps` (optional, default `12`)
- `--start` (optional): e.g. `00:00:02.5`
- `--duration` (optional): e.g. `00:00:05`
- `--scale` (optional): e.g. `512:-1`
- `--cutbg` (optional flag): if provided, run cutout after extraction

## Notes

- This script does not use absolute disk paths.
- If `--cutbg` is enabled, it locates cutout script by relative path:
  `../batch-cutout/batch_cutout.py`
- Requires `ffmpeg` in PATH.

## Examples

```powershell
cd "...\tools\video2frames"
python .\video_pipeline.py --video "C:\work\input.mp4" --output "C:\work\frames"
python .\video_pipeline.py --video "C:\work\input.mp4" --output "C:\work\frames_cut" --cutbg
python .\video_pipeline.py --video "C:\work\input.mp4" --output "C:\work\frames_cut" --cutbg --fps 15 --scale 512:-1
```
