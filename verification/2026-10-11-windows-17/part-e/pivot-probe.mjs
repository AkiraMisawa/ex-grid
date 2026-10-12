// docs/specs/exsheet/verify-on-windows-17.md, Part E, the ExPivot half: each layout of items.json built on
// /pivot-csv from the item's own CSV, and the report read cell by cell.
//
//     node pivot-probe.mjs <base URL> <items.json> <folder for the CSVs> <out.json> [ids]
//
// The file is given to "Choose any CSV" (a file nobody has described), the suggested Schema confirmed (a
// column the item names in "kinds" set to that kind first), and the layout built through the Field List as a
// user builds it: a field dragged onto Rows or Columns, a field dragged onto Values for each Value Field, its
// Aggregation and Show Values As set in Value Field Settings…, an Item hidden through Filter…, the form chosen
// in the Layout menu. This Part asks what the report says, not how the OS's keys reach the page, so
// Playwright's own input builds the layouts (Parts C, D, F and G use real OS input). Chrome, headed, on
// Windows; the report's rows read through the DOM (every painted row, by aria-rowindex and aria-colindex),
// with the column headers, and a picture of the report and the pane.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const [BASE, ITEMS, CSVDIR, OUT, ONLY] = process.argv.slice(2);
const spec = JSON.parse(fs.readFileSync(ITEMS, 'utf8'));
const items = spec.items.filter((i) => !ONLY || ONLY.split(',').includes(i.id));
const out = { base: BASE, started: new Date().toISOString(), items: [] };
const save = () => fs.writeFileSync(OUT, JSON.stringify(out, null, 1));

const browser = await chromium.launch({ channel: process.env.CHANNEL ?? 'chrome', headless: false, args: ['--window-position=40,40', '--window-size=1700,1150'] });
out.browser = browser.version();
const context = await browser.newContext({ viewport: { width: 1600, height: 1000 } });
const page = await context.newPage();
const messages = [];
page.on('console', (m) => messages.push({ type: m.type(), text: m.text() }));
page.on('pageerror', (e) => messages.push({ type: 'pageerror', text: String(e) }));

const pivot = () => page.locator('.ex-pivot');
const report = () => pivot().locator('.ex-pivot-sheet > .ex-grid');
const pane = () => page.getByRole('region', { name: 'PivotTable Fields' });
const fieldItem = (caption) => pane().getByRole('list', { name: 'PivotTable Fields' }).getByRole('listitem').filter({ has: page.getByRole('checkbox', { name: caption, exact: true }) });
const area = (title) => pane().getByRole('list', { name: title, exact: true });
const entry = (caption) => pane().getByRole('button', { name: `Options for ${caption}`, exact: true });
const quiet = () => page.waitForTimeout(500);

async function entries(title) {
    return (await area(title).getByRole('button').evaluateAll((bs) => bs.map((b) => b.getAttribute('aria-label')).filter((n) => n?.startsWith('Options for ')))).map((n) => n.slice(12));
}
async function dropOn(caption, title) {
    await fieldItem(caption).dragTo(area(title).locator('xpath=..'), { targetPosition: { x: 12, y: 8 } });
    await quiet();
}
async function readReport() {
    if ((await report().count()) === 0) return { empty: await pivot().locator('.ex-pivot-empty').textContent().catch(() => null) };
    return report().evaluate((g) => {
        const headers = [...g.querySelectorAll('[role=columnheader]')].map((h) => ({ col: Number(h.getAttribute('aria-colindex')), text: h.textContent.trim() }));
        const rows = [...g.querySelectorAll('.ex-viewport .ex-row')].sort((a, b) => Number(a.getAttribute('aria-rowindex')) - Number(b.getAttribute('aria-rowindex')))
            .map((r) => ({ row: Number(r.getAttribute('aria-rowindex')), cells: [...r.querySelectorAll('[role=gridcell]')].sort((a, b) => Number(a.getAttribute('aria-colindex')) - Number(b.getAttribute('aria-colindex'))).map((c) => c.textContent.trim()) }));
        return { rowCount: g.getAttribute('aria-rowcount'), colCount: g.getAttribute('aria-colcount'), headers, rows };
    });
}

