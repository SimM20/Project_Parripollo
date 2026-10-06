"""Reescalado de pixel art sin colores nuevos.
Cada pixel de salida cubre un rectangulo (fraccional) de la fuente. Se suma el area de cada color
y se elige UNO de los colores que ya estan en ese rectangulo (nunca una mezcla):
 - si el area opaca no llega a `alpha_min`, queda transparente;
 - cada color suma el area de los colores parecidos (luces y sombras del mismo tono cuentan juntas,
   asi un relleno sombreado no pierde contra el contorno);
 - los colores que contrastan con el bloque (contorno, ojos, brillos) tienen un bonus chico;
 - el contorno casi negro gana si ocupa al menos un 35 % del bloque (no se cortan las lineas).
Se prueban varios desfasajes de la grilla y se queda el que mas respeta los bloques (pureza).
Modo 'hd' (arte que no es pixel art): promedio de area (BOX) y alfa binario."""
import math
from PIL import Image

SIM = 150.0
OUTLINE_SHARE = 0.35

def lum(c):
    return 0.299*c[0] + 0.587*c[1] + 0.114*c[2]

def _render(px, W, H, tw, th, rx, ry, offx, offy, alpha_min, boost, xe=None, ye=None):
    out = [[None]*tw for _ in range(th)]; purity = 0.0; optot = 0.0
    for oy in range(th):
        y0, y1 = (ye[oy], ye[oy+1]) if ye else (oy*ry - offy, (oy+1)*ry - offy)
        for ox in range(tw):
            x0, x1 = (xe[ox], xe[ox+1]) if xe else (ox*rx - offx, (ox+1)*rx - offx)
            weights = {}; opaque = 0.0
            for sy in range(max(0, int(math.floor(y0))), min(H, math.ceil(y1))):
                wy = min(y1, sy+1) - max(y0, sy)
                if wy <= 0: continue
                for sx in range(max(0, int(math.floor(x0))), min(W, math.ceil(x1))):
                    wx = min(x1, sx+1) - max(x0, sx)
                    if wx <= 0: continue
                    c = px[sx, sy]
                    if c[3] < 128: continue
                    c = (c[0], c[1], c[2], 255); w = wx*wy
                    weights[c] = weights.get(c, 0.0) + w; opaque += w
            if not weights or opaque / ((x1-x0)*(y1-y0)) < alpha_min:
                continue
            mean = sum(lum(c)*w for c, w in weights.items()) / opaque
            items = list(weights.items())
            best = None; bs = -1.0
            for c, w in items:
                grp = 0.0
                for c2, w2 in items:
                    d = math.sqrt((c[0]-c2[0])**2 + (c[1]-c2[1])**2 + (c[2]-c2[2])**2)
                    if d < SIM: grp += w2 * (1.0 - d/SIM)
                score = (grp/opaque) * (1.0 + boost*abs(lum(c)-mean)/255.0)
                if score > bs: bs = score; best = c
            # contorno: un color casi negro que ocupa >= OUTLINE_SHARE del bloque se queda con el pixel
            dark = [(w, c) for c, w in items if lum(c) < 40]
            if dark:
                w, c = max(dark)
                if w / opaque >= OUTLINE_SHARE: best = c
            out[oy][ox] = best; purity += max(weights.values()); optot += opaque
    return out, (purity/optot if optot else 0.0)

def pixel_downscale(src, tw, th, alpha_min=0.4, contrast_boost=0.5, search=True, steps=4):
    src = src.convert('RGBA'); W, H = src.size; px = src.load()
    rx, ry = W/tw, H/th
    cands = [(0.0, 0.0)]
    bbox = src.getchannel('A').point(lambda a: 255 if a >= 128 else 0).getbbox()
    if search and bbox is not None:
        mx, my = math.ceil(rx/2), math.ceil(ry/2)
        # se puede correr la grilla en un eje si el dibujo no toca los dos bordes de ese eje
        # (un marco o un boton que llena el lienzo perderia el contorno; un cliente cortado abajo no)
        free_x = not (bbox[0] < mx and bbox[2] > W-mx)
        free_y = not (bbox[1] < my and bbox[3] > H-my)
        n = max(1, min(steps, int(round(max(rx, ry)))))
        sx = [i*rx/n - rx/2 + rx/(2*n) for i in range(n)] if free_x else [0.0]
        sy = [j*ry/n - ry/2 + ry/(2*n) for j in range(n)] if free_y else [0.0]
        cands += [(a, b) for a in sx for b in sy]
    best = None
    for offx, offy in cands:
        out, pur = _render(px, W, H, tw, th, rx, ry, offx, offy, alpha_min, contrast_boost)
        if best is None or pur > best[1] + 1e-9: best = (out, pur, offx, offy)
    img = Image.new('RGBA', (tw, th), (0, 0, 0, 0)); po = img.load()
    for y in range(th):
        for x in range(tw):
            if best[0][y][x] is not None: po[x, y] = best[0][y][x]
    img.info['offset'] = (best[2], best[3]); img.info['purity'] = best[1]
    return img

def _edges(parts):
    """[(largo en la fuente, pixeles de salida), ...] -> bordes de cada pixel de salida en la fuente."""
    e = [0.0]; x = 0.0
    for length, n in parts:
        for i in range(n):
            e.append(x + length*(i+1)/n)
        x += length
    return e

def pixel_downscale_parts(src, xparts, yparts, alpha_min=0.4, contrast_boost=0.5):
    """Como pixel_downscale, pero cada tramo de la fuente (marco, segmento, separador) se reduce a
    una cantidad entera de pixeles: los bordes de las partes caen justo en el borde de un pixel."""
    src = src.convert('RGBA'); W, H = src.size; px = src.load()
    xe, ye = _edges(xparts), _edges(yparts)
    assert abs(xe[-1]-W) < 1e-6 and abs(ye[-1]-H) < 1e-6, (xe[-1], W, ye[-1], H)
    tw, th = len(xe)-1, len(ye)-1
    out, pur = _render(px, W, H, tw, th, 0, 0, 0, 0, alpha_min, contrast_boost, xe, ye)
    img = Image.new('RGBA', (tw, th), (0, 0, 0, 0)); po = img.load()
    for y in range(th):
        for x in range(tw):
            if out[y][x] is not None: po[x, y] = out[y][x]
    return img

def hd_downscale(src, tw, th, alpha_cut=128):
    src = src.convert('RGBA')
    r = src.resize((tw, th), Image.BOX)
    px = r.load()
    for y in range(th):
        for x in range(tw):
            c = px[x, y]
            px[x, y] = (c[0], c[1], c[2], 255) if c[3] >= alpha_cut else (0, 0, 0, 0)
    return r
