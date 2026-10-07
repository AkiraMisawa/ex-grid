# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: write-refusal.spec.mjs >> LV-11: one commit applies the typed value after upstream changes the edited cell (ADR-0154)
- Location: write-refusal.spec.mjs:88:1

# Error details

```
Error: expect(locator).toContainText(expected) failed

Locator: locator('#edit-status')
Expected substring: "Notional=1500000"
Received string:    "Edited: —"
Timeout: 5000ms

Call log:
  - Expect "toContainText" with timeout 5000ms
  - waiting for locator('#edit-status')
    14 × locator resolved to <span id="edit-status">Edited: —</span>
       - unexpected value "Edited: —"

```

```yaml
- text: "Edited: —"
```

# Test source

```ts
  2   | import { SERVER } from './hosting.mjs';
  3   | import { expectActiveDescendant, expectTabStopTaken } from './keyboard.mjs';
  4   |
  5   | // A write is refused when what the user saw of its target changed before it lands (ADR-0142,
  6   | // LV-11 to LV-14), driven on /features?upstream=1. There F9, declared to the grid, moves the
  7   | // Notional of the first five rows up by one, as a live feed would: it reaches the host in its
  8   | // turn among the keys and presses, so on the Server host a gesture made straight after it is
  9   | // taken on the render from before the change and handled after it. Columns: Book 0, Trader 1,
  10  | // Notional 2, Act 3 (one action, Approve), Narrow 4, …
  11  | //
  12  | // What the user saw at a gesture is read by a capture listener on the document, ahead of the
  13  | // grid's own (seenAt): the text of the cell the gesture is about, as the page showed it then. A
  14  | // race test asserts what it saw before it asserts the outcome, so a run in which the change had
  15  | // already been painted says so by name rather than passing on the wrong branch. Through the
  16  | // latency proxy at 150 ms a gesture made by the next Playwright call is taken long before the
  17  | // change's render can come back.
  18  |
  19  | function grid(page) {
  20  |     return page.locator('.ex-grid').first();
  21  | }
  22  |
  23  | const NOTIONAL = 2;
  24  | const ACT = 3;
  25  |
  26  | function cell(page, row, column) {
  27  |     return grid(page).locator(`[id$='r${row}c${column}']`);
  28  | }
  29  |
  30  | async function clickCell(page, row, column) {
  31  |     // Cells are pointer-events: none — the Viewport is the delegated target (ADR-0004).
  32  |     await cell(page, row, column).click({ force: true });
  33  | }
  34  |
  35  | async function boxOf(locator) {
  36  |     const box = await locator.boundingBox();
  37  |     expect(box).not.toBeNull();
  38  |     return box;
  39  | }
  40  |
  41  | test.beforeEach(async ({ page, context }) => {
  42  |     await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  43  |     await page.goto('/features?upstream=1');
  44  |     await expect(grid(page).locator('.ex-row').first()).toBeVisible();
  45  |     await expectTabStopTaken(grid(page));
  46  | });
  47  |
  48  | /**
  49  |  * Notes, for each keydown, mousedown, mouseup and paste from now on, the text the cell at
  50  |  * (`row`, `column`) of the first grid showed and the render its Viewport named (data-ex-paint). A
  51  |  * listener on the document in the capture phase runs before the grid's own on its root.
  52  |  */
  53  | async function watchWhatIsSeen(page, row, column) {
  54  |     await alterPage(page, ({ row, column }) => {
  55  |         window.__seenAtGesture = [];
  56  |         const note = (event) => {
  57  |             const root = document.querySelector('.ex-grid');
  58  |             const shown = root?.querySelector(`[id$='r${row}c${column}']`);
  59  |             window.__seenAtGesture.push({
  60  |                 type: event.type,
  61  |                 key: event.key ?? null,
  62  |                 text: shown ? shown.textContent : null,
  63  |                 paint: root?.querySelector('.ex-viewport')?.getAttribute('data-ex-paint') ?? null,
  64  |             });
  65  |         };
  66  |         const types = ['keydown', 'mousedown', 'mouseup', 'paste'];
  67  |         for (const type of types) {
  68  |             document.addEventListener(type, note, true);
  69  |         }
  70  |         return () => {
  71  |             for (const type of types) {
  72  |                 document.removeEventListener(type, note, true);
  73  |             }
  74  |             delete window.__seenAtGesture;
  75  |         };
  76  |     }, { row, column });
  77  | }
  78  |
  79  | /** What the watched cell showed at the last gesture of `type` (and `key`, for a keydown). */
  80  | async function seenAt(page, type, key = null) {
  81  |     const seen = await page.evaluate(() => window.__seenAtGesture);
  82  |     // A key is matched without its case: a Ctrl+V reaches the page as `v` or `V`, as the platform has it.
  83  |     const matching = seen.filter((e) => e.type === type && (key === null || e.key?.toLowerCase() === key.toLowerCase()));
  84  |     expect(matching.length, `a ${type}${key ? ` of ${key}` : ''} was seen`).toBeGreaterThan(0);
  85  |     return matching[matching.length - 1];
  86  | }
  87  |
  88  | test('LV-11: one commit applies the typed value after upstream changes the edited cell (ADR-0154)', async ({ page }) => {
  89  |     const notional = cell(page, 0, NOTIONAL);
  90  |     const before = (await notional.textContent()).trim();
  91  |     await clickCell(page, 0, NOTIONAL);
  92  |     await expectActiveDescendant(grid(page), /r0c2$/);
  93  |     await setRoundTrip(150);
  94  |
  95  |     await page.keyboard.type('1500000');
  96  |     await page.keyboard.press('F9');
  97  |     await expect(page.locator('#upstream-status')).toContainText('(×1)');
  98  |     await expect(notional).not.toHaveText(before);
  99  |     await expect(grid(page).locator('input.ex-editor')).toHaveValue('1500000');
  100 |     await page.keyboard.press('Enter');
  101 |
> 102 |     await expect(page.locator('#edit-status')).toContainText('Notional=1500000');
      |                                                ^ Error: expect(locator).toContainText(expected) failed
  103 |     await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
  104 |     await expect(notional).toHaveText('1500000');
  105 | });
  106 |
  107 | test('LV-11: Escape after a refused commit writes nothing (ADR-0142)', async ({ page }) => {
  108 |     const notional = cell(page, 0, NOTIONAL);
  109 |     await clickCell(page, 0, NOTIONAL);
  110 |     await expectActiveDescendant(grid(page), /r0c2$/);
  111 |     await setRoundTrip(150);
  112 |
  113 |     await page.keyboard.type('42');
  114 |     await page.keyboard.press('F9');
  115 |     await page.keyboard.press('Enter');
  116 |     await expect(page.locator('#commit-refused-status')).toContainText('Notional changed to');
  117 |     const moved = (await notional.textContent()).trim();
  118 |
  119 |     await page.keyboard.press('Escape');
  120 |     await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
  121 |     await circuitQuiet();
  122 |     await expect(page.locator('#edit-status')).toHaveText('Edited: —');
  123 |     await expect(notional).toHaveText(moved);
  124 | });
  125 |
  126 | test('LV-11: a change to another cell of the row refuses nothing (ADR-0142)', async ({ page }) => {
  127 |     await clickCell(page, 0, 1);                       // Trader, editable; F9 moves Notional
  128 |     await expectActiveDescendant(grid(page), /r0c1$/);
  129 |     await setRoundTrip(150);
  130 |
  131 |     await page.keyboard.type('Osei');
  132 |     await page.keyboard.press('F9');
  133 |     await page.keyboard.press('Enter');
  134 |
  135 |     await expect(page.locator('#edit-status')).toContainText('Trader=Osei');
  136 |     await expect(cell(page, 0, 1)).toHaveText('Osei');
  137 |     await circuitQuiet();
  138 |     await expect(page.locator('#commit-refused-status')).toHaveText('Commit refused: —');
  139 | });
  140 |
  141 | test('LV-12: an Action press taken on a render whose row has changed since is refused, and says why; the next press fires once (ADR-0142, ADR-0020)', async ({ page }) => {
  142 |     test.skip(!SERVER, 'WebAssembly paints F9\'s change before the next press can be taken: no press can be taken on the render before it');
  143 |     await expect(page.locator('#action-refused-status')).toHaveAttribute('role', 'status');
  144 |     const notional = cell(page, 0, NOTIONAL);
  145 |     const before = (await notional.textContent()).trim();
  146 |     const button = await boxOf(cell(page, 0, ACT).locator('.ex-action'));
  147 |     // The keyboard on the grid, away from the row pressed.
  148 |     await clickCell(page, 6, 0);
  149 |     await expectActiveDescendant(grid(page), /r6c0$/);
  150 |     await watchWhatIsSeen(page, 0, NOTIONAL);
  151 |     await setRoundTrip(150);
  152 |
  153 |     await page.keyboard.press('F9');
  154 |     await page.mouse.click(button.x + button.width / 2, button.y + button.height / 2);
  155 |
  156 |     expect((await seenAt(page, 'mousedown')).text, 'the press was taken on the render before F9\'s change').toBe(before);
  157 |     await expect(page.locator('#action-refused-status')).toContainText('RowChanged');
  158 |     await circuitQuiet();
  159 |     await expect(page.locator('#action-status')).toHaveText('Action: —');
  160 |
  161 |     // Pressed again on the row as it shows now: it fires, once. Measured again: the refusal's
  162 |     // sentence above the grid may wrap, and move the grid down.
  163 |     const moved = (await notional.textContent()).trim();
  164 |     expect(moved).not.toBe(before);
  165 |     const again = await boxOf(cell(page, 0, ACT).locator('.ex-action'));
  166 |     await page.mouse.click(again.x + again.width / 2, again.y + again.height / 2);
  167 |     await expect(page.locator('#action-status')).toContainText(`approve Alpha/`);
  168 |     await expect(page.locator('#action-status')).toContainText(moved);
  169 |     await circuitQuiet();
  170 |     await expect(page.locator('#action-refused-status')).toContainText('RowChanged');
  171 |     await expect(page.locator('#action-refused-status')).not.toContainText('×2');
  172 | });
  173 |
  174 | test('LV-13/LV-14: a Ctrl+V pressed on a render a newer one replaced before it landed is judged against the older one, and refused; pressed again, it pastes (ADR-0142)', async ({ page }) => {
  175 |     await page.evaluate(() => navigator.clipboard.write([new ClipboardItem({
  176 |         'text/html': new Blob(['<table><tr><td>5</td></tr></table>'], { type: 'text/html' }),
  177 |         'text/plain': new Blob(['5\r\n'], { type: 'text/plain' }),
  178 |     })]));
  179 |     const notional = cell(page, 0, NOTIONAL);
  180 |     const before = (await notional.textContent()).trim();
  181 |     await clickCell(page, 0, NOTIONAL);
  182 |     await page.keyboard.press('Shift+ArrowDown');     // Notional, rows 0 and 1 — both moved by F9
  183 |     await circuitQuiet();
  184 |     await watchWhatIsSeen(page, 0, NOTIONAL);
  185 |     await setRoundTrip(150);
  186 |
  187 |     await page.keyboard.press('F9');
  188 |     await page.keyboard.press('ControlOrMeta+v');
  189 |
  190 |     const atPaste = await seenAt(page, 'paste');
  191 |     await expect(page.locator('#upstream-status')).toContainText('(×1)');
  192 |     await circuitQuiet();
  193 |     if (atPaste.text === before) {
  194 |         // Taken on the render before the change, handled after it: refused, nothing written.
  195 |         await expect(page.locator('#paste-refused-status')).toContainText('TargetChanged');
  196 |         await expect(page.locator('#paste-status')).toHaveText('Pasted: —');
  197 |         expect(await grid(page).locator('.ex-viewport').first().getAttribute('data-ex-paint'),
  198 |             'a newer render replaced the one the paste was taken on').not.toBe(atPaste.paint);
  199 |     } else {
  200 |         // Taken on the render that already showed the change: the change was seen, and it pastes.
  201 |         expect(SERVER, 'on the Server host the paste is taken before the change\'s render comes back').toBe(false);
  202 |         await expect(page.locator('#paste-status')).toContainText('2 cells from 1x1');
```
