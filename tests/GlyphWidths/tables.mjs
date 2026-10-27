// Writes the per-glyph tables the estimate charges letters and currency signs by (ADR-0016; ticket
// 83), from the records beside this file:
//   src/ExGrid/Columns/DefaultGlyphWidths.cs  the core's, at 14px and at 12px;
//   src/ExGrid.MudBlazor/RobotoGlyphTable.cs  ExGrid.MudBlazor's, at 14px, which the core scales;
//   src/npm/core/src/columns/default-glyph-widths.ts  the Script Core's, the same as the C# core's.
// Run it after measure.mjs: node tests/GlyphWidths/tables.mjs
// With --check it writes nothing, and fails when a table differs from what it would write: the Script
// Core's tests run it so, and the two cores' tables cannot part (ADR-0350).
//
// A glyph joins a table only where every record shows the face drawing it itself. A glyph the face
// lacks is painted in a fallback the platform chooses, so it is left to the other class.
// - The core's: a glyph DejaVu Sans draws, which is what Linux paints for system-ui. Its width is the
//   widest any core record shows: macOS's system-ui stack and Windows' (Segoe UI under an English
//   display, Yu Gothic UI under a Japanese one), fallback included, in each browser measured, and
//   DejaVu Sans.
// - Roboto's: a glyph Roboto draws, at the wider of its 14px width and its 12px one scaled to 14.
// Each width is the widest at 400, 500 and 600 (regular) or at 700 (bold), a shade over.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { classed, digit, isFullWidth, narrow, wide } from './classes.mjs';

// fileURLToPath, not the URL's pathname: on Windows the pathname is /C:/…, which is no path there.
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');

const checking = process.argv.includes('--check');
const stale = [];
/** Writes a table, or with --check notes it when it differs from what is written. */
const put = (file, content) => {
  const at = path.join(repo, file);
  if (!checking) {
    fs.writeFileSync(at, content);
  } else if (!fs.existsSync(at) || fs.readFileSync(at, 'utf8') !== content) {
    stale.push(file);
  }
};

// A record is <face>.<platform>[.<browser>].json. The platform in its name can say more than the
// one measure.mjs writes into it: windows-ja is Windows under a Japanese display language.
const records = (face) => fs.readdirSync(here)
  .filter((f) => f.startsWith(`${face}.`) && f.endsWith('.json'))
  .map((f) => ({ ...JSON.parse(fs.readFileSync(path.join(here, f), 'utf8')), named: f.split('.')[1] }));
const regular = [400, 500, 600];
const bold = [700];
const shade = (px) => Math.ceil((px + 0.005) * 100) / 100;
const eligible = (g) => /[\p{L}\p{Sc}]/u.test(g) && !classed.includes(g);
const width = (r, g, size, weights) => {
  const i = r.glyphs.indexOf(g);
  if (i < 0) throw new Error(`${r.face} (${r.named}) has no reading for '${g}'.`);
  return Math.max(...weights.map((w) => r.sizes[size].glyphs[w][i] / 64));
};
const drawsItself = (r, g, family) => {
  const i = r.glyphs.indexOf(g);
  return i >= 0 && [...regular, ...bold].every((w) => r.painters[w][i] === family);
};

const core = [...records('system-ui'), ...records('dejavu-sans')];
const dejavu = records('dejavu-sans');
const roboto = records('roboto');
if (core.length < 2 || dejavu.length === 0 || roboto.length === 0) throw new Error('Measure the three faces first.');

const glyphsOf = (rs) => [...new Set(rs.flatMap((r) => r.glyphs))].filter(eligible)
  .sort((a, b) => a.codePointAt(0) - b.codePointAt(0));
const coreGlyphs = glyphsOf(core).filter((g) => dejavu.every((r) => drawsItself(r, g, 'DejaVu Sans')));
const coreAt = (size) => coreGlyphs.map((g) => [g,
  shade(Math.max(...core.map((r) => width(r, g, size, regular)))),
  shade(Math.max(...core.map((r) => width(r, g, size, bold))))]);
const robotoGlyphs = glyphsOf(roboto).filter((g) => roboto.every((r) => drawsItself(r, g, 'Roboto')));
const robotoAt14 = robotoGlyphs.map((g) => [g, ...[regular, bold].map((ws) =>
  shade(Math.max(...roboto.flatMap((r) => [width(r, g, '14', ws), width(r, g, '12', ws) * 14 / 12]))))]);

