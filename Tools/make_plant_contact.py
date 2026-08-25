from pathlib import Path
import sys
from PIL import Image, ImageDraw

files = sorted(Path(sys.argv[1]).glob('*.png'))
cell_w, cell_h, cols = 180, 170, 8
rows = (len(files) + cols - 1) // cols
out = Image.new('RGB', (cols*cell_w, rows*cell_h), (48, 56, 42))
draw = ImageDraw.Draw(out)
for i, path in enumerate(files):
    im = Image.open(path).convert('RGBA')
    im.thumbnail((cell_w-12, cell_h-30))
    x = (i % cols)*cell_w + (cell_w-im.width)//2
    y = (i // cols)*cell_h + 4
    out.paste(im, (x,y), im)
    draw.text(((i % cols)*cell_w+5, (i // cols)*cell_h+cell_h-22), path.stem, fill='white')
out.save(sys.argv[2])
