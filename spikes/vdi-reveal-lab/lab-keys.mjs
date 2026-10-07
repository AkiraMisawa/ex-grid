// Ctrl+Down, Ctrl+Up, PageDown x3 in each mode: the Focus row must be on screen after each, with no errors.
import { createRequire } from 'node:module';
const require = createRequire('/home/akira/src/hobby/ex-grid/tests/ExGrid.Browser/');
const { chromium } = require('playwright-core');
const browser = await chromium.launch({ headless: false, channel: 'chrome' });
for (const mode of 'ABCDEFGH') {
  const page = await (await browser.newContext({ viewport: { width: 1280, height: 800 } })).newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error' && !m.text().includes('404')) errors.push(m.text()); });
  await page.goto(`${process.argv[2]}showcase/blotter?reveal=${mode}`, { waitUntil: 'load' });
  await page.waitForSelector('.ex-grid .ex-viewport .ex-row', { timeout: 60000 });
  await page.waitForTimeout(800);
  const box = await page.locator('.ex-grid .ex-scroller').boundingBox();
  await page.mouse.click(box.x + box.width * 0.6, box.y + 90);
  const results = [];
  for (const key of ['Control+ArrowDown', 'Control+ArrowUp', 'PageDown', 'PageDown', 'PageDown']) {
    await page.keyboard.press(key);
    await page.waitForTimeout(600);
    results.push(await page.evaluate((key) => {
      const root = document.querySelector('.ex-grid');
      const s = root.querySelector(':scope > .ex-scroller');
      const focus = s.querySelector('.ex-focus');
      const r = s.getBoundingClientRect(), f = focus?.getBoundingClientRect();
      const visible = !!f && f.top >= r.top && f.bottom <= r.bottom;
      return `${key.replace('Control+', '^').replace('Arrow', '')}:${visible ? 'focus-visible' : 'FOCUS-OFF'}@${Math.round(s.scrollTop)}`;
    }, key));
  }
  console.log(`mode ${mode}: ${results.join('  ')}${errors.length ? '  ERRORS ' + errors.join(' | ') : ''}`);
  await page.context().close();
}
await browser.close();
