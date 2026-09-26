import { test, expect } from './fixtures.mjs';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// The DemoHost's page index (docs/specs/row-inspectors). The pages are a sample, not the
// product, but a page nobody can reach is a demo nobody runs: the index at / lists every
// page, and every page's navigation leads back to it. The routes the router serves are
// read from the pages' own @page directives, so a page added without an entry in the
// list fails here by name.

const PAGES = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../samples/ExGrid.DemoPages/Pages');

function routedPages() {
    const routes = new Set();
    for (const file of fs.readdirSync(PAGES).filter((name) => name.endsWith('.razor'))) {
        const text = fs.readFileSync(path.join(PAGES, file), 'utf8');
        for (const match of text.matchAll(/^@page\s+"\/([^"]*)"/gm)) {
            routes.add(match[1]);
        }
    }
    routes.delete(''); // the index itself
    return [...routes].sort();
}

async function indexEntries(page) {
    await page.goto('/');
    await expect(page.locator('#demo-interactive')).toBeAttached();
    return page.locator('#demo-index a').evaluateAll((links) =>
        links.map((link) => ({ href: link.getAttribute('href'), title: link.textContent.trim() })));
}

test('RI-1: the index at / lists every routed page, once', async ({ page }) => {
    const entries = await indexEntries(page);
    const listed = entries.map((entry) => entry.href);
    expect(new Set(listed).size).toBe(listed.length);
    expect([...listed].sort()).toEqual(routedPages());
    // Every entry says what the page is for, not only what it is called.
    const described = await page.locator('#demo-index li').evaluateAll((items) =>
        items.map((item) => item.querySelector('.demo-index-description')?.textContent.trim() ?? ''));
    expect(described.length).toBe(entries.length);
    for (const text of described) {
        expect(text.length).toBeGreaterThan(20);
    }
});

test('RI-2: the Row Identity demo that was at / is at /identity', async ({ page }) => {
    await page.goto('/identity');
    await expect(page.locator('#demo-interactive')).toBeAttached();
    await expect(page.locator('#replace-row')).toBeVisible();
    await expect(page.locator('#rewrite-row')).toBeVisible();
});

test('RI-3: every page opens from the index, names itself, and leads back to it (clean console)', async ({ page }) => {
    test.setTimeout(180_000);
    const entries = await indexEntries(page);
    expect(entries.length).toBe(routedPages().length);
    for (const { href, title } of entries) {
        await page.goto(`/${href}`);
        await expect(page.locator('#demo-interactive')).toBeAttached();
        // A way back to the index. The /mud-app blotter keeps its application's own
        // drawer as its navigation; the index link is in it.
        await expect(page.locator('a[href=""]').first(), href).toBeAttached();
        if (href !== 'mud-app') {
            const nav = page.locator('nav.demo-nav');
            await expect(nav, href).toHaveCount(1);
            await expect(nav.locator('a[href=""]'), href).toHaveText('All pages');
            await expect(nav.locator('[aria-current="page"]'), href).toHaveText(title);
        }
    }
});
