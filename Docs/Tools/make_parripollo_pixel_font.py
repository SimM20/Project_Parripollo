# Genera Assets/Fonts/ParripolloPixel.ttf (font pixel-art estilo cartel de ruta) a partir de glyphs en grilla.
# Sin dependencias: escribe las tablas TrueType a mano. Uso: py -I make_parripollo_pixel_font.py <salida.ttf> [preview.png]
# Después de regenerarla: en Unity, reimportar la TTF y regenerar "ParripolloPixel SDF" (64 pt, padding 6, SDFAA, 1024x1024).
import struct, sys, os, zlib, time

P = 128            # unidades por pixel de la font
UPEM = 1024        # 8 px por em -> fontSize 32 = 4 unidades por pixel
ASC_PX, DESC_PX = 10, -3

ACUTE6 = ["...##.", "..##..", "......"]
ACUTE4 = ["..##", ".##.", "...."]
TILDE6 = [".##..#", "#..##.", "......"]
UML6 = ["##..##", "......"]

G = {}  # char -> (top_row_y, rows)
def g(ch, rows, top=6):
    G[ch] = (top, rows)

g('A', [".####.","##..##","##..##","######","##..##","##..##","##..##"])
g('B', ["#####.","##..##","##..##","#####.","##..##","##..##","#####."])
g('C', [".####.","##..##","##....","##....","##....","##..##",".####."])
g('D', ["#####.","##..##","##..##","##..##","##..##","##..##","#####."])
g('E', ["######","##....","##....","#####.","##....","##....","######"])
g('F', ["######","##....","##....","#####.","##....","##....","##...."])
g('G', [".####.","##..##","##....","######","##..##","##..##",".####."])
g('H', ["##..##","##..##","##..##","######","##..##","##..##","##..##"])
g('I', ["####",".##.",".##.",".##.",".##.",".##.","####"])
g('J', ["..####","...##.","...##.","...##.","##.##.","##.##.",".###.."])
g('K', ["##..##","##.##.","####..","###...","####..","##.##.","##..##"])
g('L', ["##....","##....","##....","##....","##....","##....","######"])
g('M', ["##...##","###.###","#######","##.#.##","##...##","##...##","##...##"])
g('N', ["##..##","###.##","######","######","##.###","##..##","##..##"])
g('O', [".####.","##..##","##..##","##..##","##..##","##..##",".####."])
g('P', ["#####.","##..##","##..##","#####.","##....","##....","##...."])
g('Q', [".####.","##..##","##..##","##..##","##.###",".####.","....##"])
g('R', ["#####.","##..##","##..##","#####.","####..","##.##.","##..##"])
g('S', [".#####","##....","##....",".####.","....##","....##","#####."])
g('T', ["######","..##..","..##..","..##..","..##..","..##..","..##.."])
g('U', ["##..##","##..##","##..##","##..##","##..##","##..##",".####."])
g('V', ["##...##","##...##","##...##",".##.##.",".##.##.","..###..","...#..."])
g('W', ["##...##","##...##","##...##","##.#.##","#######","###.###","##...##"])
g('X', ["##..##","##..##",".####.","..##..",".####.","##..##","##..##"])
g('Y', ["##..##","##..##",".####.","..##..","..##..","..##..","..##.."])
g('Z', ["######","....##","...##.","..##..",".##...","##....","######"])

g('0', [".####.","##..##","##.###","###.##","##..##","##..##",".####."])
g('1', ["..##..",".###..","..##..","..##..","..##..","..##..",".####."])
g('2', [".####.","##..##","....##","...##.","..##..",".##...","######"])
g('3', ["#####.","....##","....##","..###.","....##","....##","#####."])
g('4', ["##..##","##..##","##..##","######","....##","....##","....##"])
g('5', ["######","##....","#####.","....##","....##","##..##",".####."])
g('6', [".####.","##....","##....","#####.","##..##","##..##",".####."])
g('7', ["######","....##","....##","...##.","..##..","..##..","..##.."])
g('8', [".####.","##..##","##..##",".####.","##..##","##..##",".####."])
g('9', [".####.","##..##","##..##",".#####","....##","....##",".####."])

