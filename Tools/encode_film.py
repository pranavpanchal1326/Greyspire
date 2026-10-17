"""Encodes Logs/film/f_*.png into an mp4.  python Tools/encode_film.py <out.mp4> [width]"""
import glob, os, sys
import cv2

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
out = sys.argv[1]
width = int(sys.argv[2]) if len(sys.argv) > 2 else 1600
frames = sorted(glob.glob(os.path.join(root, "Logs", "film", "f_*.png")))
if not frames:
    sys.exit("no frames in Logs/film")
first = cv2.imread(frames[0])
h, w = first.shape[:2]
height = int(round(h * width / w / 2)) * 2
writer = cv2.VideoWriter(out, cv2.VideoWriter_fourcc(*"mp4v"), 30, (width, height))
bad = 0
for f in frames:
    im = cv2.imread(f)
    if im is None:            # a frame still being written when the editor closed
        bad += 1
        continue
    writer.write(cv2.resize(im, (width, height), interpolation=cv2.INTER_AREA))
writer.release()
print(f"{len(frames) - bad} frames, {width}x{height}, {(len(frames) - bad) / 30:.1f} s, "
      f"{os.path.getsize(out) / 1048576:.1f} MB -> {out}")
