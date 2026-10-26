import { defineConfig } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
import { API_URL, BASE_URL, HOST_LOG, HOST_PORT, LATENCY_CONTROL_URL, REACT, SERVER } from './hosting.mjs';

// The host is started on whatever port BASE_URL names, so two checkouts — one per
// parallel agent — do not share a port: with reuseExistingServer a second runner on
// the same port would be driving the OTHER checkout's DemoHost and passing (AGENTS.md,
// "Working in parallel"). Which host, and every port derived from it, is hosting.mjs's.
const HOST_URL = `http://localhost:${HOST_PORT}`;

// A fresh log per run: CON-6 reads what each test appended to it. The config is loaded
// again in every worker, and a worker is replaced after each failing test, so only the
// runner itself clears it — a worker doing so would erase the lines it is meant to read.
if (SERVER && process.env.TEST_WORKER_INDEX === undefined) {
    fs.rmSync(HOST_LOG, { force: true });
}

// What starts the hosts. By default the projects themselves, with `dotnet run`: the
// edit-and-rerun loop. With EXGRID_HOSTS naming a directory, the hosts as
// `dotnet publish -c Release` wrote them, one directory each — `wasm`, `server` and `api` —
// which is what CI's full run drives (ADR-0041): built once, as a Consumer deploys it.
// The published Server host and demo API server run where they were published (their
// content root) under ASPNETCORE_ENVIRONMENT=Development, as their launch profiles set it
// for `dotnet run`, and the WebAssembly host's files are served by static-host.mjs, which
// does what the dev server does. So what changes is how the hosts were built, not the
// environment they run in.
const PUBLISHED = process.env.EXGRID_HOSTS ? path.resolve(process.env.EXGRID_HOSTS) : null;
const published = (name) => {
    const dir = path.join(PUBLISHED, name);
    if (!fs.existsSync(dir)) {
        throw new Error(`EXGRID_HOSTS is "${PUBLISHED}", which holds no "${name}" directory.`);
    }
    return dir;
};
const development = { ASPNETCORE_ENVIRONMENT: 'Development' };

// Started here so `npx playwright test` is the whole command on any machine. An
// already-running host is reused, which is what makes an edit-and-rerun loop quick.
//
// Into the run's own output, which §22 Step 4 tees into layer3.log. On WebAssembly the
// log the renderer writes is the browser console, which fixtures.mjs reads, and this is
// the dev server's side. On Server the circuit's log is this process's, and the host
// also appends it to HOST_LOG so the fixture can read it per test (CON-6). A reused
// host was started elsewhere and its output is wherever that was.
// The React DemoHost is static files as `vite build` writes them (ADR-0350 section 13), served by
// static-host.mjs as the published WebAssembly host is: installed and built here unless EXGRID_HOSTS holds
// a `react` build, which is what CI drives.
const REACT_HOST = '../../samples/ExGrid.DemoHost.React';
const reactHost = REACT
    ? {
        command: PUBLISHED
            ? `node static-host.mjs ${JSON.stringify(published('react'))} ${HOST_PORT}`
            : `npm --prefix ${REACT_HOST} ci && npm --prefix ${REACT_HOST} run build && node static-host.mjs ${REACT_HOST}/dist ${HOST_PORT}`,
        url: `${BASE_URL}/wide`,
        reuseExistingServer: true,
        timeout: 180_000,
        stdout: 'pipe',
        stderr: 'pipe',
    }
    : null;