g('.', ["##","##"], top=1)
g(',', ["##","##","#."], top=1)
g(':', ["##","##","..","..","##","##"], top=5)
g(';', ["##","##","..","..","##","##","#."], top=5)
g('!', ["##","##","##","##","##","..","##"])
g('¡', ["##","..","##","##","##","##","##"])
g('?', [".####.","##..##","....##","...##.","..##..","......","..##.."])
g('¿', ["..##..","......","..##..",".##...","##....","##..##",".####."])
g('-', ["####"], top=3)
g('+', ["..##..","..##..","######","..##..","..##.."], top=5)
g('/', ["....##","....##","...##.","..##..",".##...","##....","##...."])
g('(', ["..##",".##.","##..","##..","##..",".##.","..##"])
g(')', ["##..",".##.","..##","..##","..##",".##.","##.."])
g("'", ["##","##"])
g('"', ["##.##","##.##"])
g('&', [".###..","##.##.","##.##.",".###..","###.##","##.##.",".##.##"])
g('%', ["##..##","##..##","...##.","..##..",".##...","##..##","##..##"])
g('<', ["...##","..##.",".##..","##...",".##..","..##.","...##"])
g('>', ["##...",".##..","..##.","...##","..##.",".##..","##..."])
g('=', ["######","......","######"], top=4)
g('_', ["######"], top=-1)

for base, acc in (('A', ACUTE6), ('E', ACUTE6), ('O', ACUTE6), ('U', ACUTE6), ('I', ACUTE4)):
    ch = {'A': 'Á', 'E': 'É', 'I': 'Í', 'O': 'Ó', 'U': 'Ú'}[base]
    g(ch, acc + G[base][1], top=9)
g('Ñ', TILDE6 + G['N'][1], top=9)
g('Ü', UML6 + G['U'][1], top=8)

SPACE_ADV = 4

# Minúsculas: mismas formas que las mayúsculas (font solo mayúsculas, como los carteles).
ALIASES = {}
for c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ":
    ALIASES[c.lower()] = c
for lo, up in zip("áéíóúñü", "ÁÉÍÓÚÑÜ"):
    ALIASES[lo] = up

def pixels(top, rows):
    pts = set()
    for i, r in enumerate(rows):
        y = top - i
        for x, c in enumerate(r):
            if c == '#':
                pts.add((x, y))
    return pts

def contours(pts):
    edges = {}
    def add(a, b):
        if (b, a) in edges:
            del edges[(b, a)]
        else:
            edges[(a, b)] = True
    for (x, y) in pts:
        add((x, y), (x, y + 1))
        add((x, y + 1), (x + 1, y + 1))
        add((x + 1, y + 1), (x + 1, y))
        add((x + 1, y), (x, y))
    out = {}
    for (a, b) in edges:
        out.setdefault(a, []).append(b)
    loops = []
    while out:
        start = next(iter(out))
        loop = [start]
        cur = start
        prev_dir = None
        while True:
            nxts = out[cur]
            if len(nxts) == 1 or prev_dir is None:
                nxt = nxts[0]
            else:
                # en un cruce diagonal, doblar a la derecha para no cruzar contornos
                def turn(n):
                    d = (n[0] - cur[0], n[1] - cur[1])
                    return prev_dir[0] * d[1] - prev_dir[1] * d[0]
                nxt = min(nxts, key=turn)
            nxts.remove(nxt)
            if not nxts:
                del out[cur]
            prev_dir = (nxt[0] - cur[0], nxt[1] - cur[1])
            cur = nxt
            if cur == start:
                break
            loop.append(cur)
        # quitar puntos colineales
        simp = []
        n = len(loop)
        for i in range(n):
            a, b, c = loop[i - 1], loop[i], loop[(i + 1) % n]
            if (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0]) != 0:
                simp.append(b)
        loops.append(simp)
    return loops

class Glyph:
    def __init__(self, name, pts, adv_px):
        self.name = name
        self.loops = [[(x * P, y * P) for (x, y) in l] for l in contours(pts)] if pts else []
        self.adv = adv_px * P
        if self.loops:
            xs = [p[0] for l in self.loops for p in l]; ys = [p[1] for l in self.loops for p in l]
            self.bbox = (min(xs), min(ys), max(xs), max(ys))
        else:
            self.bbox = (0, 0, 0, 0)

    def data(self):
        if not self.loops:
            return b''
        xmin, ymin, xmax, ymax = self.bbox
        b = struct.pack('>hhhhh', len(self.loops), xmin, ymin, xmax, ymax)
        end = -1
        for l in self.loops:
            end += len(l)
            b += struct.pack('>H', end)
        b += struct.pack('>H', 0)  # sin instrucciones
        pts = [p for l in self.loops for p in l]
        b += bytes([0x01]) * len(pts)
        px = py = 0
        xs = b''; ys = b''
        for (x, y) in pts:
            xs += struct.pack('>h', x - px); ys += struct.pack('>h', y - py)
            px, py = x, y
        b += xs + ys
        while len(b) % 4:
            b += b'\0'
        return b

