// The CRC-32 a zip entry carries (ISO 3309, as APPNOTE 4.4.7 names it), computed a piece at a time.

const table = ((): Uint32Array => {
    const t = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
        let c = n;
        for (let k = 0; k < 8; k++) c = (c & 1) !== 0 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
        t[n] = c >>> 0;
    }
    return t;
})();

/** The CRC-32 of what came before, `crc` (0 for nothing), followed by `bytes`. */
export function crc32(crc: number, bytes: Uint8Array): number {
    let c = ~crc >>> 0;
    // Indexed in place: a checked read per byte made the CRC 14 times slower, 27 against 379 MB/s on Node 26 (2026-10-27),
    // and neither read can miss: i stays below the length, and a byte masked to 0xFF stays below the table's 256.
    for (let i = 0; i < bytes.length; i++) {
        c = (table[(c ^ (bytes[i] as number)) & 0xFF] as number) ^ (c >>> 8); // oxlint-disable-line typescript/no-unsafe-type-assertion -- both indexes are in range, as the comment above says
    }
    return ~c >>> 0;
}
