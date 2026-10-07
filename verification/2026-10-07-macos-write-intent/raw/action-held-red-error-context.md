# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: write-intent.spec.mjs >> LV-12: an Action held across a keyed row move keeps its original target and fires once (ADR-0154)
- Location: write-intent.spec.mjs:168:1

# Error details

```
Error: expect(locator).toHaveText(expected) failed

Locator:  locator('#action-status')
Expected: "Action: approve Alpha/Ishikawa at 1000000.00 (×1)"
Received: "Action: —"
Timeout:  5000ms

Call log:
  - Expect "toHaveText" with timeout 5000ms
  - waiting for locator('#action-status')
    14 × locator resolved to <span id="action-status">Action: —</span>
       - unexpected value "Action: —"

```

```yaml
- text: "Action: —"
```

# Test source

```ts
  102 |     await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
  103 |     await expect(notional).toHaveText('1500000');
  104 | });
  105 |
  106 | test('LV-11: Escape after an upstream change cancels the typing without writing (ADR-0154)', async ({ page }) => {
  107 |     const notional = cell(page, 0, NOTIONAL);
  108 |     await clickCell(page, 0, NOTIONAL);
  109 |     await expectActiveDescendant(grid(page), /r0c2$/);
  110 |     await setRoundTrip(150);
  111 |
  112 |     await page.keyboard.type('42');
  113 |     await page.keyboard.press('F9');
  114 |     await expect(page.locator('#upstream-status')).toContainText('(×1)');
  115 |     const moved = (await notional.textContent()).trim();
  116 |
  117 |     await page.keyboard.press('Escape');
  118 |     await expect(grid(page).locator('input.ex-editor')).toHaveCount(0);
  119 |     await circuitQuiet();
  120 |     await expect(page.locator('#edit-status')).toHaveText('Edited: —');
  121 |     await expect(notional).toHaveText(moved);
  122 | });
  123 |
  124 | test('LV-11: editing another cell preserves the upstream value on the same row (ADR-0154)', async ({ page }) => {
  125 |     const before = Number((await cell(page, 0, NOTIONAL).textContent()).trim());
  126 |     await clickCell(page, 0, 1);                       // Trader, editable; F9 moves Notional
  127 |     await expectActiveDescendant(grid(page), /r0c1$/);
  128 |     await setRoundTrip(150);
  129 |
  130 |     await page.keyboard.type('Osei');
  131 |     await page.keyboard.press('F9');
  132 |     await page.keyboard.press('Enter');
  133 |
  134 |     await expect(page.locator('#edit-status')).toContainText('Trader=Osei');
  135 |     await expect(cell(page, 0, 1)).toHaveText('Osei');
  136 |     await circuitQuiet();
  137 |     await expect(cell(page, 0, NOTIONAL)).toHaveText((before + 1).toFixed(2));
  138 | });
  139 |
  140 | test('LV-12: an Action reaches the current same row exactly once after an upstream change (ADR-0154, ADR-0020)', async ({ page }) => {
  141 |     const notional = cell(page, 0, NOTIONAL);
  142 |     const before = (await notional.textContent()).trim();
  143 |     const trader = (await cell(page, 0, 1).textContent()).trim();
  144 |     let button = await boxOf(cell(page, 0, ACT).locator('.ex-action'));
  145 |     await clickCell(page, 6, 0);
  146 |     await expectActiveDescendant(grid(page), /r6c0$/);
  147 |     await circuitQuiet();
  148 |     await watchWhatIsSeen(page, 0, NOTIONAL);
  149 |     await setRoundTrip(150);
  150 |
  151 |     await page.keyboard.press('F9');
  152 |     if (!SERVER) {
  153 |         button = await boxOf(cell(page, 0, ACT).locator('.ex-action'));
  154 |     }
  155 |     await page.mouse.click(button.x + button.width / 2, button.y + button.height / 2);
  156 |
  157 |     if (SERVER) {
  158 |         expect((await seenAt(page, 'mousedown')).text, 'the press preceded F9’s paint').toBe(before);
  159 |     }
  160 |     await expect(page.locator('#upstream-status')).toContainText('(×1)');
  161 |     await expect(page.locator('#action-status')).toHaveText(
  162 |         `Action: approve Alpha/${trader} at ${(Number(before) + 1).toFixed(2)} (×1)`);
  163 |     await circuitQuiet();
  164 |     await expect(page.locator('#action-status')).toContainText('(×1)');
  165 |     await expect(page.locator('#action-refused-status')).toHaveText('Action refused: —');
  166 | });
  167 |
  168 | test('LV-12: an Action held across a keyed row move keeps its original target and fires once (ADR-0154)', async ({ page }) => {
  169 |     await page.goto('/features?upstream=1&upstreamReorder=1');
  170 |     await expect(grid(page).locator('.ex-row').first()).toBeVisible();
  171 |     await expectTabStopTaken(grid(page));
  172 |     await expect(cell(page, 0, 0)).toHaveText('Alpha');
  173 |     await expect(cell(page, 1, 0)).toHaveText('Beta');
  174 |     await clickCell(page, 6, 0);
  175 |     await expectActiveDescendant(grid(page), /r6c0$/);
  176 |     await circuitQuiet();
  177 |     await setRoundTrip(150);
  178 |     await watchWhatIsSeen(page, 0, NOTIONAL);
  179 |
  180 |     const originalButton = await cell(page, 0, ACT).locator('.ex-action').elementHandle();
  181 |     const originalBox = await boxOf(originalButton);
  182 |     await page.mouse.move(originalBox.x + originalBox.width / 2, originalBox.y + originalBox.height / 2);
  183 |     await page.mouse.down();
  184 |     try {
  185 |         // The pointer stays down while the keyed component moves; no second press can refresh
  186 |         // its original address. Waiting for the new rows makes this independent of wire timing.
  187 |         await page.keyboard.press('F10');
  188 |         await expect(page.locator('#upstream-status')).toHaveText('Upstream: first two rows swapped (×1)');
  189 |         await expect(cell(page, 0, 0)).toHaveText('Beta');
  190 |         await expect(cell(page, 1, 0)).toHaveText('Alpha');
  191 |         const movedButton = cell(page, 1, ACT).locator('.ex-action');
  192 |         expect(await movedButton.evaluate((button, original) => button === original, originalButton),
  193 |             'the original Action button moved with its keyed row').toBe(true);
  194 |         const movedBox = await boxOf(movedButton);
  195 |         await page.mouse.move(movedBox.x + movedBox.width / 2, movedBox.y + movedBox.height / 2);
  196 |     } finally {
  197 |         await page.mouse.up();
  198 |     }
  199 |
  200 |     expect(await page.evaluate(() => window.__seenAtGesture.filter(event => event.type === 'mousedown').length),
  201 |         'only the original press was made').toBe(1);
> 202 |     await expect(page.locator('#action-status')).toHaveText('Action: approve Alpha/Ishikawa at 1000000.00 (×1)');
      |                                                  ^ Error: expect(locator).toHaveText(expected) failed
  203 |     await circuitQuiet();
  204 |     await expect(page.locator('#action-status')).toHaveText('Action: approve Alpha/Ishikawa at 1000000.00 (×1)');
  205 |     await expect(page.locator('#action-refused-status')).toHaveText('Action refused: —');
  206 |     await originalButton.dispose();
  207 | });
  208 |
  209 | test('LV-13/LV-14: one Ctrl+V overwrites targets changed upstream before the paste lands (ADR-0154)', async ({ page }) => {
  210 |     await page.evaluate(() => navigator.clipboard.write([new ClipboardItem({
  211 |         'text/html': new Blob(['<table><tr><td>5</td></tr></table>'], { type: 'text/html' }),
  212 |         'text/plain': new Blob(['5\r\n'], { type: 'text/plain' }),
  213 |     })]));
  214 |     const notional = cell(page, 0, NOTIONAL);
  215 |     const before = (await notional.textContent()).trim();
  216 |     await clickCell(page, 0, NOTIONAL);
  217 |     await page.keyboard.press('Shift+ArrowDown');     // Notional, rows 0 and 1 — both moved by F9
  218 |     await circuitQuiet();
  219 |     await watchWhatIsSeen(page, 0, NOTIONAL);
  220 |     await setRoundTrip(150);
  221 |
  222 |     await page.keyboard.press('F9');
  223 |     await page.keyboard.press('ControlOrMeta+v');
  224 |
  225 |     if (SERVER) {
  226 |         expect((await seenAt(page, 'paste')).text, 'the paste preceded F9’s paint').toBe(before);
  227 |     }
  228 |     await expect(page.locator('#upstream-status')).toContainText('(×1)');
  229 |     await expect(page.locator('#paste-status')).toHaveText('Pasted: 2 cells from 1x1, 2 applied');
  230 |     await expect(notional).toHaveText('5');
  231 |     await expect(cell(page, 1, NOTIONAL)).toHaveText('5');
  232 | });
  233 |
  234 | test('LV-13: Ctrl+Enter writes the whole selection after a target changes upstream (ADR-0154, ADR-0035)', async ({ page }) => {
  235 |     // Notional rows 4 and 5, the Focus on 5 (ADR-0052): F9 moves row 4, not the edited cell.
  236 |     await clickCell(page, 5, NOTIONAL);
  237 |     await page.keyboard.press('Shift+ArrowUp');
  238 |     await page.keyboard.type('7');
  239 |     const editor = grid(page).locator('input.ex-editor');
  240 |     await expect(editor).toHaveValue('7');
  241 |     await circuitQuiet();
  242 |     const before = (await cell(page, 4, NOTIONAL).textContent()).trim();
  243 |     await watchWhatIsSeen(page, 4, NOTIONAL);
  244 |     await setRoundTrip(150);
  245 |
  246 |     await page.keyboard.press('F9');
  247 |     await page.keyboard.press('ControlOrMeta+Enter');
  248 |
  249 |     if (SERVER) {
  250 |         expect((await seenAt(page, 'keydown', 'Enter')).text, 'Ctrl+Enter was taken on the render before F9\'s change').toBe(before);
  251 |     }
  252 |     await expect(page.locator('#paste-status')).toHaveText('Pasted: 2 cells from 1x1, 2 applied');
  253 |     await expect(editor).toHaveCount(0);
  254 |     await expect(cell(page, 4, NOTIONAL)).toHaveText('7');
  255 |     await expect(cell(page, 5, NOTIONAL)).toHaveText('7');
  256 | });
  257 |
  258 | test('LV-13: a fill-handle drag copies the current source over upstream changes (ADR-0154, ADR-0050 item 5)', async ({ page }) => {
  259 |     await clickCell(page, 0, NOTIONAL);
  260 |     await circuitQuiet();
  261 |     const corner = await boxOf(cell(page, 0, NOTIONAL));
  262 |     await expect.poll(async () => {
  263 |         const box = await grid(page).locator('.ex-fill-handle').boundingBox();
  264 |         return box !== null
  265 |             && Math.abs(box.x + box.width / 2 - (corner.x + corner.width)) <= 2
  266 |             && Math.abs(box.y + box.height / 2 - (corner.y + corner.height)) <= 2;
  267 |     }).toBe(true);
  268 |     const handle = await boxOf(grid(page).locator('.ex-fill-handle'));
  269 |     const target = await boxOf(cell(page, 2, NOTIONAL));
  270 |     const before = (await cell(page, 2, NOTIONAL).textContent()).trim();
  271 |     const sourceBefore = Number((await cell(page, 0, NOTIONAL).textContent()).trim());
  272 |     await page.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
  273 |     await page.mouse.down();
  274 |     const toX = target.x + target.width / 2;
  275 |     const toY = target.y + target.height / 2;
  276 |     await page.mouse.move(toX + 3, toY, { steps: 8 });
  277 |     // The target is outlined once the drag's moves are heard (declarations.spec.mjs, fillDragHeard).
  278 |     let nudge = 0;
  279 |     await expect.poll(async () => {
  280 |         await page.mouse.move(toX + 3 + (nudge++ % 2), toY);
  281 |         return grid(page).locator('.ex-fill-target').count();
  282 |     }).toBe(1);
  283 |     await watchWhatIsSeen(page, 2, NOTIONAL);
  284 |     await setRoundTrip(150);
  285 |
  286 |     await page.keyboard.press('F9');
  287 |     await page.mouse.up();
  288 |
  289 |     if (SERVER) {
  290 |         expect((await seenAt(page, 'mouseup')).text, 'the release was taken on the render before F9\'s change').toBe(before);
  291 |     }
  292 |     await expect(page.locator('#fill-status')).toHaveText('Filled: 2 cells');
  293 |     await expect(cell(page, 1, NOTIONAL)).toHaveText((sourceBefore + 1).toFixed(2));
  294 |     await expect(cell(page, 2, NOTIONAL)).toHaveText((sourceBefore + 1).toFixed(2));
  295 | });
  296 |
  297 | // Consecutive gestures retain their order, even while earlier writes have not reached the screen.
  298 | for (const rtt of [0, 150]) {
  299 |     test(`LV-17: 5 Enter ↑ Ctrl+V typed at once pastes over the cell the commit just wrote (ADR-0154, ${rtt} ms)`, async ({ page }) => {
  300 |         test.skip(!SERVER && rtt !== 0, 'WebAssembly has no round trip to set');
  301 |         await page.evaluate(() => navigator.clipboard.write([new ClipboardItem({
  302 |             'text/html': new Blob(['<table><tr><td>7</td></tr></table>'], { type: 'text/html' }),
```