// Which browser measured a record: Edge names itself (Edg/) after the Chrome version it is built on.
const browser = (r) => {
  const edge = / Edg\/(\d+)/.exec(r.userAgent);
  return edge ? `Edge ${edge[1]}` : `Chrome ${/Chrome\/(\d+)/.exec(r.userAgent)?.[1] ?? '?'}`;
};
const measuredBy = (rs) => [...new Set(rs.map((r) => `${r.face} on ${r.named} in ${browser(r)} (${r.measured})`))].join(', ');
const entries = (rows) => rows.map(([g, r, b]) => `        (${JSON.stringify(g)}, ${r.toFixed(2)}, ${b.toFixed(2)}),`).join('\n');
const table = (name, size, rows, summary) => `    /// <summary>${summary}</summary>
    public static GlyphWidthTable ${name} { get; } = new(${size},
    [
${entries(rows)}
    ]);`;
// A comment, wrapped at 100 characters, so the generated header lists every record measured.
const wrap = (text) => {
  const lines = [];
  let line = '//';
  for (const word of text.split(' ')) {
    if (line.length + 1 + word.length > 100) { lines.push(line); line = '//'; }
    line += ` ${word}`;
  }
  return [...lines, line].join('\n');
};
const header = (rs) => `${wrap(`Generated by tests/GlyphWidths/tables.mjs from ${measuredBy(rs)}.`)}
// Do not edit by hand: measure again and run it (tests/GlyphWidths/README.md).`;

put('src/ExGrid/Columns/DefaultGlyphWidths.cs', `${header(core)}

namespace ExGrid.Columns;

/// <summary>
/// The core's per-glyph widths (ADR-0016; ticket 83): each letter and currency sign DejaVu Sans
/// draws, at the widest of macOS's <c>system-ui</c>, Windows' <c>system-ui</c> (Segoe UI under an
/// English display, Yu Gothic UI under a Japanese one) and DejaVu Sans, so a date's month or a
/// currency symbol is charged what it paints rather than the other class. Regular is the widest at
/// 400, 500 and 600, bold at 700, a shade over.
/// </summary>
internal static class DefaultGlyphWidths
{
${table('At14', 14, coreAt('14'), 'At 14px: Compact, Standard and Comfortable.')}

${table('At12', 12, coreAt('12'), 'At 12px, measured there rather than scaled: Excel.')}
}
`);
put('src/ExGrid.MudBlazor/RobotoGlyphTable.cs', `${header(roboto)}

using ExGrid.Columns;

namespace ExGrid.MudBlazor;

/// <summary>
/// Roboto's per-glyph widths (ADR-0016; ticket 83): each letter and currency sign Roboto draws, at
/// 14px, covering its 12px width scaled to 14 too, since the core scales the table to the resolved
/// font size. Regular is the widest at 400, 500 and 600, bold at 700, a shade over.
/// </summary>
internal static class RobotoGlyphTable
{
${table('At14', 14, robotoAt14, 'At 14px, which the core scales to the resolved size.')}
}
`);
const tsGlyph = (g) => `'${g.replaceAll('\\', '\\\\').replaceAll("'", "\\'")}'`;
const tsEntries = (rows) => rows.map(([g, r, b]) => `    [${tsGlyph(g)}, ${r.toFixed(2)}, ${b.toFixed(2)}],`).join('\n');
put('src/npm/core/src/columns/default-glyph-widths.ts', `${header(core)}
import { GlyphWidthTable } from './glyph-width-table';

type Row = readonly [glyph: string, regularPx: number, boldPx: number];

// At 14px: Compact, Standard and Comfortable.
const at14: readonly Row[] = [
${tsEntries(coreAt('14'))}
];

// At 12px, measured there rather than scaled: Excel.
const at12: readonly Row[] = [
${tsEntries(coreAt('12'))}
];

const built = new Map<14 | 12, GlyphWidthTable>();

/**
 * The core's per-glyph widths at \`sizePx\` (ADR-0016; ticket 83), the same as the C# core's
 * DefaultGlyphWidths: each letter and currency sign DejaVu Sans draws, at the widest of macOS's
 * \`system-ui\`, Windows' \`system-ui\` (Segoe UI under an English display, Yu Gothic UI under a Japanese
 * one) and DejaVu Sans. Regular is the widest at 400, 500 and 600, bold at 700, a shade over. Built on
 * first asking, so loading the module does no work.
 */
export function defaultGlyphWidthsAt(sizePx: 14 | 12): GlyphWidthTable {
    const known = built.get(sizePx);
    if (known !== undefined) {
        return known;
    }
    const table = new GlyphWidthTable(sizePx, sizePx === 14 ? at14 : at12);
    built.set(sizePx, table);
    return table;
}
`);
// @ex-grid/mantine's (ADR-0390): Mantine's default stack, -apple-system, BlinkMacSystemFont, Segoe UI, Roboto,
// Helvetica, Arial, sans-serif, resolves to macOS's system face, Segoe UI on every Windows (it is named ahead of
// any display language's face), and on Linux to Roboto where it is installed, Liberation Sans for Helvetica and
// Arial, or DejaVu Sans. A glyph joins its table where each Linux candidate draws it itself; every width, class or
// glyph, is the widest any of those records shows, at 14px and at 12px scaled to 14.
const mantine = [...records('system-ui').filter((r) => r.named !== 'windows-ja'), ...roboto, ...dejavu, ...records('liberation-sans')];
const linux = [[roboto, 'Roboto'], [dejavu, 'DejaVu Sans'], [records('liberation-sans'), 'Liberation Sans']];
if (linux.some(([rs]) => rs.length === 0)) throw new Error('Measure liberation-sans first.');
const mantineGlyphs = glyphsOf(mantine).filter((g) => linux.every(([rs, family]) => rs.every((r) => drawsItself(r, g, family))));
const widest = (gs, ws) => shade(Math.max(...mantine.flatMap((r) => gs.flatMap((g) => [width(r, g, '14', ws), width(r, g, '12', ws) * 14 / 12]))));
const mantineAt14 = mantineGlyphs.map((g) => [g, widest([g], regular), widest([g], bold)]);
// The other class is what a glyph outside the classes and the table costs: the widest glyph any record paints,
// fallbacks included, short of the full-width blocks the em is charged for.
const unclassed = [...new Set(mantine.flatMap((r) => r.glyphs))]
  .filter((g) => !classed.includes(g) && !isFullWidth(g.codePointAt(0)) && !mantineGlyphs.includes(g));
