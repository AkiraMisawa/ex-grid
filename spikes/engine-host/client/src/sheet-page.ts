// /core/sheet over the Engine Host (ADR-0370 section 6): render-bench's Sheet (Bench.Live/CoreSheet.razor)
// computed by ExSheet.Engine in the Worker, its Values drawn by ExGrid's Script Core on the page. ExSheet's
// own Script Core does not exist yet, so this page stands in for it as far as core-bench.js needs: digits
// typed with the Focus on a cell, and Enter, enter them into that cell, as ExSheet's editor would; the grid
// moves the Focus as it does for Enter. Every other key is the grid's.
//
// What is timed is what a Consumer of ExSheet's Script Core would see: the keystroke, the edit crossing to
// the Worker, its recalculation, the Window of Values crossing back, and the grid's paint.

import { createGrid, type GridColumn } from '../../../../src/npm/core/src/index';
import { EngineClient } from './engine-client';
import { EngineWindowSource, type WindowAnswer } from './engine-window-source';
import { recordBoot, waitForPaint } from './boot';

/** The rows that read A1, as CoreSheet. */
const sheetRows = 2_000;
/** The columns drawn: A to Z. The Worker answers these of every row asked for. */
const columnCount = 26;
/** ExSheet's row height (ExSheet.DefaultRowHeightPx) and, near enough, its default column width. */
const rowHeightPx = 28;
const columnWidthPx = 70;

interface SheetRow {
    readonly index: number;
    readonly texts: readonly (string | null)[];
}

interface WindowJson {
    readonly version: number;
    readonly start: number;
    readonly rows: readonly (readonly (string | null)[])[];
}

/** CoreSheet's document in the Sheet Document's JSON form (version 9): A1 a number, A2:A2001 read it, B to
 * J each add one to the cell on their left, L1 read by nothing. */
const documentJson = (): string => {
    const cells: object[] = [{ at: 'A1', number: 1 }, { at: 'L1', number: 0 }];
    for (let row = 2; row <= sheetRows + 1; row++) {
        cells.push({ at: `A${row}`, formula: '=$A$1+ROW()' });
        for (let c = 1; c < 10; c++) cells.push({ at: `${String.fromCharCode(65 + c)}${row}`, formula: `=${String.fromCharCode(64 + c)}${row}+1` });
    }
    return JSON.stringify({ version: 9, culture: 'en-US', name: 'Sheet1', cells });
};

const letters = (column: number): string => String.fromCharCode(65 + column);

const columns: readonly GridColumn<SheetRow>[] = Array.from({ length: columnCount }, (_, c) => ({
    name: letters(c),
    type: 'text',
    width: columnWidthPx,
    value: (row: SheetRow) => row.texts[c] ?? null,
}));

const sameRow = (painted: SheetRow, next: SheetRow): boolean =>
    painted.index === next.index && painted.texts.every((text, c) => text === next.texts[c]);

const toAnswer = (json: WindowJson, rowCount: number): WindowAnswer<SheetRow> => ({
    rows: json.rows.map((texts, i) => ({ index: json.start + i, texts })),
    start: json.start,
    totalCount: rowCount,
});

const main = async (): Promise<void> => {
    const engine = new EngineClient('/dist/engine-worker.js');
    const boot = await engine.ready;
    const opened = await engine.call<{ version: number; rowCount: number; engineMs: number }>('SheetOpen', documentJson());
    const openedAt = performance.now();
    const rowCount = opened.value.rowCount;
    const first = await engine.call<WindowJson>('SheetWindow', 0, 80, columnCount);
    const firstWindowAt = performance.now();

    const source = new EngineWindowSource<SheetRow>(
        async (range) => toAnswer((await engine.call<WindowJson>('SheetWindow', range.start, range.count, columnCount)).value, rowCount),
        sameRow,
        toAnswer(first.value, rowCount));

    const box = document.getElementById('core-bench');
    const element = box?.querySelector<HTMLElement>('#sheet');
    if (!box || !element) throw new Error('The page has no #core-bench with a #sheet inside.');
    createGrid(element, { columns, source, rowHeight: rowHeightPx, viewportWidth: 1200, viewportHeight: 600 });

    // What each edit cost the Worker, for the record beside what the driver saw.
    const edits: object[] = [];
    (window as unknown as { engineEdits: () => object[] }).engineEdits = () => edits.splice(0);

    // The stand-in for ExSheet's editor: digits typed on the Focus, entered by Enter. Heard on the box in the
    // capture phase, ahead of the grid's own listener on its root, which takes Enter to move the Focus.
    let typed = '';
    box.addEventListener('keydown', (event) => {
        if (/^[0-9.-]$/.test(event.key) && !event.ctrlKey && !event.metaKey && !event.altKey) {
            typed += event.key;
            event.preventDefault();
        } else if (event.key === 'Escape') {
            typed = '';
        } else if (event.key === 'Enter' && typed !== '') {
            const focus = /r(\d+)c(\d+)$/.exec(element.getAttribute('aria-activedescendant') ?? '');
            if (!focus) throw new Error('Enter with typed digits and no Focus.');
            const address = `${letters(Number(focus[2]))}${Number(focus[1]) + 1}`;
            const text = typed;
            typed = '';
            void source.write(async (held) => {
                const answer = await engine.call<WindowJson & { engineMs: number; windowMs: number }>('SheetEnter', address, text, held.start, held.count, columnCount);
                edits.push({ address, engineMs: answer.value.engineMs, windowMs: answer.value.windowMs, workerMs: answer.workerMs, roundTripMs: answer.roundTripMs });
                return toAnswer(answer.value, rowCount);
            });
        }
    }, true);

    const firstPaintAt = await waitForPaint(element);
    recordBoot({ boot, openedAt, firstWindowAt, firstPaintAt, openEngineMs: opened.value.engineMs });
};

main().catch((error: unknown) => {
    console.error(error);
});
