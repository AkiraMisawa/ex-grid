import zlib from 'node:zlib';

// What the browser put on the screen, read in device pixels: a screenshot at the device's own
// scale, decoded here. A computed style says what the cascade resolved; it cannot say that a
// layer painted above an outline hid one of its pixels, which is what UX-17/18/19 are about.

/** Decodes the PNG a Chromium screenshot writes: 8-bit RGB or RGBA, not interlaced. */
export function decodePng(buffer) {
    let offset = 8;
    let width = 0;
    let height = 0;
    let colourType = 0;
    const data = [];
    while (offset < buffer.length) {
        const length = buffer.readUInt32BE(offset);
        const type = buffer.toString('ascii', offset + 4, offset + 8);
        const chunk = buffer.subarray(offset + 8, offset + 8 + length);
        if (type === 'IHDR') {
            width = chunk.readUInt32BE(0);
            height = chunk.readUInt32BE(4);
            if (chunk[8] !== 8 || chunk[12] !== 0 || (chunk[9] !== 2 && chunk[9] !== 6)) {
                throw new Error('decodePng reads 8-bit, non-interlaced RGB or RGBA only');
            }
            colourType = chunk[9];
        } else if (type === 'IDAT') {
            data.push(chunk);
        }
        offset += 12 + length;
    }
    const channels = colourType === 6 ? 4 : 3;
    const raw = zlib.inflateSync(Buffer.concat(data));
    const stride = width * channels;
    const pixels = Buffer.alloc(height * stride);
    for (let y = 0; y < height; y++) {
        const filter = raw[y * (stride + 1)];
        for (let x = 0; x < stride; x++) {
            const a = x >= channels ? pixels[y * stride + x - channels] : 0;
            const b = y > 0 ? pixels[(y - 1) * stride + x] : 0;
            const c = x >= channels && y > 0 ? pixels[(y - 1) * stride + x - channels] : 0;
            let v = raw[y * (stride + 1) + 1 + x];
            if (filter === 1) v += a;
            else if (filter === 2) v += b;
            else if (filter === 3) v += Math.floor((a + b) / 2);
            else if (filter === 4) {
                const p = a + b - c;
                const pa = Math.abs(p - a);
                const pb = Math.abs(p - b);
                const pc = Math.abs(p - c);
                v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
            }
            pixels[y * stride + x] = v & 0xff;
        }
    }
    return {
        width,
        height,
        at: (x, y) => {
            const i = (y * stride) + (x * channels);
            return [pixels[i], pixels[i + 1], pixels[i + 2]];
        },
    };
}

/**
 * A region of the page as painted, in device pixels. Every coordinate given is a page point in
 * CSS px; `across` and `down` answer the device pixels along a line through the region.
 */
export async function painted(page, { x, y, width, height }) {
    const clip = { x, y, width, height };
    const image = decodePng(await page.screenshot({ clip, scale: 'device', animations: 'disabled', caret: 'hide' }));
    const scale = image.width / width;
    const col = (px) => Math.min(image.width - 1, Math.max(0, Math.floor((px - x) * scale)));
    const row = (py) => Math.min(image.height - 1, Math.max(0, Math.floor((py - y) * scale)));
    return {
        scale,
        at: (px, py) => image.at(col(px), row(py)),
        across: (py, fromX, toX) => {
            const out = [];
            for (let i = col(fromX); i <= col(toX); i++) out.push(image.at(i, row(py)));
            return out;
        },
        down: (px, fromY, toY) => {
            const out = [];
            for (let j = row(fromY); j <= row(toY); j++) out.push(image.at(col(px), j));
            return out;
        },
        lastRow: (px) => image.at(col(px), image.height - 1),
    };
}

/** Whether two painted colours are the same to within `tolerance` on each channel. */
export const sameColour = (a, b, tolerance = 12) => a.every((v, i) => Math.abs(v - b[i]) <= tolerance);

/** The lengths, in device pixels, of the runs of `colour` along a line of pixels. */
export function runsOf(pixels, colour, tolerance = 48) {
    const runs = [];
    let run = 0;
    for (const pixel of pixels) {
        if (sameColour(pixel, colour, tolerance)) {
            run++;
        } else if (run > 0) {
            runs.push(run);
            run = 0;
        }
    }
    if (run > 0) runs.push(run);
    return runs;
}

/** A CSS colour as the page resolves it, in sRGB bytes — whatever syntax the cascade kept it in. */
export function resolvedColour(page, css) {
    return page.evaluate((colour) => {
        const context = new OffscreenCanvas(1, 1).getContext('2d', { willReadFrequently: true });
        context.fillStyle = colour;
        context.fillRect(0, 0, 1, 1);
        return Array.from(context.getImageData(0, 0, 1, 1).data.slice(0, 3));
    }, css);
}

/** WCAG's contrast ratio between two sRGB colours. */
export function contrast(a, b) {
    const luminance = (rgb) => {
        const [r, g, bl] = rgb.map((v) => {
            const c = v / 255;
            return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
        });
        return 0.2126 * r + 0.7152 * g + 0.0722 * bl;
    };
    const la = luminance(a);
    const lb = luminance(b);
    return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
}
