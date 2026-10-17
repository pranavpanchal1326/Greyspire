# Lays out the A01/A02 evidence written by Assets/Editor/AnimationTest.cs (Logs/anim/):
#   a01_a02_before_after.png  each scenario with the old behaviour (left) and the new (right)
#   gait_set.png              every gait of the directional set as the hero plays it, two phases each
#
#   python Tools/anim_sheet.py [Logs/anim]
import os
import sys

from PIL import Image, ImageDraw

root = sys.argv[1] if len(sys.argv) > 1 else os.path.join('Logs', 'anim')
w = 300


def tile(name, label, colour):
    im = Image.open(os.path.join(root, name + '.png')).convert('RGB').resize((w, w))
    ImageDraw.Draw(im).text((6, 4), label, fill=colour)
    return im


def sheet(rows, out):
    cols = max(len(r) for r in rows)
    canvas = Image.new('RGB', (cols * (w + 6), len(rows) * (w + 6)), (24, 24, 28))
    for y, row in enumerate(rows):
        for x, im in enumerate(row):
            canvas.paste(im, (x * (w + 6), y * (w + 6)))
    canvas.save(os.path.join(root, out))
    print(out, canvas.size)


pairs = ['strafe-right', 'strafe-left', 'guard-shuffle', 'backpedal', 'slope', 'step', 'grip']
sheet([[tile(p + '-before', p.upper() + '  BEFORE', (255, 200, 120)), tile(p + '-after', p.upper() + '  AFTER', (140, 255, 160))]
       for p in pairs if os.path.exists(os.path.join(root, p + '-after.png'))], 'a01_a02_before_after.png')

gaits = ['Walk_Loop', 'Gait_Walk_Bwd', 'Gait_Walk_Right', 'Gait_Walk_Left', 'Gait_Turn_Step', 'Jog_Fwd_Loop', 'Gait_Jog_Bwd']
sheet([[tile('sheet-' + g + '-a', g, (230, 230, 240)), tile('sheet-' + g + '-b', g + '  later', (230, 230, 240))]
       for g in gaits if os.path.exists(os.path.join(root, 'sheet-' + g + '-a.png'))], 'gait_set.png')
