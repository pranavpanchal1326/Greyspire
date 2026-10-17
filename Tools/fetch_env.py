# Downloads the CC0 KayKit environment models the arenas use into
# Assets/Resources/Env. Safe to re-run; existing files are skipped.
import json
import os
import urllib.parse
import urllib.request

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Env")

PACKS = {
    "KayKit-Medieval-Hexagon-Pack-1.0": {
        "models": ["tree_single_A", "tree_single_B", "trees_A_large", "trees_B_large",
                   "rock_single_A", "rock_single_B", "rock_single_C", "rock_single_D", "rock_single_E",
                   "hills_A_trees", "hills_B_trees", "mountain_A_grass_trees", "mountain_B", "mountain_C",
                   "barrel", "crate_A_big", "tent", "flag_red",
                   # Brushwick (village -> town -> city)
                   "building_home_A_blue", "building_home_B_blue", "building_well_blue", "building_blacksmith_blue",
                   "building_market_blue", "building_tavern_blue", "building_windmill_blue", "building_lumbermill_blue",
                   "building_church_blue", "building_castle_blue", "building_barracks_blue", "building_watermill_blue",
                   "building_tower_A_blue", "building_tower_B_blue",
                   "sack", "wheelbarrow", "weaponrack", "bucket_water"],
        "textures": ["hexagons_medieval.png"],
    },
    "KayKit-Dungeon-Remastered-1.0": {
        "models": ["pillar", "pillar_decorated", "column", "rubble_large", "barrel_large", "torch_lit",
                   "banner_patternA_red", "chest_gold"],
        "textures": ["dungeon_texture.png"],
    },
    "KayKit-Halloween-Bits-1.0": {
        "models": ["tree_dead_large", "tree_dead_medium", "gravestone", "grave_A", "lantern_standing",
                   "post_skull", "pumpkin_orange_jackolantern", "fence_broken", "coffin"],
        "textures": ["halloweenbits_texture.png"],
    },
}


def fetch(repo, path, dest):
    if os.path.exists(dest) and os.path.getsize(dest) > 0:
        return
    url = "https://raw.githubusercontent.com/KayKit-Game-Assets/%s/main/%s" % (repo, urllib.parse.quote(path))
    urllib.request.urlretrieve(url, dest)
    print("got", os.path.basename(dest), os.path.getsize(dest))


os.makedirs(OUT, exist_ok=True)
for repo, want in PACKS.items():
    api = "https://api.github.com/repos/KayKit-Game-Assets/%s/git/trees/main?recursive=1" % repo
    tree = [x["path"] for x in json.load(urllib.request.urlopen(api))["tree"]]
    for name in want["models"]:
        hits = [p for p in tree if p.endswith("/" + name + ".fbx")]
        # Prefer the Unity-ready export when a pack ships one.
        hits.sort(key=lambda p: 0 if "fbx(unity)" in p else 1)
        if hits:
            fetch(repo, hits[0], os.path.join(OUT, name + ".fbx"))
        else:
            print("MISSING", repo, name)
    for tex in want["textures"]:
        hits = [p for p in tree if p.endswith("/" + tex)]
        if hits:
            fetch(repo, hits[0], os.path.join(OUT, tex))
        else:
            print("MISSING", repo, tex)
print("done")
