// Minimal PNG decoder (8-bit RGB/RGBA, non-interlaced) — enough to read a CDP screenshot
// without an npm dependency. Spike code.
import zlib from 'node:zlib';

export function decodePng(buf) {
    let p = 8, width = 0, height = 0, colourType = 0;
    const idat = [];
    while (p < buf.length) {
        const len = buf.readUInt32BE(p);
        const type = buf.toString('ascii', p + 4, p + 8);
        const data = buf.subarray(p + 8, p + 8 + len);
        if (type === 'IHDR') {
            width = data.readUInt32BE(0);
            height = data.readUInt32BE(4);
            if (data[8] !== 8 || data[12] !== 0) throw new Error('unsupported PNG');
            colourType = data[9];
        } else if (type === 'IDAT') idat.push(data);
        else if (type === 'IEND') break;
        p += 12 + len;
    }
    const bpp = colourType === 6 ? 4 : colourType === 2 ? 3 : (() => { throw new Error('colour type ' + colourType); })();
    const raw = zlib.inflateSync(Buffer.concat(idat));
    const stride = width * bpp;
    const out = Buffer.alloc(width * height * 4);
    let prev = Buffer.alloc(stride);
    for (let y = 0; y < height; y++) {
        const f = raw[y * (stride + 1)];
        const line = Buffer.from(raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1)));
        for (let x = 0; x < stride; x++) {
            const a = x >= bpp ? line[x - bpp] : 0, b = prev[x], c = x >= bpp ? prev[x - bpp] : 0;
            let v = line[x];
            if (f === 1) v += a;
            else if (f === 2) v += b;
            else if (f === 3) v += (a + b) >> 1;
            else if (f === 4) {
                const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c);
                v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
            }
            line[x] = v & 0xff;
        }
        for (let x = 0; x < width; x++) {
            out[(y * width + x) * 4] = line[x * bpp];
            out[(y * width + x) * 4 + 1] = line[x * bpp + 1];
            out[(y * width + x) * 4 + 2] = line[x * bpp + 2];
            out[(y * width + x) * 4 + 3] = bpp === 4 ? line[x * bpp + 3] : 255;
        }
        prev = line;
    }
    return { width, height, data: out };
}

// Counts strongly red and strongly blue pixels inside a rectangle (CSS px at scale 1),
// and the horizontal extent of the red ones.
export function inkIn(img, rect) {
    let red = 0, blue = 0, redMinX = Infinity, redMaxX = -Infinity;
    const x0 = Math.max(0, Math.floor(rect.x)), x1 = Math.min(img.width, Math.ceil(rect.x + rect.width));
    const y0 = Math.max(0, Math.floor(rect.y)), y1 = Math.min(img.height, Math.ceil(rect.y + rect.height));
    for (let y = y0; y < y1; y++) {
        for (let x = x0; x < x1; x++) {
            const i = (y * img.width + x) * 4;
            const r = img.data[i], g = img.data[i + 1], b = img.data[i + 2];
            if (r > 150 && g < 120 && b < 120) { red++; redMinX = Math.min(redMinX, x); redMaxX = Math.max(redMaxX, x); }
            else if (b > 150 && r < 120 && g < 120) blue++;
        }
    }
    return { red, blue, redMinX, redMaxX };
}
