"""Extract individual illustrations from light-background plant sheets."""
from pathlib import Path
import sys

import numpy as np
from PIL import Image, ImageFilter


def foreground_mask(image: Image.Image) -> np.ndarray:
    rgb = np.asarray(image.convert("RGB")).astype(np.float32) / 255.0
    maximum, minimum = rgb.max(axis=2), rgb.min(axis=2)
    saturation = maximum - minimum
    # The later sheets have pale green panels, so use a stricter colour key to
    # keep those panels out of the foreground mask.
    return (saturation > 0.16) | (maximum < 0.60)


def components(mask: np.ndarray):
    grouped = np.asarray(
        Image.fromarray((mask * 255).astype("uint8")).filter(ImageFilter.MaxFilter(21))
    ) > 0
    height, width = grouped.shape
    seen = np.zeros_like(grouped)
    boxes = []
    for y in range(height):
        for x in range(width):
            if not grouped[y, x] or seen[y, x]:
                continue
            stack = [(y, x)]
            seen[y, x] = True
            xs, ys = [], []
            while stack:
                yy, xx = stack.pop()
                xs.append(xx); ys.append(yy)
                for ny, nx in ((yy - 1, xx), (yy + 1, xx), (yy, xx - 1), (yy, xx + 1)):
                    if 0 <= ny < height and 0 <= nx < width and grouped[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if len(xs) >= 1000:
                boxes.append((min(xs), min(ys), max(xs) + 1, max(ys) + 1))
    return boxes


def split_sheet(source: Path, destination: Path, sheet_index: int):
    image = Image.open(source).convert("RGBA")
    alpha = foreground_mask(image)
    boxes = sorted(components(alpha), key=lambda b: (b[1], b[0]))
    written = 0
    for x0, y0, x1, y1 in boxes:
        margin = 16
        x0, y0 = max(0, x0 - margin), max(0, y0 - margin)
        x1, y1 = min(image.width, x1 + margin), min(image.height, y1 + margin)
        crop = np.asarray(image.crop((x0, y0, x1, y1))).copy()
        crop[:, :, 3] = (alpha[y0:y1, x0:x1].astype(np.uint8) * 255)
        if int((crop[:, :, 3] > 20).sum()) < 500:
            continue
        written += 1
        Image.fromarray(crop, "RGBA").save(destination / f"flora_sheet{sheet_index:02d}_{written:03d}.png")
    return written


def main():
    if len(sys.argv) < 3:
        raise SystemExit("usage: split_plant_sheets.py DESTINATION SOURCE...")
    destination = Path(sys.argv[1])
    destination.mkdir(parents=True, exist_ok=True)
    total = 0
    for index, source_name in enumerate(sys.argv[2:], 1):
        count = split_sheet(Path(source_name), destination, index)
        print(f"{Path(source_name).name}: {count} sprites")
        total += count
    print(f"wrote {total} plant sprites to {destination}")


if __name__ == "__main__":
    main()
