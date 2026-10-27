// The files the shared .xlsx cases read, built as ExGrid.Data.Xlsx's runner builds them (tests/cases/README.md): bytes, an
// OLE compound file, a zip of the parts given, and a workbook of one worksheet laid out as the C# tests' `Fixtures.Book`
// lays it out; a zip's parts stored rather than deflated, one part's deflate stream damaged, or one part's size declared
// smaller than it is.
import { crc32 } from '../src/crc32';
import { type Json, flag, isObject, key, known, list, number, object, optional, string, text } from './case-json';

const utf8 = new TextEncoder();

/** The bytes of a file that is not a zip, as the CSV cases write them: UTF-8 text, `{"hex": …}` or `{"repeat": …, "times": n}`. */
function bytesOf(pieces: readonly Json[]): Uint8Array {
    const parts = pieces.map(piece => {
        if (typeof piece === 'string') return utf8.encode(piece);
        const given = object(piece);
        if (given['hex'] !== undefined) {
            const hex = string(given['hex']).replaceAll(' ', '');
            return Uint8Array.from({ length: hex.length / 2 }, (_, i) => Number.parseInt(hex.slice(i * 2, i * 2 + 2), 16));
        }
        return utf8.encode(string(key(given, 'repeat')).repeat(number(key(given, 'times'))));
    });
    return concat(parts);
}

function concat(parts: readonly Uint8Array[]): Uint8Array {
    const bytes = new Uint8Array(parts.reduce((sum, part) => sum + part.length, 0));
    let at = 0;
    for (const part of parts) {
        bytes.set(part, at);
        at += part.length;
    }
    return bytes;
}

async function deflate(bytes: Uint8Array<ArrayBuffer>): Promise<Uint8Array> {
    const stream = new Blob([bytes]).stream().pipeThrough(new CompressionStream('deflate-raw'));
    return new Uint8Array(await new Response(stream).arrayBuffer());
}

interface ZipOptions {
    readonly stored: boolean;
    /** The part whose deflate stream is damaged: its first block takes the reserved type. */
    readonly damage: string | undefined;
    /** The part whose uncompressed size is declared as another. */
    readonly lie: { readonly part: string; readonly declared: number } | undefined;
}

/** A zip of the parts, each written as UTF-8, deflated unless stored. */
async function zip(parts: readonly (readonly [string, string])[], options: ZipOptions): Promise<Uint8Array> {
    const locals: Uint8Array[] = [];
    const centrals: Uint8Array[] = [];
    let offset = 0;
    let damaged = options.damage === undefined;
    for (const [name, content] of parts) {
        const nameBytes = utf8.encode(name);
        const data = utf8.encode(content);
        const crc = crc32(0, data);
        const body = options.stored ? data : await deflate(data);
        if (name === options.damage) {
            if (options.stored) throw new Error(`The part '${name}' is stored, so it has no deflate stream to damage.`);
            body[0] = 0b111;
            damaged = true;
        }
        const declared = options.lie?.part === name ? options.lie.declared : data.length;
        const local = new Uint8Array(30 + nameBytes.length);
        const lv = new DataView(local.buffer);
        lv.setUint32(0, 0x04034b50, true);
        lv.setUint16(4, 20, true);
        lv.setUint16(8, options.stored ? 0 : 8, true);
        lv.setUint32(14, crc, true);
        lv.setUint32(18, body.length, true);
        lv.setUint32(22, declared, true);
        lv.setUint16(26, nameBytes.length, true);
        local.set(nameBytes, 30);
        const central = new Uint8Array(46 + nameBytes.length);
        const cv = new DataView(central.buffer);
        cv.setUint32(0, 0x02014b50, true);
        cv.setUint16(4, 20, true);
        cv.setUint16(6, 20, true);
        cv.setUint16(10, options.stored ? 0 : 8, true);
        cv.setUint32(16, crc, true);
        cv.setUint32(20, body.length, true);
        cv.setUint32(24, declared, true);
        cv.setUint16(28, nameBytes.length, true);
        cv.setUint32(42, offset, true);
        central.set(nameBytes, 46);
        locals.push(local, body);
        centrals.push(central);
        offset += local.length + body.length;
    }
    if (!damaged) throw new Error(`The zip has no part '${String(options.damage)}'.`);
    if (options.lie !== undefined && !parts.some(([name]) => name === options.lie?.part)) {
        throw new Error(`The zip has no part '${options.lie.part}'.`);
    }
    const directory = concat(centrals);
    const end = new Uint8Array(22);
    const ev = new DataView(end.buffer);
    ev.setUint32(0, 0x06054b50, true);
    ev.setUint16(8, parts.length, true);
    ev.setUint16(10, parts.length, true);
    ev.setUint32(12, directory.length, true);
    ev.setUint32(16, offset, true);
    return concat([...locals, directory, end]);
}

