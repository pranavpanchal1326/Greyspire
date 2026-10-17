# Tiles screenshots from Logs/smoke into labelled contact sheets, so a whole
# play-through can be reviewed in a few pictures.
#
#   python Tools/contact_sheet.py <out.png> <columns> <cell width> <name> [<name> ...]
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SRC = os.path.join(ROOT, "Logs", "smoke")

out, cols, width = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
names = sys.argv[4:]
cells = []
for name in names:
    path = os.path.join(SRC, name + ".png")
    if not os.path.exists(path):
        print("missing", name)
        continue
    im = Image.open(path).convert("RGB")
    im = im.resize((width, round(im.height * width / im.width)))
    ImageDraw.Draw(im).text((6, 4), name, fill=(255, 255, 0))
    cells.append(im)

rows = (len(cells) + cols - 1) // cols
height = max(c.height for c in cells)
sheet = Image.new("RGB", (cols * width, rows * height), (40, 40, 40))
for i, c in enumerate(cells):
    sheet.paste(c, ((i % cols) * width, (i // cols) * height))
os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
sheet.save(out)
print("wrote", out, sheet.size)
