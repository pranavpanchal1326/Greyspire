# Copies the Imp and Puglin from Quaternius' "Bestiary - Dungeon Monsters Kit" into
# Assets/Resources/Humans/Monsters. The kit's licence allows it in a game but not the files
# themselves in a repository, so they are fetched, not committed.
#
#   python Tools/fetch_packs.py itch            (downloads and unpacks the kit into Downloads/)
#   python Tools/install_monsters.py            (copies the 14 files the game uses)
#   python Tools/install_monsters.py <folder>   (or point it at a kit you unpacked yourself)
#
# Run it before the first time Unity opens the project: the committed .meta files hold the
# import settings, and Unity deletes a .meta whose asset is missing.
import os
import shutil
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
DEST = os.path.join(ROOT, "Assets", "Resources", "Humans", "Monsters")
WANTED = ["Imp.fbx", "Puglin.fbx"] + [
    "T_%s_%s.png" % (m, kind)
    for m in ("Imp", "Puglin")
    for kind in ("BaseColor_1", "BaseColor_2", "BaseColor_3", "Emissive", "Normal", "ORM")
]


def find(base):
    """Maps each wanted file name to its path under base, preferring the Unity FBX export."""
    found = {}
    for folder, _, files in os.walk(base):
        unity = "FBX (Unity)" in folder
        for name in files:
            if name in WANTED and "Unreal" not in folder and (name not in found or unity):
                found[name] = os.path.join(folder, name)
    return found


base = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Downloads", "quaternius")
if not os.path.isdir(base):
    sys.exit("No kit at %s. Run python Tools/fetch_packs.py itch first, or pass the unpacked folder." % base)
found = find(base)
missing = [n for n in WANTED if n not in found]
if missing:
    sys.exit("Not found under %s: %s" % (base, ", ".join(missing)))
os.makedirs(DEST, exist_ok=True)
for name in WANTED:
    shutil.copy2(found[name], os.path.join(DEST, name))
    print("copied", name)
print("done: %d files in %s" % (len(WANTED), DEST))