async function setValueField(caption, agg, show) {
    await entry(caption).click();
    await page.getByRole('menuitem', { name: 'Value Field Settings…', exact: true }).click();
    const panel = page.locator('.ex-pivot-value-settings');
    await panel.waitFor();
    const selects = panel.locator('select.ex-pivot-select');
    const offered = await selects.nth(0).locator('option').evaluateAll((os) => os.map((o) => ({ label: o.textContent, disabled: o.disabled, title: o.title })));
    const wanted = offered.find((o) => o.label === agg);
    if (agg && wanted?.disabled) {
        // Offered disabled, with its reason (ADR-0066): recorded, and the panel cancelled.
        const reasons = await panel.locator('.ex-pivot-not-offered').allTextContents();
        await panel.locator('.ex-pivot-cancel').click();
        await quiet();
        return { offered, notOffered: agg, reasons };
    }
    if (agg) await selects.nth(0).selectOption({ label: agg });
    if (show) await selects.nth(1).selectOption({ label: show });
    await quiet();
    const refusal = await panel.locator('.ex-pivot-refusal').textContent().catch(() => null);
    const sample = await panel.locator('.ex-pivot-sample').textContent().catch(() => null);
    await panel.locator('.ex-pivot-ok').click();
    await quiet();
    return { offered, refusal, sample };
}

for (const it of items) {
    const row = { id: it.id, item: it.item, what: it.what, steps: [] };
    try {
        const file = path.join(CSVDIR, `${it.id}.csv`);
        fs.writeFileSync(file, it.csv);
        await page.goto(`${BASE}/pivot-csv`);
        await page.locator('#csv-sample').waitFor();
        await page.locator('#csv-unknown-file').setInputFiles(file);
        await page.locator('#csv-suggestion').waitFor({ timeout: 30_000 });
        row.suggestion = await page.locator('#csv-suggestion').evaluate((s) => ({
            file: s.querySelector('#csv-suggestion-file')?.textContent.trim(),
            columns: [...s.querySelectorAll('tr[data-column]')].map((tr) => ({ column: tr.getAttribute('data-column'), kind: tr.querySelector('select').value, readAs: tr.children[2].textContent.trim(), examples: tr.children[3].textContent.trim(), notClear: tr.children[4].textContent.trim() })),
        }));
        for (const [col, kind] of Object.entries(it.kinds ?? {})) {
            await page.getByLabel(`Kind of ${col}`).selectOption(kind);
            row.steps.push(`kind of ${col} set to ${kind}`);
        }
        await page.locator('#csv-confirm').click();
        await pane().waitFor({ timeout: 30_000 });
        row.status = await page.locator('#csv-status').textContent();
        row.refusal = await page.locator('#csv-refusal').textContent();
        for (const f of it.rows) { await dropOn(f, 'Rows'); row.steps.push(`${f} dragged onto Rows`); }
        for (const f of it.cols) { await dropOn(f, 'Columns'); row.steps.push(`${f} dragged onto Columns`); }
        for (const v of it.values) {
            const before = await entries('Values');
            await dropOn(v.field, 'Values');
            const after = await entries('Values');
            const added = after.find((c) => !before.includes(c)) ?? after.at(-1);
            row.steps.push(`${v.field} dragged onto Values: ${added}`);
            const wantedCaption = `${v.agg} of ${v.field}`;
            if (!added.startsWith(`${v.agg} of `) || v.show) {
                const r = await setValueField(added, added.startsWith(`${v.agg} of `) ? null : v.agg, v.show);
                row.steps.push({ valueFieldSettings: added, agg: v.agg, show: v.show ?? null, ...r, now: await entries('Values') });
            }
        }
        for (const [f, hidden] of Object.entries(it.hide ?? {})) {
            const where = (await entries('Rows')).includes(f) ? f : f;
            await entry(where).click();
            await page.getByRole('menuitem', { name: 'Filter…', exact: true }).click();
            const panel = page.locator('.ex-pivot-item-filter');
            await panel.waitFor();
            for (const h of hidden) { await panel.locator('label.ex-pivot-item', { hasText: h }).locator('input').uncheck(); }
            await panel.locator('.ex-pivot-ok').click();
            await quiet();
            row.steps.push(`${hidden.join(', ')} hidden through Filter… on ${f}`);
        }
        if (it.layout) {
            const name = { compact: 'Show in Compact Form', outline: 'Show in Outline Form', tabular: 'Show in Tabular Form' }[it.layout];
            await pivot().locator('.ex-pivot-report').getByRole('button', { name: 'Layout', exact: true }).click();
            await page.getByRole('menuitemradio', { name, exact: true }).click();
            await quiet();
            row.steps.push(`Layout: ${name}`);
        }
        row.areas = { filters: await entries('Filters'), columns: await entries('Columns'), rows: await entries('Rows'), values: await entries('Values') };
        await page.waitForTimeout(800);
        row.report = await readReport();
        const shot = path.join(CSVDIR, `${it.id}-expivot.png`);
        await pivot().screenshot({ path: shot });
        row.shot = path.basename(shot);
    } catch (e) { row.error = String(e).slice(0, 600); }
    row.messages = messages.splice(0);
    out.items.push(row);
    save();
    console.log(`${it.id}: ${row.error ?? `${row.report?.rows?.length ?? 0} rows`}`);
}
out.finished = new Date().toISOString();
save();
await browser.close();