/** An OLE compound file (MS-CFB, version 3) whose directory lists `names`, four to a sector. */
function ole(names: readonly string[]): Uint8Array {
    const size = 512;
    const dirSectors = Math.ceil(names.length / 4);
    const file = new Uint8Array(size * (2 + dirSectors));
    const view = new DataView(file.buffer);
    file.set([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
    view.setUint16(24, 0x3E, true);
    view.setUint16(26, 3, true);
    view.setUint16(28, 0xFFFE, true);
    view.setUint16(30, 9, true);
    view.setUint16(32, 6, true);
    view.setUint32(44, 1, true);
    view.setUint32(48, 1, true);
    view.setUint32(56, 4096, true);
    view.setUint32(60, 0xFFFFFFFE, true);
    view.setUint32(68, 0xFFFFFFFE, true);
    for (let i = 0; i < 109; i++) view.setUint32(76 + i * 4, i === 0 ? 0 : 0xFFFFFFFF, true);
    // The FAT, in sector 0: itself, then the directory's chain, sector 1 onward.
    file.fill(0xFF, size, size * 2);
    view.setUint32(size, 0xFFFFFFFD, true);
    for (let s = 1; s <= dirSectors; s++) view.setUint32(size + s * 4, s === dirSectors ? 0xFFFFFFFE : s + 1, true);
    names.forEach((name, i) => {
        const entry = size * 2 + i * 128;
        const written = `${name}\0`;
        for (let c = 0; c < written.length; c++) view.setUint16(entry + c * 2, written.charCodeAt(c), true);
        view.setUint16(entry + 64, written.length * 2, true);
        file[entry + 66] = i === 0 ? 5 : 2;
    });
    return file;
}

const contentTypes = [
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
    '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">',
    '  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>',
    '  <Default Extension="xml" ContentType="application/xml"/>',
    '  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>',
    '  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>',
    '  ',
    '</Types>',
].join('\n');

const rootRels = [
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">',
    '  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>',
    '</Relationships>',
].join('\n');

const workbook = text(['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n',
    '<workbook xmlns="{Main}" xmlns:r="{Rel}"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>']);

function workbookRels(extra: string): string {
    return text(['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n',
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">\n',
        '  <Relationship Id="rId1" Type="{Rel}/worksheet" Target="worksheets/sheet1.xml"/>\n',
        `  ${extra}\n`,
        '</Relationships>']);
}

function sheet(data: string, head: string, tail: string): string {
    return text(['<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n',
        `<worksheet xmlns="{Main}" xmlns:r="{Rel}">${head}<sheetData>${data}</sheetData>${tail}</worksheet>`]);
}

/** The parts of a workbook of one worksheet, with the parts given beside it, each related from the workbook part by its
 * relationship type's last word, as the C# tests' `Fixtures.Book` lays them out. */
function book(given: Json): (readonly [string, string])[] {
    const parts = object(given);
    known(parts, 'sheet', 'sheetData', 'head', 'tail', 'sharedStrings', 'styles', 'theme', 'metadata', 'workbook');
    const sheetXml = parts['sheet'] !== undefined
        ? text(parts['sheet'])
        : sheet(text(key(parts, 'sheetData')), optional(parts, 'head', text, ''), optional(parts, 'tail', text, ''));
    const result: (readonly [string, string])[] = [
        ['[Content_Types].xml', contentTypes],
        ['_rels/.rels', rootRels],
        ['xl/workbook.xml', optional(parts, 'workbook', text, workbook)],
        ['xl/worksheets/sheet1.xml', sheetXml],
    ];
    let rels = '';
    let id = 2;
    const add = (name: string, type: string, target: string): void => {
        if (parts[name] === undefined) return;
        result.push([`xl/${target}`, text(parts[name])]);
        rels += `<Relationship Id="rId${String(id++)}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/${type}" Target="${target}"/>`;
    };
    add('sharedStrings', 'sharedStrings', 'sharedStrings.xml');
    add('styles', 'styles', 'styles.xml');
    add('theme', 'theme', 'theme/theme1.xml');
    add('metadata', 'sheetMetadata', 'metadata.xml');
    result.push(['xl/_rels/workbook.xml.rels', workbookRels(rels)]);
    return result;
}

/** A zip of the parts, each written as UTF-8 and deflated, as another program would write one. */
export function zipOf(parts: readonly (readonly [string, string])[]): Promise<Uint8Array> {
    return zip(parts, { stored: false, damage: undefined, lie: undefined });
}

/** The file a read case reads. */
export async function fileOf(value: Json): Promise<Uint8Array> {
    const file = object(value);
    known(file, 'bytes', 'ole', 'zip', 'book', 'stored', 'damage', 'lie');
    if (file['bytes'] !== undefined) return bytesOf(list(file['bytes']));
    if (file['ole'] !== undefined) return ole(list(file['ole']).map(text));
    let parts: (readonly [string, string])[];
    if (file['zip'] !== undefined) {
        parts = list(file['zip']).map(part => {
            const [name, content] = list(part);
            if (name === undefined || content === undefined) throw new Error(`${JSON.stringify(part)} is not a part's name and its content.`);
            return [text(name), text(content)] as const;
        });
    }
    else if (file['book'] !== undefined) parts = book(file['book']);
    else throw new Error(`${JSON.stringify(value)} is not a file.`);
    const lie = file['lie'];
    return zip(parts, {
        stored: optional(file, 'stored', flag, false),
        damage: optional<string | undefined>(file, 'damage', string, undefined),
        lie: isObject(lie) ? { part: string(key(lie, 'part')), declared: number(key(lie, 'declared')) } : undefined,
    });
}
