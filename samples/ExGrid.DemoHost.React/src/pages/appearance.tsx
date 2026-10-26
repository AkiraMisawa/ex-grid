import type { CSSProperties, ReactElement } from 'react';
import { cellTextMeasure, type BorderStyle, type CellAppearance, type CellState, type CellTextMeasure, type GridColumn, type RowKind } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';

/** One row of these grids: its position, and the values its cells show. */
interface AppearanceRow {
    readonly index: number;
    readonly label: string;
    readonly number?: number;
    readonly text: string;
}

/** Excel's thirteen line styles, in the order the eleventh Windows run drew them, with the names the cells show. */
const styles: readonly (readonly [BorderStyle, string])[] = Object.freeze([
    ['hair', 'Hair'], ['thin', 'Thin'], ['medium', 'Medium'], ['thick', 'Thick'], ['double', 'Double'],
    ['dotted', 'Dotted'], ['dashed', 'Dashed'], ['dashDot', 'DashDot'], ['dashDotDot', 'DashDotDot'],
    ['mediumDashed', 'MediumDashed'], ['mediumDashDot', 'MediumDashDot'], ['mediumDashDotDot', 'MediumDashDotDot'],
    ['slantedDashDot', 'SlantedDashDot'],
]);

const red = 0xFF0000;
const yellow = 0xFFFF00;
const none: CellAppearance = Object.freeze({});

const row = (index: number, fields: Partial<AppearanceRow> = {}): AppearanceRow => ({ index, label: '', text: '', ...fields });

// Held once, never rebuilt per render: a new array or function would paint every row again (ADR-0003).
const borderRows: readonly AppearanceRow[] = Object.freeze(Array.from({ length: 15 }, (_, i) => row(i, { label: styles[i]?.[1] ?? '' }))); // oxlint-disable-line ex-grid/no-stand-in-default -- the two rows past the thirteen styles carry no line

// Even widths, so every gridline falls on a device pixel at 150% as well.
const borderColumns: readonly GridColumn<AppearanceRow>[] = Object.freeze([
    { name: 'A', type: 'text', value: (r: AppearanceRow) => r.label, width: 110 },
    { name: 'B', type: 'text', value: () => '', width: 64 },
    { name: 'C', type: 'text', value: () => '', width: 64 },
    { name: 'D', type: 'text', value: () => '', width: 64 },
    { name: 'E', type: 'text', value: () => '', width: 48 },
]);

function borderOf(r: AppearanceRow, column: GridColumn<AppearanceRow>): CellAppearance {
    const style = styles[r.index]?.[0];
    if (column.name === 'B' && style !== undefined) return { bottom: { style } };
    if (column.name === 'D' && style !== undefined) return { right: { style } };
    if (column.name === 'C' && r.index === 3) return { bottom: { style: 'thick', colour: red } };
    if (column.name === 'C' && r.index === 4) return { fill: yellow };
    return none;
}

// "1234567890" in a column exactly as wide as it needs at the Standard preset's regular widths, and in one as wide
// as it needs at the bold ones: the grid's own widths, where the Blazor page takes Roboto's.
const boldNumber = 1_234_567_890;

/** The narrowest whole column whose content box holds the number at these widths. */
function fitting(measure: CellTextMeasure): number {
    const needed = measure.textWidthPx(String(boldNumber));
    let width = Math.floor(needed);
    while (measure.contentWidthPx(width) < needed) width += 1;
    return width;
}

const standard = cellTextMeasure('standard');

const boldRows: readonly AppearanceRow[] = Object.freeze([
    row(0, { label: 'Regular', number: boldNumber }),
    row(1, { label: 'Bold', number: boldNumber }),
]);

const boldColumns: readonly GridColumn<AppearanceRow>[] = Object.freeze([
    { name: 'Label', type: 'text', value: (r: AppearanceRow) => r.label, width: 100 },
    { name: 'Fits', type: 'number', value: (r: AppearanceRow) => r.number, width: fitting(standard) },
    { name: 'Wide', type: 'number', value: (r: AppearanceRow) => r.number, width: fitting(standard.bold) },
]);

const boldOf = (r: AppearanceRow): CellAppearance => (r.index === 1 ? { bold: true } : none);

// The Excel preset's widths: "1234567" needs 7 × 8.36 + 8 = 66.52px, so 67 fits it.
const italicRows: readonly AppearanceRow[] = Object.freeze([
    row(0, { text: 'fjfjfj', number: 1_234_567 }),
    row(1, { text: 'jWjWjW', number: 7_777_777 }),
]);

const italicColumns: readonly GridColumn<AppearanceRow>[] = Object.freeze([
    { name: 'Text', type: 'text', value: (r: AppearanceRow) => r.text, width: 120 },
    { name: 'Number', type: 'number', value: (r: AppearanceRow) => r.number, width: 67 },
]);

const italic: CellAppearance = Object.freeze({ italic: true });
const italicOf = (): CellAppearance => italic;

