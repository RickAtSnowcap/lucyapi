# Image test fixtures

Used by `tools/ownership_e2e.py` (image upload checks, migration 010).

| File | What | Source |
|---|---|---|
| `sample.png` | 320x200 RGBA PNG | generated with SkiaSharp |
| `sample.jpg` | 320x200 JPEG (flattened onto white) | generated with SkiaSharp |
| `lossy.webp` | 320x200 lossy WebP | generated with SkiaSharp |
| `lossless-alpha.webp` | 320x200 lossless WebP with alpha | generated with SkiaSharp |
| `animated.webp` | 300x225 animated WebP, 100 frames | Google's WebP sample, https://www.gstatic.com/webp/animated/1.webp |

Animated GIFs, truncated/garbage files and the over-pixel-cap PNG are built in the test script itself.