glyphs = [Glyph('.notdef', set(), 6), Glyph('space', set(), SPACE_ADV)]
cmap = {0x20: 1, 0xA0: 1}
order = sorted(G.keys())
for ch in order:
    top, rows = G[ch]
    w = max(len(r) for r in rows)
    glyphs.append(Glyph(ch, pixels(top, rows), w + 1))
    cmap[ord(ch)] = len(glyphs) - 1
for lo, up in ALIASES.items():
    cmap[ord(lo)] = cmap[ord(up)]

def checksum(data):
    data += b'\0' * ((4 - len(data) % 4) % 4)
    return sum(struct.unpack('>%dI' % (len(data) // 4), data)) & 0xFFFFFFFF

# glyf / loca
glyf = b''; loca = []
for gl in glyphs:
    loca.append(len(glyf)); glyf += gl.data()
loca.append(len(glyf))
loca_t = b''.join(struct.pack('>I', o) for o in loca)

allx = [gl.bbox for gl in glyphs if gl.loops]
xmin = min(b[0] for b in allx); ymin = min(b[1] for b in allx)
xmax = max(b[2] for b in allx); ymax = max(b[3] for b in allx)
max_pts = max(sum(len(l) for l in gl.loops) for gl in glyphs)
max_cnt = max(len(gl.loops) for gl in glyphs)

now = int(time.time()) + 2082844800
head = struct.pack('>IIIIHHqqhhhhHHhhh', 0x00010000, 0x00010000, 0, 0x5F0F3CF5, 0x000B, UPEM,
                   now, now, xmin, ymin, xmax, ymax, 0, 8, 2, 1, 0)
asc, desc = ASC_PX * P, DESC_PX * P
adv_max = max(gl.adv for gl in glyphs)
hhea = struct.pack('>IhhhHhhhhhhhhhhhH', 0x00010000, asc, desc, 0, adv_max, 0, 0, xmax, 1, 0, 0,
                   0, 0, 0, 0, 0, len(glyphs))
maxp = struct.pack('>IHHHHHHHHHHHHHH', 0x00010000, len(glyphs), max_pts, max_cnt, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0)
hmtx = b''.join(struct.pack('>Hh', gl.adv, gl.bbox[0] if gl.loops else 0) for gl in glyphs)
avg = int(sum(gl.adv for gl in glyphs) / len(glyphs))
codes = sorted(cmap)
os2 = struct.pack('>HhHHHhhhhhhhhhhh', 4, avg, 400, 5, 0,
                  650, 700, 0, 140, 650, 700, 0, 480, 50, 250, 0)
os2 += bytes(10)  # panose
os2 += struct.pack('>IIII', 0x00000003, 0, 0, 0)  # Basic Latin + Latin-1
os2 += b'PRPL'
os2 += struct.pack('>HHHhhhHHII', 0x00C0, codes[0], min(codes[-1], 0xFFFF), asc, desc, 0, asc, -desc, 0x00000001, 0)
os2 += struct.pack('>hhHHH', 7 * P, 7 * P, 0, 0x20, 1)

# cmap formato 4 (un segmento por carácter)
segs = [(c, c, (cmap[c] - c) & 0xFFFF) for c in codes] + [(0xFFFF, 0xFFFF, 1)]
segX2 = len(segs) * 2
import math
search = 2 * (2 ** int(math.floor(math.log2(len(segs)))))
sub = struct.pack('>HHHHHHH', 4, 0, 0, segX2, search, int(math.log2(search // 2)), segX2 - search)
sub += b''.join(struct.pack('>H', s[1]) for s in segs) + b'\0\0'
sub += b''.join(struct.pack('>H', s[0]) for s in segs)
sub += b''.join(struct.pack('>H', s[2]) for s in segs)
sub += b''.join(struct.pack('>H', 0) for s in segs)
sub = sub[:2] + struct.pack('>H', len(sub)) + sub[4:]
# (los dos registros apuntan al mismo subtable)
cmap_t =struct.pack('>HH', 0, 2) + struct.pack('>HHI', 0, 3, 20) + struct.pack('>HHI', 3, 1, 20) + sub

FAMILY = 'Parripollo Pixel'
names = {1: FAMILY, 2: 'Regular', 3: FAMILY + ' Regular 1.0', 4: FAMILY, 5: 'Version 1.0', 6: 'ParripolloPixel-Regular'}
recs = b''; strs = b''
for nid, s in names.items():
    enc = s.encode('utf-16-be')
    recs += struct.pack('>HHHHHH', 3, 1, 0x0409, nid, len(enc), len(strs))
    strs += enc
name_t = struct.pack('>HHH', 0, len(names), 6 + 12 * len(names)) + recs + strs
post = struct.pack('>IIhhIIIII', 0x00030000, 0, -P, P, 1, 0, 0, 0, 0)

tables = {'OS/2': os2, 'cmap': cmap_t, 'glyf': glyf, 'head': head, 'hhea': hhea, 'hmtx': hmtx,
          'loca': loca_t, 'maxp': maxp, 'name': name_t, 'post': post}
tags = sorted(tables)
n = len(tags)
es = 2 ** int(math.floor(math.log2(n)))
font = struct.pack('>IHHHH', 0x00010000, n, es * 16, int(math.log2(es)), n * 16 - es * 16)
offset = 12 + 16 * n
dirs = b''; body = b''
head_off = None
for t in tags:
    d = tables[t]
    if t == 'head':
        head_off = offset + len(body)
    dirs += struct.pack('>4sIII', t.encode('ascii'), checksum(d), offset + len(body), len(d))
    body += d + b'\0' * ((4 - len(d) % 4) % 4)
font += dirs + body
adj = (0xB1B0AFBA - checksum(font)) & 0xFFFFFFFF
font = font[:head_off + 8] + struct.pack('>I', adj) + font[head_off + 12:]

out = sys.argv[1]
with open(out, 'wb') as f:
    f.write(font)
print('glyphs', len(glyphs), 'codes', len(codes), 'bytes', len(font))

# ── Vista previa (PNG sin dependencias) ──
def png(path, w, h, rgb):
    raw = b''.join(b'\0' + bytes(rgb[y * w * 3:(y + 1) * w * 3]) for y in range(h))
    def chunk(t, d):
        c = struct.pack('>I', len(d)) + t + d
        return c + struct.pack('>I', zlib.crc32(t + d) & 0xFFFFFFFF)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
                + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

if len(sys.argv) > 2:
    lines = ["JUGAR OPCIONES SALIR", "RESOLUCIÓN PANTALLA VSYNC", "FPS MÁXIMOS CONTROLES IDIOMA",
             "AYUDAS REINICIAR LISTO", "APLICAR VOLVER SÍ NO SIN LÍMITE", "AUTOMÁTICO TECLADO Y MOUSE",
             "JOYSTICK COMPLETA SIN BORDES", "VENTANA ESPAÑOL ENGLISH", "KEYBOARD & MOUSE PRÓXIMAMENTE",
             "1920 X 1080  144  V1.0 ¿QUÉ? ¡SÍ!", "0123456789 +-/()'\"%<>=_.,:;", "ABCDEFGHIJKLMNOPQRSTUVWXYZ ÜÑÍÚ"]
    S = 3
    lh = 15
    W = 8 + max(sum((len(max(G[ALIASES.get(c, c)][1], key=len)) + 1) if c != ' ' else SPACE_ADV for c in l) for l in lines) + 8
    H = 4 + lh * len(lines)
    pix = [[(40, 110, 70)] * W for _ in range(H)]
    for li, l in enumerate(lines):
        x = 4; base = 4 + li * lh + 10
        for c in l:
            if c == ' ':
                x += SPACE_ADV; continue
            top, rows = G[ALIASES.get(c, c)]
            for (px_, py_) in pixels(top, rows):
                yy = base - py_
                if 0 <= yy < H:
                    pix[yy][x + px_] = (240, 238, 228)
            x += max(len(r) for r in rows) + 1
    rgb = []
    for y in range(H * S):
        row = pix[y // S]
        for xx in range(W * S):
            rgb.extend(row[xx // S])
    png(sys.argv[2], W * S, H * S, rgb)
    print('preview', sys.argv[2])
