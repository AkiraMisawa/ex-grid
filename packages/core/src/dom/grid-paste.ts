// The grid's half of a paste (ADR-0005, ADR-0014): what the browser's clipboard hands over is measured
// against the paste ceiling here. The rules that approve or refuse it are clipboard-rules.ts's, and the
// parse clipboard-parse.ts's.

/** The default paste ceiling, in UTF-8 bytes across both flavours: 16 MB, as the C# core's (ADR-0005). */
export const defaultPasteByteCap = 16 * 1024 * 1024;

/** Whether `texts` take more than `cap` bytes as UTF-8, as the C# core measures a paste. Each code unit is one
 * to three bytes, so most answers need no count; one that does stops once past the cap. */
export function exceedsUtf8(texts: readonly string[], cap: number): boolean {
    const units = texts.reduce((sum, text) => sum + text.length, 0);
    if (units > cap) return true;
    if (units * 3 <= cap) return false;
    let bytes = 0;
    for (const text of texts) {
        bytes += utf8Length(text, cap - bytes);
        if (bytes > cap) return true;
    }
    return false;
}

/** How many bytes `text` takes as UTF-8, counted without encoding it, and no further than past `limit`: a
 * lone surrogate is written as U+FFFD, three bytes. */
export function utf8Length(text: string, limit = Number.POSITIVE_INFINITY): number {
    let bytes = 0;
    for (let i = 0; i < text.length && bytes <= limit; i++) {
        const unit = text.charCodeAt(i);
        if (unit < 0x80) {
            bytes += 1;
        } else if (unit < 0x8_00) {
            bytes += 2;
        } else if (unit >= 0xd8_00 && unit < 0xdc_00 && i + 1 < text.length && (text.charCodeAt(i + 1) & 0xfc_00) === 0xdc_00) {
            bytes += 4;
            i++;
        } else {
            bytes += 3;
        }
    }
    return bytes;
}
