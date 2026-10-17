# Downloads the free Quaternius packs the game uses into /Downloads and unpacks them.
# Safe to re-run; files that are already there are skipped.
#
#   python Tools/fetch_packs.py            (everything)
#   python Tools/fetch_packs.py itch       (only the itch.io packs)
import http.cookiejar
import json
import os
import re
import sys
import urllib.parse
import urllib.request
import zipfile

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(ROOT, "Downloads", "quaternius")
UA = {"User-Agent": "Mozilla/5.0"}

DIRECT = {
    "universal_animation_librarystandard.zip": "https://opengameart.org/sites/default/files/universal_animation_librarystandard.zip",
    "universal_animation_library_2standard.zip": "https://opengameart.org/sites/default/files/universal_animation_library_2standard.zip",
    "stylized_nature_megakitstandard.zip": "https://opengameart.org/sites/default/files/stylized_nature_megakitstandard.zip",
    "medieval_village_megakitstandard.zip": "https://opengameart.org/sites/default/files/medieval_village_megakitstandard.zip",
    "fantasy_props_megakitstandard.zip": "https://opengameart.org/sites/default/files/fantasy_props_megakitstandard.zip",
}

# itch.io project slug -> the free file to take from it
ITCH = {
    "universal-base-characters": "Universal Base Characters[Standard].zip",
    "modular-character-outfits-fantasy": "Modular Character Outfits - Fantasy[Standard].zip",
    "bestiary-dungeon-monsters-kit": "Bestiary - Dungeon Monsters Kit[Standard].zip",
}


def save(opener, url, dest):
    """Downloads url to dest, resuming after dropped connections until the size matches."""
    if os.path.exists(dest) and os.path.getsize(dest) > 0:
        print("have", os.path.basename(dest))
        return
    tmp = dest + ".part"
    total = None
    for attempt in range(12):
        done = os.path.getsize(tmp) if os.path.exists(tmp) else 0
        if total is not None and done >= total:
            break
        headers = dict(UA)
        if done:
            headers["Range"] = "bytes=%d-" % done
        try:
            with opener.open(urllib.request.Request(url, headers=headers), timeout=60) as r:
                if done and r.status != 206:
                    done = 0            # server ignored the range: start again
                length = r.headers.get("Content-Length")
                if total is None and length is not None:
                    total = done + int(length)
                with open(tmp, "ab" if done else "wb") as f:
                    while True:
                        chunk = r.read(1 << 20)
                        if not chunk:
                            break
                        f.write(chunk)
        except Exception as e:
            print("  retrying after:", e)
    size = os.path.getsize(tmp)
    if total is not None and size != total:
        raise SystemExit("incomplete download: %s (%d of %d bytes)" % (os.path.basename(dest), size, total))
    os.replace(tmp, dest)
    print("got", os.path.basename(dest), round(size / 1e6, 1), "MB")


def itch(slug, wanted, dest):
    """Walks itch.io's free-download flow: page -> download key -> file list -> file URL."""
    if os.path.exists(dest) and os.path.getsize(dest) > 0:
        print("have", os.path.basename(dest))
        return
    jar = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    base = "https://quaternius.itch.io/" + slug

    def get(url):
        return opener.open(urllib.request.Request(url, headers=UA)).read().decode("utf-8", "ignore")

    def post(url, csrf):
        data = urllib.parse.urlencode({"csrf_token": csrf}).encode()
        return json.loads(opener.open(urllib.request.Request(url, data=data, headers=UA)).read())

    csrf = re.search(r'name="csrf_token" value="([^"]*)"', get(base)).group(1)
    listing_url = post(base + "/download_url", csrf)["url"]
    listing = get(listing_url)
    upload = None
    for m in re.finditer(r'data-upload_id="(\d+)"', listing):
        name = re.search(r'title="([^"]*)"', listing[m.start():m.start() + 900])
        if name and name.group(1) == wanted:
            upload = m.group(1)
    if upload is None:
        raise SystemExit("file not offered any more: " + wanted)
    url = post("%s/file/%s?source=game_download" % (base, upload), csrf)["url"]
    save(opener, url, dest)


os.makedirs(OUT, exist_ok=True)
plain = urllib.request.build_opener()
only = sys.argv[1] if len(sys.argv) > 1 else ""     # "itch" or "direct" to fetch just that half
if only != "itch":
    for name, url in DIRECT.items():
        save(plain, url, os.path.join(OUT, name))
if only != "direct":
    for slug, name in ITCH.items():
        itch(slug, name, os.path.join(OUT, name))

for name in sorted(os.listdir(OUT)):
    if not name.endswith(".zip"):
        continue
    target = os.path.join(OUT, name[:-4])
    if os.path.isdir(target):
        continue
    with zipfile.ZipFile(os.path.join(OUT, name)) as z:
        z.extractall(target)
    print("unpacked", name)
print("done")
