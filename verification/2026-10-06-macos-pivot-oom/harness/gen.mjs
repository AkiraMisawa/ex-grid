// Load /pivot-live-costs at A x B and print one generation's memory breakdown.
import { createRequire } from 'node:module';
const require = createRequire('/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc/tests/ExGrid.Browser/package.json');
const { chromium } = require('playwright');
const [base, a, b] = process.argv.slice(2);
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
page.on('console', (m) => { if (m.type() === 'error') console.log('console error:', m.text().slice(0, 300)); });
await page.goto(`${base}/pivot-live-costs?a=${a}&b=${b}&batch=1&step=1`);
await page.locator('#pivot-live-costs .ex-grid .ex-viewport .ex-row').first().waitFor({ timeout: 300000 });
await page.click('#pivot-live-costs-gen');
await page.locator('#pivot-live-costs-genout').filter({ hasText: 'rows=' }).waitFor({ timeout: 300000 });
console.log(a + 'x' + b, (await page.locator('#pivot-live-costs-genout').textContent()).trim());
await browser.close();
