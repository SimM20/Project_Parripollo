"""Reescala los sprites de Assets/Sprites a la grilla 480x270 -> Assets/Sprites/Pixel480/.
Uso: python run_batch.py [filtro ...]   (sin filtro rehace todo y escribe manifest.json)
Los .aseprite que Unity usa directo se exportan antes a aseprite_export/ (MCP de Aseprite, saveCopyAs)."""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import downscale
from downscale import pixel_downscale, pixel_downscale_parts, hd_downscale
from PIL import Image
exec(open(os.path.join(os.path.dirname(__file__), "jobs.py")).read())
HERE = os.path.dirname(os.path.abspath(__file__))
SPR = os.path.join(HERE, "..", "..", "Assets", "Sprites").replace("\\", "/") + "/"
EXPD = os.path.join(HERE, "aseprite_export") + "/"
OUT = SPR + "Pixel480/"
manifest = []
only = sys.argv[1:]  # filtro opcional
for j in J:
    if only and not any(o in j["src"] for o in only): continue
    src = j["src"].replace("@EXPORT/", EXPD) if j["src"].startswith("@EXPORT/") else SPR + j["src"]
    rel_out = (j["out"] or j["src"])
    rel_out = os.path.splitext(rel_out)[0] + ".png"
    dst = OUT + rel_out
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    im = Image.open(src).convert("RGBA")
    downscale.OUTLINE_SHARE = j["outline"]
    if j["mode"] == "parts":
        out = pixel_downscale_parts(im, *j["parts"])
        assert out.size == (j["w"], j["h"]), out.size
    elif j["mode"] == "pixel":
        out = pixel_downscale(im, j["w"], j["h"])
    else:
        out = hd_downscale(im, j["w"], j["h"])
    out.save(dst, optimize=True)
    srcs = [] if j["src"].startswith("@EXPORT/") else [j["src"]]
    manifest.append(dict(out="Pixel480/" + rel_out, sources=srcs + j["refs"], ppu=j["ppu"], w=j["w"], h=j["h"],
                         srcW=im.width, srcH=im.height, border=j["border"], mode=j["mode"], note=j["note"]))
    print(f"{j['mode']:5} {im.width}x{im.height} -> {j['w']}x{j['h']}  {rel_out}", flush=True)
if not only: json.dump(manifest, open(os.path.join(os.path.dirname(__file__), "manifest.json"), "w"), indent=1, ensure_ascii=False)
print(len(manifest), "sprites")