const mantineClasses = {
  wide: widest(wide, regular), digit: widest(digit, regular), narrow: widest(narrow, regular), other: widest(unclassed, regular),
  boldWide: widest(wide, bold), boldDigit: widest(digit, bold), boldNarrow: widest(narrow, bold), boldOther: widest(unclassed, bold),
};
put('src/npm/mantine/src/mantine-glyph-widths.ts', `${header(mantine)}
import { GlyphWidthTable } from '@ex-grid/core';

type Row = readonly [glyph: string, regularPx: number, boldPx: number];

/** The class widths of Mantine's default stack at 14px, which the core scales to the resolved size: each the
 * widest any record shows at 400, 500 and 600 (regular) or 700 (bold), at 14px or at 12px scaled, a shade over. */
export const mantineClassWidths = Object.freeze({
    fontSizePx: 14,
    wideWidthPx: ${mantineClasses.wide.toFixed(2)},
    digitWidthPx: ${mantineClasses.digit.toFixed(2)},
    narrowWidthPx: ${mantineClasses.narrow.toFixed(2)},
    otherWidthPx: ${mantineClasses.other.toFixed(2)},
    boldWideWidthPx: ${mantineClasses.boldWide.toFixed(2)},
    boldDigitWidthPx: ${mantineClasses.boldDigit.toFixed(2)},
    boldNarrowWidthPx: ${mantineClasses.boldNarrow.toFixed(2)},
    boldOtherWidthPx: ${mantineClasses.boldOther.toFixed(2)},
} as const);

const at14: readonly Row[] = [
${tsEntries(mantineAt14)}
];

let built: GlyphWidthTable | undefined;

/** The letters and currency signs every Linux face of Mantine's stack draws itself, at 14px. Built on first
 * asking, so loading the module does no work. */
export function mantineGlyphWidths(): GlyphWidthTable {
    built ??= new GlyphWidthTable(14, at14);
    return built;
}
`);
if (stale.length > 0) {
  console.error(`Not what tests/GlyphWidths/tables.mjs writes from the records: ${stale.join(', ')}. Run it without --check.`);
  process.exit(1);
}
console.log(`core: ${coreGlyphs.length} glyphs; Roboto: ${robotoGlyphs.length} glyphs; Mantine: ${mantineGlyphs.length} glyphs${checking ? ', every table as written' : ''}`);
