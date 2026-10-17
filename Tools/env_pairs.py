# Places paired environment captures side by side (V01 evidence): the same camera before and
# after a visual pass, written by Assets/Editor/EnvironmentShowcase.cs to Logs/env/<label>/.
#
#   python Tools/env_pairs.py <before dir> <after dir> <out.png> <width> <shot,shot,...>
import os
import sys

from PIL import Image, ImageDraw

base, cand, out, width = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
rows = []
for n in sys.argv[5].split(','):
    a = Image.open(os.path.join(base, n + '.png')).convert('RGB')
    b = Image.open(os.path.join(cand, n + '.png')).convert('RGB')
    a = a.resize((width, round(a.height * width / a.width)))
    b = b.resize((width, round(b.height * width / b.width)))
    ImageDraw.Draw(a).text((6, 4), n + '  BEFORE', fill=(255, 200, 120))
    ImageDraw.Draw(b).text((6, 4), n + '  AFTER', fill=(140, 255, 160))
    rows.append((a, b))
h = rows[0][0].height
sheet = Image.new('RGB', (width * 2 + 8, len(rows) * (h + 6)), (24, 24, 28))
for i, (a, b) in enumerate(rows):
    sheet.paste(a, (0, i * (h + 6)))
    sheet.paste(b, (width + 8, i * (h + 6)))
sheet.save(out)
print(out, sheet.size)