// The part of the suite the Script Core passes on the React host (ADR-0350 step 1), which grows with each
// slice until it is the whole suite and this goes. Named by the tests' ids, so a test it holds that fails
// again fails the run, and one outside it is not yet claimed. PF-1 counts calls into ex-grid.js, a Blazor
// host's interop, and is not a React test as written. Of ExChart's Showcases, the tests that read a recorded table
// of decimal figures are not claimed: a C# decimal keeps the places it was written with (3.30), which a
// TypeScript number does not (3.3), so those records change when Blazor draws through the Script Core
// (ADR-0370, step 2). Of ExPivot's, /pivot and /pivot-risk under their own markup, Find in the report included,
// /pivot-db's PV-20, whose server pivot asks the Leaf Aggregates, /pivot-csv, and /pivot-live under its own markup;
// /pivot-db's and /pivot-live's report Windows (LV-) are the Blazor hosts' report source, and /pivot-live's code is
// the Blazor page's.
const REACT_SUITE = /\(BIG-1\)|\(BIG-3, SL-6, DOM-2\)|VZ-18, ADR-0012|VZ-1, BIG-2, DOM-1|\(BIG-5\)|\(BIG-5, ADR-0053\)|ADR-0004 P1, across a scroll|DOM-3, ADR-0004|FN-6, ADR-0004|ADR-0080 \/ A11Y-5: a display-only grid|A11Y-4 \/ ADR-0080: the root is the one tab stop|KB-8 \/ ADR-0012 \/ ADR-0080: on a grid without a field|KB-12 \/ ADR-0080: a display-only grid|never behind a scrollbar \(ADR-0012\/0013\) (with the platform's own scrollbars|with scrollbars that occupy layout|at every zoom level, after moving again)|\(VZ-15\)|UX-21 at the display's scale|UX-21: the mark over a Pinned Column's header|UX-21: under forced colors|UX-21 D2 at the display's scale|UX-18: with Pinned Columns and no Headings|UX-19 at the display's scale|then Tab cycles inside it \(KB-8\)|then Tab cycles inside the selection \(KB-8\)|then Tab leaves for the page \(KB-8, KB-44\)|\(KB-15\)|\(DOM-4\)|\(SR-1\)|A11Y-10: arrows|A11Y-9: a drag across 200 rows|A11Y-6: a wheel scroll|KB-10: Home and Ctrl\+Home|KB-40: an Extent carried|\(CP-4\/CP-5\/CP-6\/CP-10\)|\(CP-1\/CP-3\)|CP-6 \/ ADR-0005: a Ctrl\+C inside the Window|CP-7: a copy of rows beyond the Window|\(ADR-0170, CP-26, UX-6\)|\(ADR-0170, CP-27\)|\(ADR-0170, CP-32\)|\(ADR-0170, ADR-0012, CP-28, CP-31\)|CP-14 \/ ADR-0005: an Excel-shaped paste|CP-14 \/ ADR-0005: desktop Excel's paste|\(ED-2\/ED-4\)|ED-13: Control and Alt held together|in Overwrite an arrow commits and moves; in Caret|never blurs the grid mid-edit \(ED-3\)|Escape with nothing to dismiss keeps the keyboard, and the next Tab|Escape, Escape, then a character|CP-6\/CP-10\/CP-14: Ctrl\+C and Ctrl\+V reach the grid after an edit|in Overwrite Shift\+Home and Shift\+↓|in Caret Ctrl\+← and Ctrl\+→ move the caret by a word|KB-42: with an edit open|a Reject holds the editor with real keys|the grid is one tab stop, its Keyboard Field on a grid that edits|inside the editor Ctrl\+Z, Delete and Backspace are the input's own|Backspace opens an empty editor, and Escape leaves the cell as it was|Ctrl\+D fills down and Ctrl\+R fills right, and the browser keeps neither key|over a cell that does not edit the Keyboard Field is read-only, and no composition starts there|reorders within its group and clamps at its edge \(HG-14\)|grabbing a group moves the whole group|a resize drag with a real mouse applies on release|a drag past MaxWidth is recorded as it came|builtin Chrome a secondary click opens the grid's menu, not the browser's|builtin Chrome the ContextMenu key opens it on the Focus|CP-19 \/ ADR-0005\/0036: a menu copy writes|KB-28: Alt\+↓ is taken from the browser|builtin Chrome in a menu (Enter|Space) runs the item|builtin Chrome popovers are not clipped by the scroll container|builtin Chrome a popover taller than its grid|builtin Chrome keys typed while (Escape closes a filter panel|Enter on OK closes|Space on Cancel closes)|builtin Chrome a key typed while a press on OK closes a filter panel|builtin Chrome Tab and Shift\+Tab wrap through both halves|builtin Chrome the letters: S sorts, E goes to the search box|under the builtin Chrome (Ctrl\+F opens the grid's panel|a capital and a Shift\+Enter typed straight after Ctrl\+F|a match beyond the painted rows is reached|ADR-0240: a tick of Match case|Ctrl\+F in the find field selects|Ctrl\+F in a column's popover opens Find|Ctrl\+F in the Context Menu, and in the filter's|a Ctrl\+F typed straight after Alt\+Down|Escape closes the panel and hands|every painted match is marked as it is typed|a match scrolled into view is marked|Ctrl\+H opens the panel with Replace|with Replace shown, Enter in either field|Ctrl\+F in a field of the panel with Replace|ADR-0333: a column header Find reaches)|Tab stays inside the find panel|the find panel stands inside the grid's box|in the Cell Editor Ctrl\+F and Ctrl\+H are taken|KB-11: arrows typed in a Template|\(KB-20\/KB-21|\(KB-23\/KB-24|\(A11Y-17, ADR-0037\)|\(UX-14, ADR-0029\/0037\)|\(KB-27, ADR-0020\/0037\)|ADR-0270, CH-4: the tooltip lists|ADR-0271, ADR-0272: a legend click hides|ADR-0273, ADR-0272: a drag across the plot narrows|ADR-0272, CH-5: a drag begun before the data|ADR-0275, CH-8: the chart is an image|ADR-0282, CH-12: one click saves the (CSV|SVG)|ADR-0282, CH-10, CH-12: one click saves the PNG|ADR-0300, ADR-0302: ten years merge into months|ADR-0304: a scatter's intervals run both ways|ADR-0274: (the tooltip names the grid point|a drag turns the surface|the surface is seen from above|a drag to the right brings|the surface turns below|a drag with the right button pans|the arrow keys turn the surface|the wheel zooms the surface|a click on a grid point draws|the slice through the hole)|ADR-0274, ADR-0272: a drag turns the surface|CH-7, ADR-0274: a missing value is a hole|ADR-0282: the drawing is handed over as SVG text|ADR-0310: (a surface carries its colour bar|colours that diverge from zero)|ADR-0311: (a mesh is drawn as its own|a node is read in the tooltip)|ADR-0282, ADR-0274, CH-10: the 3D surface saves|ADR-0257: a surface the page interpolated names its method|ADR-0312: an axis and the vertical scale placed by their logarithm|ADR-0258: tenor columns chart one curve a row, from the one item "Chart"|ADR-0258: one cell holds dates and tenors, so the menu offers both|ADR-0258: Add to chart adds a curve, and refuses a curve into a history|ADR-0258, M3-Q6: a click on a point moves the grid's Focus|ADR-0290: (the tooltip reads the one point nearest|a rectangle dragged across the plot narrows both axes|the spread on a logarithmic axis is ticked by decades)|ADR-0290, ADR-0258: a click on a point moves the grid's Focus to its row|ADR-0280[^(]*(Exposure profile charts|CDS spread vs leverage|FX volatility|on Equity index a merged candle|Equity index packs its sessions|Equity index merges ten years|Binomial lattice prices a European call|Binomial lattice rings the nodes|Binomial lattice.s switch)[^(]*\(built-in\)|\(MK-1\)|\(MK-6\)|\(MK-7\)|a mark whose row was deleted elsewhere reports it as gone|ADR-0320: the spec.s colour paints the nodes|ADR-0321: the branch table is drawn as edges|ADR-0320: the tooltip on a node reads|ADR-0321: the tooltip over an edge, away from any node|ADR-0320, ADR-0274: a spec of a surface is drawn by ExSurface|ADR-0320: a spec edited in the field is read again|ADR-0340: a selected row narrows|ADR-0340: a Ctrl\+click adds|ADR-0340: the confirm hands on the values|ADR-0340: a sort is raised as the Board Diff|ADR-0340: a pivot's selected cells narrow by their items|DC-62: with OnLeave declared|under the builtin Chrome (a selection of numbers shows Average|selecting does not move the Viewport)|a summary the page answers, under the builtin Chrome SM-11 \/ ADR-0130 \(SM-|one striped row paint the same ground \(UX-15\)|paints its own ground over the stripe and still counts \(UX-16\)|paints its tint once, on its pinned and its scrollable cells alike|reads the same over a group row's pinned and scrollable cells|a Cell State ground and the overlays paint over the stripe \(UX-16\)|under forced colours no stripe is painted \(UX-16\)|the vertical scrollbar paints a thumb in its gutter|a pinned cell paints its row's rule as the scrollable cells show it| pivot\.spec\.mjs .*(\(builtin\)|ADR-0070 \(DC-61\): a control focused)| pivot-find\.spec\.mjs .*\(builtin\)| pivot-risk\.spec\.mjs .*\(builtin\)| pivot-db\.spec\.mjs .*PV-20.*\(builtin\)| pivot-csv\.spec\.mjs .*\(builtin\)| pivot-live\.spec\.mjs .*\(builtin[,)]|SR-2c: with the view at the left, a Row Heading click then Shift\+↓|DC-42: a drag across Row Headings selects whole rows|DC-43: a Row Heading drag at the bottom edge scrolls down|DC-42: Ctrl\+click on a Row Heading adds the row|a sized box: the grid is the box, rows reach its bottom|the rest of a flex column: the grid starts under the toolbar|the Viewport follows the window, both ways|a column menu with less than a row of room closes, and the keyboard is the grid|\(FN-12[acde], ADR-0016\)|\(SR-2[ade]|\(SR-2c, ADR-0052\)|ticket 21, ADR-0016|\(UX-11b, ADR-0045\)|\(FN-6a, ADR-0045\)|UX-3 \/ ST-3 \/ ADR-0027 P6|CON-4 \/ ADR-0013: no ResizeObserver loop|a toned Stale or Error cell paints in its state|the Modified mark is a triangle in the top right corner| appearance\.spec\.mjs .*DC-59: |DC-58: italic text is not cut|ADR-0050 item 15 \/ ADR-00(24|38|06)|UX-21 D1: marked Row Headings keep|UX-21 D1: a whole Row Heading shade|ERR-7: disposing a grid while|aria-rows\.spec\.mjs ADR-0033: \/features A11Y-3 \/ ADR-0033: a Header Group band is row 1, the header row 2, and the data rows count from 3, near the top and far down$| aria-rows\.spec\.mjs ADR-0033: \/features A11Y-2 \/ A11Y-3 \/ ADR-0033: an untiered header is row 1, the first data row 2, and the count is the data rows and the header row$| differential\.spec\.mjs ADR-0240 §8: ExGrid ends the same typed fast and slow, seed 1$| differential\.spec\.mjs ADR-0240 §8: ExGrid ends the same typed fast and slow, seed 2$| differential\.spec\.mjs ADR-0240 §8: ExGrid ends the same typed fast and slow, seed 3$| differential\.spec\.mjs ADR-0240 §8: ExGrid ends the same typed fast and slow, seed 987654321$| differential\.spec\.mjs ADR-0240 §8: ExPivot ends the same typed fast and slow, seed 1$| differential\.spec\.mjs ADR-0240 §8: ExPivot ends the same typed fast and slow, seed 2$| differential\.spec\.mjs ADR-0240 §8: ExPivot ends the same typed fast and slow, seed 3$| differential\.spec\.mjs ADR-0240 §8: ExPivot ends the same typed fast and slow, seed 987654321$| excel-keys\.spec\.mjs Ctrl\+Z and Ctrl\+Y reach the Consumer's history, and the keydown is taken \(KB-39, ADR-0007\)$| excel-keys\.spec\.mjs Delete raises one Clear Intent, and the Consumer blanks what it can \(ED-24, ADR-0054\)$| excel-keys\.spec\.mjs a display-only grid leaves Delete, Backspace, Ctrl\+Z, Ctrl\+D and Ctrl\+R to the page \(ED-25, KB-39, ADR-0054\/0035\/0007\)$| features\.spec\.mjs ADR-0014 \(amended 2026-09-29\): one cell copied inside the grid still fills the whole range$| features\.spec\.mjs ED-19: after a refused fill the editor stands, and Enter then raises exactly one Edit Intent, for the one cell; the fill raised none \(ADR-0035\)$| features\.spec\.mjs a refusal is announced in the root's live region in the built-in words, and again when repeated \(A11Y-16, ADR-0203, ERR-2\)$| features\.spec\.mjs CP-10 \/ ADR-0005: text selected with the mouse, then | features\.spec\.mjs in Overwrite Ctrl\+← commits and moves to the row's first column, Ctrl\+Shift\+← commits and extends to it; in Caret Ctrl\+← moves the caret \(ED-2, ADR-0010\)$| features\.spec\.mjs a Reject holds the commit Shift\+↓ asks for in Overwrite, and the Selection stays \(ED-2, ED-15, ADR-0034\)$| features\.spec\.mjs a display-only grid does not take printable keys away from the page \(ADR-0010\/0020\)$| features\.spec\.mjs DC-63: without a listener, a double click where no edit opens places the Focus and changes nothing else \(ADR-0063, DC-1\)$| features\.spec\.mjs inside a cell Enter never fires: it leaves and moves down \(KB-22, ADR-0020\/0037\)$| features\.spec\.mjs Escape leaves an Interactive cell and the grid keeps the keyboard \(KB-22, ADR-0037\)$| features\.spec\.mjs a held Space fires a one-action cell once \(KB-26, ADR-0037\)$| features\.spec\.mjs holding Space to enter a cell fires nothing \(KB-26, ADR-0037\)$| features\.spec\.mjs KB-3: off an Apple platform, Meta\+↓ moves no Focus, and Meta\+Enter in an open edit over a range fills nothing \(ADR-0012\)$| filter-panel\.spec\.mjs under the builtin Chrome WR-1 \/ ADR-0009\/0023: the value list ends with the Blank entry, its search narrows it, and Apply sends what was ticked \(builtin\)$| filter-panel\.spec\.mjs under the builtin Chrome WR-1 \/ ADR-0009: a column whose values are TooMany gets the condition form, and Apply sends its condition \(builtin\)$| filter-panel\.spec\.mjs under the builtin Chrome WR-1 \/ ADR-0009: a column that declares the condition form gets it alone, and Cancel sends nothing \(builtin\)$| find\.spec\.mjs under the builtin Chrome FD-1\/ADR-0240: a Tab typed once Find's field holds the keyboard moves on in the panel, before any frame has looked$| find\.spec\.mjs a grid with nothing to replace takes Ctrl\+H and opens nothing \(ADR-0332\)$| gestures\.spec\.mjs a ~10MB paste parses without freezing the grid \(PST-5, PST-6 recorded, ADR-0052\)$| gestures\.spec\.mjs the focus outline holds 3:1 against the cell ground under the default theme \(UX-9\)$| gestures\.spec\.mjs narrowing the scrollbar by token changes the gutter and the geometry follows \(UX-10\)$| gestures\.spec\.mjs a composing IME keydown is never taken \(ED-11, the listener guard\)$| heading-mark\.spec\.mjs UX-21 \(built-in Chrome\): ExPivot's report header is marked over the selected value's column and not over the labels', in the Focus outline's colour \(ADR-0008, 2026-10-13; ADR-0059\)$| headings\.spec\.mjs SR-2c: with the view at the top, a Column Heading click then Shift\+→ leaves scrollTop where it was$| headings\.spec\.mjs SR-2c: Shift\+↑ from a whole column leaves it one row short, and scrolls to show the Extent$| headings\.spec\.mjs DC-42: a drag across Column Headings selects whole columns, over the Headings and over the cells$| headings\.spec\.mjs DC-42: Shift\+press on a Column Heading extends from the Focus's column, and the drag goes on$| headings\.spec\.mjs DC-43: a Column Heading drag at the right edge scrolls sideways, and never down$| headings\.spec\.mjs SR-2e\/DC-42: Ctrl\+click on a Column Heading adds the column, and a second takes it out$| headings\.spec\.mjs SR-2e\/DC-42: Ctrl\+drag across Column Headings adds the columns crossed as one range$| input-text-order\.spec\.mjs ADR-0240: a row press commits all text typed immediately before it$| input-text-order\.spec\.mjs ADR-0240: typing followed immediately by a heading command keeps the full text \(builtin\)$| input-text-order\.spec\.mjs ADR-0240: a right-click without an edit holds the following menu command \(builtin\)$| key-field\.spec\.mjs \/features under the builtin Chrome A11Y-4 \/ ADR-0080: Tab from before the first grid, which edits, reaches its field past every ▾, and the next Tab leaves the grid; Shift\+Tab likewise$| key-field\.spec\.mjs \/features under the builtin Chrome A11Y-4 \/ ADR-0080: on the second grid, which edits nothing, Tab and Shift\+Tab reach its root and pass no ▾ on the way out$| key-field\.spec\.mjs A11Y-21 \/ ADR-0080 \/ ADR-0037 under the builtin Chrome: on \/cells, while Interactive, the field's active descendant resolves to the chosen action's button$| page-follow\.spec\.mjs KB-45: on a grid that edits, its Keyboard Field, the arrows bring the Focus into the window at the nearest edge, and a cell in the window moves nothing \(ADR-0204\)$| page-follow\.spec\.mjs KB-45: on a display-only grid, its root, the arrows bring the Focus into the window at the nearest edge, and a cell in the window moves nothing \(ADR-0204\)$| page-follow\.spec\.mjs KB-45: while extending, the page follows the Extent, and the Focus may be left behind \(ADR-0204, ADR-0052\)$| page-follow\.spec\.mjs KB-45: extending past the grid's own Viewport still brings the Extent into the window, the Focus left off the painted rows \(ADR-0204, ADR-0052\)$| page-follow\.spec\.mjs KB-45: a resize is not followed — the page stays where it is when the window shrinks over the Focus \(ADR-0204\)$| page-follow\.spec\.mjs SRV-5\/KB-45: a column menu's opening focus on its way does not take the keyboard from a grid pressed meanwhile \(ADR-0204, builtin Chrome\)$| page-follow\.spec\.mjs KB-23\/ADR-0204: with nothing pressed meanwhile, Space still gives a Template cell's control the keyboard, through the grid$| popovers\.spec\.mjs under the builtin Chrome a secondary click outside the selection moves it first \(CTX-1, ADR-0036\)$| popovers\.spec\.mjs under the builtin Chrome every popover takes the keyboard when it opens, by pointer or by key \(KB-29, ADR-0039\)$| popovers\.spec\.mjs under the builtin Chrome however a popover closes, the keyboard is back on the grid and the arrows move the Focus \(KB-32, ADR-0039\)$| popovers\.spec\.mjs under the builtin Chrome keys typed while Escape closes a menu are the grid's, in order: ↓, ↓ typed at once move the Focus \(ADR-0044, 2026-10-12\)$| popovers\.spec\.mjs under the builtin Chrome a key typed while a command pressed in a menu closes it is the grid's: ↓ typed at once moves the Focus \(ADR-0044, 2026-10-12\)$| popovers\.spec\.mjs under the builtin Chrome in a menu the arrows, Home and End move among the enabled items and wrap \(KB-30, ADR-0039\)$| popovers\.spec\.mjs under the builtin Chrome over a filter, Tab on a command moves to the search box — nothing runs, nothing closes \(FL-12, KB-30, ADR-0044\)$| popovers\.spec\.mjs under the builtin Chrome over a filter, Shift\+Tab on a command moves to Cancel — nothing runs, nothing closes \(FL-12, KB-30, ADR-0044\)$| popovers\.spec\.mjs under the builtin Chrome the Context Menu answers the same keys, and Tab closes it \(KB-30, ADR-0039\)$| popovers\.spec\.mjs under the builtin Chrome on the value list the letters act too: E goes back to the search box, O sorts \(FL-15, ADR-0044\)$| popovers\.spec\.mjs under the builtin Chrome where no value list stands, E goes to the condition's value, where a search is typed \(FL-15, ADR-0044\)$| popovers\.spec\.mjs under the builtin Chrome E on a checkbox outside the value list holds nothing: a Space after it ticks at once \(ADR-0010\/0044\)$| popovers\.spec\.mjs under the builtin Chrome Enter in the value field applies exactly what OK applies \(KB-31, ADR-0039\)$| popovers\.spec\.mjs under the builtin Chrome with nothing ticked the value filter is not applied: Apply shows it is unavailable, and neither Apply nor Enter applies it \(ADR-0009\/0023, WR-2, ticket 76\)$| popovers\.spec\.mjs under the builtin Chrome a pointer-down elsewhere in the instance dismisses a popover and keeps its own meaning \(KB-17, ADR-0010\/0039\)$| presentation\.spec\.mjs geometry tokens are inline and read-only: an ancestor or stylesheet cannot move them \(UX-2\)$| presentation\.spec\.mjs a Visual Token set on an ancestor recolours the paint \(UX-5, the paint half\)$| presentation\.spec\.mjs nothing inside the Viewport transitions or animates \(UX-6\)$| presentation\.spec\.mjs UX-7 \/ ADR-0027, ADR-0029: under forced colors every Cell State and Row Kind stays tellable without its background, on \/cells$| presentation\.spec\.mjs under a dark scheme the untouched grid stays readable \(UX-8\)$| presentation\.spec\.mjs inside an RTL ancestor the grid stays an LTR island \(DIR-2\)$| presentation\.spec\.mjs DIR-3 \/ ADR-0031: an Arabic value reads right to left inside its cell, and the cell does not move, under an RTL page$| presentation\.spec\.mjs a Header Group label is centred, and nothing aligns it vertically \(HG-13, ADR-0032\)$| presentation\.spec\.mjs the editor's box is exactly the cell's \(ED-9\)$| presentation\.spec\.mjs the pointer leaving the grid stops the auto-scroll \(SL-14\/SL-15\)$| presentation\.spec\.mjs a drag that leaves the grid before its press is answered stops the auto-scroll at its turn \(SL-14, ADR-0240\)$| features\.spec\.mjs a flagged cell says why when the pointer rests on it \(ED-17b, ADR-0021\/0034\)$| features\.spec\.mjs a pasted value wears the same mark as a typed one \(ADR-0034\)$| features\.spec\.mjs DC-63: a double click where no edit opens is heard once with its cell and the Focus placed there; an Editable cell opens its edit, an open edit and a control in an Action, Template or Mark cell raise nothing \(ADR-0063\)$| find\.spec\.mjs a grid with nothing to search takes Ctrl\+F and opens nothing \(FD-1\/FD-2, ADR-0055\)$| gestures\.spec\.mjs pasting onto an off-screen selection works, and the indicator showed first \(PST-3\)$| page-follow\.spec\.mjs KB-45: the editor opens in the window, at the nearest edge, on a cell scrolled out of it \(ADR-0204\)$| page-follow\.spec\.mjs SRV-5\/KB-45: a Template cell's request for the keyboard on its way does not take it from a grid pressed meanwhile \(ADR-0204\)$| write-lands\.spec\.mjs | features\.spec\.mjs SL-11: a drag held against either boundary of a page does not turn it, and the Selection stops at the page \(ADR-0015\)$| grid-live-local\.spec\.mjs ADR-0141\/D8, LV-9: | grid-live-local\.spec\.mjs ADR-0141 LV-5\/LV-7: /;

const hostServers = reactHost !== null
    ? [reactHost]
    : SERVER
    ? [
        {
            ...(PUBLISHED
                ? {
                    command: `dotnet ExGrid.DemoHost.Server.dll --urls ${HOST_URL}`,
                    cwd: published('server'),
                    env: { ...development, EXGRID_HOST_LOG: HOST_LOG },
                }
                : {
                    command: `dotnet run --project ../../samples/ExGrid.DemoHost.Server --urls ${HOST_URL}`,
                    env: { EXGRID_HOST_LOG: HOST_LOG },
                }),
            url: `${HOST_URL}/wide`,
            reuseExistingServer: true,
            timeout: 180_000,
            stdout: 'pipe',
            stderr: 'pipe',
        },
        {
            // The browser reaches the Server host through this, at 0 ms until a test
            // sets a round trip (ED-22, SRV-5, SRV-6).
            command: `node latency-proxy.mjs ${new URL(BASE_URL).port} ${HOST_PORT} ${new URL(LATENCY_CONTROL_URL).port}`,
            url: `${BASE_URL}/wide`,
            reuseExistingServer: true,
            timeout: 180_000,
        },
    ]
    : [
        {
            command: PUBLISHED
                ? `node static-host.mjs ${JSON.stringify(path.join(published('wasm'), 'wwwroot'))} ${HOST_PORT}`
                : `dotnet run --project ../../samples/ExGrid.DemoHost --urls ${HOST_URL}`,
            url: `${BASE_URL}/wide`,
            reuseExistingServer: true,
            timeout: 180_000,
            stdout: 'pipe',
            stderr: 'pipe',
        },
    ];

// The demo API server (ADR-0069), beside either host: the pages on both call it over HTTP,
// at their own port plus 3000 (hosting.mjs). Its first start for a count generates the
// trades into a file outside the repository, which every later start reuses; the run asks
// for 20,000, about a second's work, unless EXGRID_DEMO_TRADES asks for another count.
// /api/status answers 503 until the trades are ready, so the run waits for the data, not
// only for the port. A reused server keeps the count it was started with, so no test
// assumes one. Stopped with SIGTERM rather than killed, so it deletes the copy of the trades
// it served (a killed one's copy is removed by the next start).
const apiServer = {
    name: 'DemoApi',
    ...(PUBLISHED
        ? {
            command: `dotnet ExGrid.DemoApi.dll --urls ${API_URL}`,
            cwd: published('api'),
            env: { ...development, EXGRID_DEMO_TRADES: process.env.EXGRID_DEMO_TRADES ?? '20000' },
        }
        : {
            command: `dotnet run --project ../../samples/ExGrid.DemoApi --urls ${API_URL}`,
            env: { EXGRID_DEMO_TRADES: process.env.EXGRID_DEMO_TRADES ?? '20000' },
        }),
    url: `${API_URL}/api/status`,
    reuseExistingServer: true,
    timeout: 300_000,
    gracefulShutdown: { signal: 'SIGTERM', timeout: 5_000 },
    stdout: 'pipe',
    stderr: 'pipe',
};

const webServer = [...hostServers, apiServer];

export default defineConfig({
    testDir: '.',
    ...(REACT ? { grep: REACT_SUITE } : {}),
    // A wait's bound tells a hang, never a speed (ADR-0056, rule 5): performance never gates, so a
    // test fails on what the app did, never on how long it took. Playwright's defaults, 5 s for a
    // wait and 30 s for a test, failed a correct pivot laid out again on a Debug build of the hosts.
    // A test still waits for the state it expects, and reads the moment it holds.
    timeout: 300_000,
    expect: { timeout: 60_000 },
    // One worker, because a run shares what one machine has only one of: the OS clipboard
    // (one per display, and these browsers are headed), the records fixtures.mjs
    // read-modify-writes, and on the Server host the latency proxy's round trip and the
    // host's log. Not the zoom, which this comment used to give: the device scale is
    // emulated per page, and each worker has a browser of its own (ADR-0056). More at once
    // is done across machines — CI's shards (ADR-0041) — where none of it is shared, and a
    // shard is whole spec files, so a file's booted app is never split.
    workers: 1,
    fullyParallel: false,
    reporter: [['list']],
    // A failing layout assertion is a real answer about this platform, not a flake.
    // Retrying would turn "the scrollbar covers the Focus here" into an intermittent
    // report, which is the opposite of what this suite is for.
    retries: 0,
    // Both target browsers, because ADR-0017 says both are verified: "Passing on one
    // does not satisfy the requirement." Each channel resolves a browser that is
    // already on the machine rather than one downloaded into the repository — what
    // this suite asks about is what the real browser on the real OS does, so a
    // bundled build would be answering for the wrong one. A machine without Edge
    // fails that project by name, which is the honest outcome (ADR-0026).
    projects: [
        { name: 'chrome', use: { channel: 'chrome' } },
        // Edge opens its Downloads hub when a download ends, and from Edge 155 a page under that
        // hub never finishes closing: every test that saved a file (ADR-0282) timed out tearing its
        // page down, after its assertions had passed. The hub is the browser's own window furniture,
        // not the page's behaviour, so it is turned off; the download itself still runs.
        { name: 'msedge', use: { channel: 'msedge', launchOptions: { args: ['--disable-features=msDownloadsHub'] } } },
        // The Layout Ceiling at 150% (ADR-0053, VZ-15): Chrome with the display scale set
        // the way the OS sets it, on the command line, and the viewport left to the window.
        // Playwright's deviceScaleFactor does NOT reproduce this — it raises
        // devicePixelRatio and leaves the clamp where it was, which is why the suite was
        // green while a Windows desktop at 150% could not reach the last 200,000 rows of
        // /wide. Only the tests the clamp broke run here: the far corner and both ends of a
        // million rows, the Focus against the scrollbars, and the Sheet's extent. And every
        // test that scrolls a compressed grid to a row (scrollRowToTop in fixtures.mjs): a
        // scrollTop of rows × row height showed a later row here, which only a run at 150%
        // could catch (verification/2026-09-28-windows-3). And a far reveal, which paints
        // the slice at its target offset through that same compressed mapping (ADR-0012).
        // And the Border lines (DC-59), whose pixels at 150% are Excel's case 9 at that zoom: a
        // line's widths and dashes are device pixels, which only a real display scale lays out.
        // And SH-41's sweep of the bold edge (VZ-19): at 150% a Device Pixel is two thirds of a CSS
        // pixel, which is what a column's edges round to and what a number may use of its padding.
        // And the Focus among several ranges (UX-19, ADR-0008 of 2026-10-12): ExGrid's own on /wide,
        // on the Paper beside R4's three selections, and under ExGrid.MudBlazor on /mud. A border of
        // one Device Pixel inside its cell, which only a real display scale lays out, and a
        // screenshot reads, as an OS does. And the Heading Mark (UX-21, ADR-0008 of 2026-10-13):
        // a line of two Device Pixels on a Heading's edge, ExGrid's own on /wide and on the Sheet's
        // Headings under both Chromes.
        {
            name: 'chrome-150',
            grep: /BIG-1\b|CH-10|BIG-5|VZ-15|never behind a scrollbar|VZ-14|SH-2:|SH-18\/DC-7|SH-18\/DC-2\/DC-3|MK-6|item 3:|item 5:|active cell, cases 7 and 8|far reveal|DC-59|VZ-19|UX-19\/SH-39|UX-19\/SH-54|UX-19\/UX-9|UX-19 at the display's scale|UX-21 at the display's scale|UX-21 D2 at the display's scale|UX-21 \([^)]*\): on the Sheet/,
            use: {
                channel: 'chrome',
                viewport: null,
                launchOptions: { args: ['--force-device-scale-factor=1.5', '--window-size=1280,800'] },
            },
        },
    ],
    use: {
        // Headed by default, which is not a preference. Measured on macOS 15 / Chrome:
        // headless keeps overlay scrollbars on the horizontal axis whatever CSS asks
        // for, so the row band's 15px could not be reproduced there at all and the
        // scrollbar tests would pass without testing anything. Headed reports 15/15.
        //
        // On Windows and Linux the native scrollbars already occupy layout and nothing
        // has to be forced, so EXGRID_HEADLESS=1 is available for a machine without a
        // display. Do not make it the default: it would quietly disarm the suite on the
        // one platform where the bug is invisible to begin with.
        headless: process.env.EXGRID_HEADLESS === '1',
        baseURL: BASE_URL,
        trace: 'retain-on-failure',
    },
    webServer,
});
