// The styles part written from XlsxStyles, as ExGrid.Data.Xlsx's StylesWriter writes it: what the styles reader reads,
// and the one named style (Normal) every styles part carries.

import { hex8, roundTrip } from './invariant';
import { mainNamespace } from './names';
import type { XlsxBorder, XlsxBorderEdge, XlsxCellFormat, XlsxColor, XlsxFill, XlsxFont, XlsxStyles } from './styles';
import { encodeXString } from './x-string';
import type { XmlTextWriter } from './xml-writer';
import { unreachable } from './must';

export function writeStyles(w: XmlTextWriter, styles: XlsxStyles): void {
    w.start('styleSheet', mainNamespace);
    if (styles.numberFormats.size > 0) {
        w.start('numFmts');
        count(w, styles.numberFormats.size);
        for (const [id, code] of [...styles.numberFormats].toSorted(([a], [b]) => a - b)) {
            w.start('numFmt');
            w.attribute('numFmtId', String(id));
            w.attribute('formatCode', encodeXString(code));
            w.end();
        }
        w.end();
    }

    w.start('fonts');
    count(w, styles.fonts.length);
    for (const font of styles.fonts) writeFont(w, font);
    w.end();

    w.start('fills');
    count(w, styles.fills.length);
    for (const fill of styles.fills) writeFill(w, fill);
    w.end();

    w.start('borders');
    count(w, styles.borders.length);
    for (const border of styles.borders) writeBorder(w, border);
    w.end();

    w.start('cellStyleXfs');
    count(w, 1);
    w.start('xf');
    w.attribute('numFmtId', '0');
    w.attribute('fontId', '0');
    w.attribute('fillId', '0');
    w.attribute('borderId', '0');
    w.end();
    w.end();

    w.start('cellXfs');
    count(w, styles.cellFormats.length);
    for (const format of styles.cellFormats) writeCellFormat(w, format);
    w.end();

    w.start('cellStyles');
    count(w, 1);
    w.start('cellStyle');
    w.attribute('name', 'Normal');
    w.attribute('xfId', '0');
    w.attribute('builtinId', '0');
    w.end();
    w.end();

    const indexed = styles.indexedColors;
    if (indexed !== null && indexed.length > 0) {
        w.start('colors');
        w.start('indexedColors');
        for (const argb of indexed) {
            w.start('rgbColor');
            w.attribute('rgb', hex8(argb));
            w.end();
        }
        w.end();
        w.end();
    }
    w.end();
}

function count(w: XmlTextWriter, n: number): void {
    w.attribute('count', String(n));
}

function writeFont(w: XmlTextWriter, font: XlsxFont): void {
    w.start('font');
    // The schema's order: b, i, strike, …, u, …, sz, color, name.
    if (font.bold) w.element('b', null);
    if (font.italic) w.element('i', null);
    if (font.strike) w.element('strike', null);
    if (font.underline !== 'none') {
        w.start('u');
        if (font.underline !== 'single') w.attribute('val', font.underline);
        w.end();
    }
    if (font.size !== null) {
        w.start('sz');
        w.attribute('val', roundTrip(font.size));
        w.end();
    }
    if (font.color !== null) writeColor(w, 'color', font.color);
    if (font.name !== null) {
        w.start('name');
        w.attribute('val', font.name);
        w.end();
    }
    w.end();
}

function writeFill(w: XmlTextWriter, fill: XlsxFill): void {
    w.start('fill');
    if (fill.isGradient) {
        // A gradient's stops are not read, so none is written: an empty gradient.
        w.start('gradientFill');
        w.end();
    } else {
        w.start('patternFill');
        w.attribute('patternType', fill.pattern === null ? 'none' : fill.pattern);
        if (fill.foreground !== null) writeColor(w, 'fgColor', fill.foreground);
        if (fill.background !== null) writeColor(w, 'bgColor', fill.background);
        w.end();
    }
    w.end();
}

function writeBorder(w: XmlTextWriter, border: XlsxBorder): void {
    w.start('border');
    if (border.diagonalUp) w.attribute('diagonalUp', '1');
    if (border.diagonalDown) w.attribute('diagonalDown', '1');
    writeEdge(w, 'left', border.left);
    writeEdge(w, 'right', border.right);
    writeEdge(w, 'top', border.top);
    writeEdge(w, 'bottom', border.bottom);
    writeEdge(w, 'diagonal', border.diagonal);
    w.end();
}

function writeEdge(w: XmlTextWriter, name: string, edge: XlsxBorderEdge): void {
    w.start(name);
    if (edge.style !== 'none') w.attribute('style', edge.style);
    if (edge.color !== null) writeColor(w, 'color', edge.color);
    w.end();
}

function writeCellFormat(w: XmlTextWriter, format: XlsxCellFormat): void {
    w.start('xf');
    w.attribute('numFmtId', String(format.numberFormatId));
    w.attribute('fontId', String(format.fontId));
    w.attribute('fillId', String(format.fillId));
    w.attribute('borderId', String(format.borderId));
    w.attribute('xfId', '0');
    if (format.numberFormatId !== 0) w.attribute('applyNumberFormat', '1');
    if (format.fontId !== 0) w.attribute('applyFont', '1');
    if (format.fillId !== 0) w.attribute('applyFill', '1');
    if (format.borderId !== 0) w.attribute('applyBorder', '1');
    const a = format.alignment;
    if (a !== null) {
        w.attribute('applyAlignment', '1');
        w.start('alignment');
        if (a.horizontal !== 'general') w.attribute('horizontal', a.horizontal);
        if (a.vertical !== 'bottom') w.attribute('vertical', a.vertical);
        if (a.textRotation !== 0) w.attribute('textRotation', String(a.textRotation));
        if (a.wrapText) w.attribute('wrapText', '1');
        if (a.indent !== 0) w.attribute('indent', String(a.indent));
        if (a.shrinkToFit) w.attribute('shrinkToFit', '1');
        w.end();
    }
    w.end();
}

function writeColor(w: XmlTextWriter, name: string, color: XlsxColor): void {
    w.start(name);
    switch (color.kind) {
        case 'automatic':
            w.attribute('auto', '1');
            break;
        case 'rgb':
            w.attribute('rgb', hex8(color.value));
            break;
        case 'theme':
            w.attribute('theme', String(color.value));
            break;
        case 'indexed':
            w.attribute('indexed', String(color.value));
            break;
        default:
            unreachable(color.kind);
    }
    if (color.tint !== 0) w.attribute('tint', roundTrip(color.tint));
    w.end();
}
