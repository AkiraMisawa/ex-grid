# Minimal PNG read/write (8-bit RGB/RGBA, non-interlaced) and crops, for the pictures of this run.
import zlib, struct
def read(path):
    data = open(path, 'rb').read()
    pos = 8; idat = b''; w = h = ct = 0
    while pos < len(data):
        n = struct.unpack('>I', data[pos:pos+4])[0]; t = data[pos+4:pos+8]; c = data[pos+8:pos+8+n]
        if t == b'IHDR': w, h = struct.unpack('>II', c[:8]); ct = c[9]
        elif t == b'IDAT': idat += c
        pos += 12 + n
    ch = 4 if ct == 6 else 3
    raw = zlib.decompress(idat); stride = w * ch
    out = bytearray(h * stride); prev = bytearray(stride)
    for y in range(h):
        f = raw[y*(stride+1)]; line = bytearray(raw[y*(stride+1)+1:(y+1)*(stride+1)])
        for x in range(stride):
            a = line[x-ch] if x >= ch else 0; b = prev[x]; cc = prev[x-ch] if x >= ch else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - cc; pa, pb, pc = abs(p-a), abs(p-b), abs(p-cc)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else cc)) & 255
        out[y*stride:(y+1)*stride] = line; prev = line
    px = [[tuple(out[y*stride+x*ch:y*stride+x*ch+3]) for x in range(w)] for y in range(h)]
    return px
def write(path, px, scale=1):
    h = len(px) * scale; w = len(px[0]) * scale
    raw = bytearray()
    for row in px:
        line = bytearray([0])
        for p in row: line += bytes(p) * scale
        for _ in range(scale): raw += line
    def chunk(t, c): return struct.pack('>I', len(c)) + t + c + struct.pack('>I', zlib.crc32(t + c) & 0xffffffff)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))
def crop(px, x0, y0, x1, y1):
    return [row[max(0, x0):x1] for row in px[max(0, y0):y1]]