// The rows that carry a tint, at odd positions, so the detail rows among them (1, and 7 with its Missing state) are
// striped.
const groupRow = 3;
const totalRow = 5;
const missingRow = 7;

const tintRows: readonly AppearanceRow[] = Object.freeze(Array.from({ length: 9 }, (_, i) => row(i)));

const tintColumns: readonly GridColumn<AppearanceRow>[] = Object.freeze(
    [...'ABCDEFGHIJ'].map((name): GridColumn<AppearanceRow> => ({ name, type: 'text', value: () => '', width: 44 })));

const headingNumber = (index: number): string => String(index + 1);

const tintKindOf = (r: AppearanceRow): RowKind => (r.index === groupRow ? 'group' : r.index === totalRow ? 'total' : 'detail');

const tintStateOf = (r: AppearanceRow): CellState => (r.index === missingRow ? 'missing' : 'normal');

function tintOf(r: AppearanceRow, column: GridColumn<AppearanceRow>): CellAppearance {
    if (r.index % 2 === 0) return none;
    switch (column.name) {
        case 'B': case 'G': return { bottom: { style: 'dashed' } };
        case 'C': case 'H': return { fill: yellow };
        case 'E': case 'J': return { fill: yellow, bottom: { style: 'dashed' } };
        default: return none;
    }
}

const headingWeights = { '--ex-row-group-weight': 700, '--ex-row-total-weight': 800 } as CSSProperties;

/** The page /appearance of the Blazor hosts: a per-cell Font, Fill and Borders, painted as Excel paints them
 * (ADR-0050 item 15, ADR-0071). Its bold grid is judged by the grid's own widths here: Roboto and the widths
 * measured for it are the MudBlazor Wrapper's, and join with `@ex-grid/mui`. */
export function Appearance(): ReactElement {
    return (
        <>
            <DemoNav route="appearance" />
            <h1>Cell appearance</h1>
            <p>
                A per-cell Font, Fill and Borders, as a Consumer supplies them (ADR-0050, item 15), painted as Excel paints
                them (ADR-0071). The browser layer reads these grids&apos; pixels.
            </p>

            <h2>Borders</h2>
            <p>
                Excel&apos;s thirteen line styles, black: each on the bottom of a cell in <i>B</i> and on the right of a cell
                in <i>D</i>. In <i>C</i>, a red thick line over a yellow Fill, for the Fill below it and the Focus above it.
            </p>
            <div id="appearance-borders">
                <ExGrid columns={borderColumns} rows={borderRows} cellAppearance={borderOf} density="excel"
                    viewportHeight={320} viewportWidth={360} />
            </div>

            <h2>Bold is judged by bold widths</h2>
            <p>
                The grid&apos;s own regular and bold widths. The same number in every cell: <i>Fits</i> is as wide as the
                number needs at the regular widths, so the bold one there is <code>####</code>; <i>Wide</i> is as wide as it
                needs bold.
            </p>
            <div id="appearance-bold">
                <ExGrid columns={boldColumns} rows={boldRows} cellAppearance={boldOf} density="standard"
                    viewportHeight={140} viewportWidth={420} />
            </div>

            <h2>Italic is not cut</h2>
            <p>
                Italic, judged by the regular widths: text against the left edge, and a number against the right edge of a
                column exactly as wide as the number needs.
            </p>
            <div id="appearance-italic">
                <ExGrid columns={italicColumns} rows={italicRows} cellAppearance={italicOf} density="excel"
                    viewportHeight={120} viewportWidth={320} />
            </div>

            <h2>Lines keep a row&apos;s tint</h2>
            <p>
                Row Stripes, a group row, a total row and a Missing state, on cells with and without lines and a Fill.
                <i> A</i> to <i>E</i> are pinned, <i>F</i> to <i>J</i> scroll: in each, the first cell is plain, the second
                has a dashed bottom line, the third a yellow Fill, the fourth no line of its own but the Fill beside it over
                its gridline, and the fifth the Fill and the dashed line. The tint lies beneath the lines and over the Fill,
                on every cell alike. Rows 1, 3, 5 and 7 carry them; the rows between are plain, so no Fill reaches across
                from one of them to the next.
            </p>
            <div id="appearance-tints">
                <ExGrid columns={tintColumns} rows={tintRows} cellAppearance={tintOf} rowKind={tintKindOf}
                    cellState={tintStateOf} stripeRows pinnedColumnCount={5} density="excel" viewportHeight={220}
                    viewportWidth={480} />
            </div>

            <h2>Marked Row Headings keep their weight</h2>
            <p>Select a cell or a whole row. Group and Total Row Headings retain their own font weight under the Heading Mark.</p>
            <div id="appearance-headings" style={headingWeights}>
                <ExGrid columns={tintColumns} rows={tintRows} rowKind={tintKindOf} rowHeadings={headingNumber}
                    rowHeadingWidth={44} viewportHeight={300} viewportWidth={480} />
            </div>
        </>
    );
}
