import { test, expect } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';

// Each package asks for the files it was built with (ADR-0123, rewritten 2026-10-11). What only a
// browser shows, on the DemoHost under both hosts:
//
// - ERR-9: the address each file is asked for at. A Blazor Web App on MapStaticAssets (the Server
//   host) gets the fingerprinted file; a standalone WebAssembly application (the WebAssembly host)
//   the plain path with the file's content hash as ?v=. And where a standalone application puts
//   the stylesheet components: root components in the head, which a page's own HeadContent does
//   not displace. A deploy over a cache is freshness.spec.mjs's.
// - ERR-10: a script that is not the one the grid was built with — another version's, or one older
//   than the stamp — is refused before anything attaches, said in place of the rows and once in the
//   live region, and the page says nothing in its console. A missing script is layer 2's: a missing
//   file is a console error of the browser's own.
// - ERR-11: each of the six packages' stylesheet notice — a Wrapper's rendered for the stylesheet its
//   Chrome brings — hidden by its own stylesheet, shown by another version's, and not shown while no
//   stylesheet has arrived.
//
// Every test here serves a file the host does not have, or watches a document from its start, so
// each has a document of its own (ADR-0056).
test.use({ freshDocument: true });

const SCRIPT_REFUSED = 'This page holds another version of the grid\'s script. Reload the page.';
const STYLESHEET_NOTICE = 'This page holds another version\'s stylesheet. Reload the page.';
const ANOTHER_STAMP = '0'.repeat(64);

// The grid's script, as either host serves it: fingerprinted, or with ?v=. Never its source map.
const SCRIPT = /\/_content\/ExGrid\/ex-grid\.min(\.[a-z0-9]+)?\.js(\?v=[0-9a-f]{12})?$/;
const stylesheetOf = (pkg, name) => new RegExp(`/_content/${pkg.replace('.', '\\.')}/${name}\\.min(\\.[a-z0-9]+)?\\.css(\\?v=[0-9a-f]{12})?$`);

/** Serves what `pattern` matches changed by `change`: the host's response, its body decoded and
 * changed, sent as it is with the host's other headers. */
async function serveChanged(page, pattern, change) {
    await page.route(pattern, async (route) => {
        const response = await route.fetch();
        const headers = { ...response.headers() };
        delete headers['content-encoding'];
        delete headers['content-length'];
        await route.fulfill({ status: response.status(), headers, body: change(await response.text()) });
    });
}

/** Serves the grid's script changed by `change`, as a page holding another build of it would. */
const serveScript = (page, change) => serveChanged(page, SCRIPT, change);

/** Serves a stylesheet changed by `change`. */
const serveStylesheet = serveChanged;

/** The module's export list, as esbuild writes it: `export{a as attach,b as stamp}`. */
const EXPORTS = /export\{([^}]*)\};?/;

/** The script with its `attach` counted, so a test can say nothing attached. */
function countingAttaches(body) {
    const [statement, list] = EXPORTS.exec(body);
    const entries = list.split(',');
    const local = entries.find((entry) => / as attach$/.test(entry)).split(' as ')[0];
    const counted = 'function __exCountedAttach(...a){globalThis.__exAttaches=(globalThis.__exAttaches??0)+1;'
        + `return ${local}(...a)}`;
    const exports = entries.map((entry) => (/ as attach$/.test(entry) ? '__exCountedAttach as attach' : entry));
    return body.replace(statement, `${counted}export{${exports.join(',')}};`);
}

/** Another version's script: the same module answering another stamp. */
const anotherVersion = (body) => {
    const changed = countingAttaches(body).replace(/return"[0-9a-f]{64}"/, `return"${ANOTHER_STAMP}"`);
    expect(changed, 'the shipped script answers a stamp to replace').not.toBe(countingAttaches(body));
    return changed;
};

/** A script older than the stamp: the module without its `stamp` export. */
const olderThanTheStamp = (body) => {
    const counted = countingAttaches(body);
    const [statement, list] = EXPORTS.exec(counted);
    const kept = list.split(',').filter((entry) => !/ as stamp$/.test(entry));
    expect(kept.length, 'the shipped script exports a stamp to take away').toBe(list.split(',').length - 1);
    return counted.replace(statement, `export{${kept.join(',')}};`);
};

/** Records every text each grid's live region is given, from the document's start. */
async function recordAnnouncements(page) {
    await page.addInitScript(() => {
        window.__exAnnounced = [];
        new MutationObserver(() => {
            document.querySelectorAll('.ex-announce').forEach((region, i) => {
                const text = region.textContent;
                if (text && region.__exLast !== text) {
                    window.__exAnnounced.push({ grid: i, text });
                }
                region.__exLast = text;
            });
        }).observe(document, { subtree: true, childList: true, characterData: true });
    });
}

test.describe('ERR-9: the address of each file', () => {
    test(`ERR-9 / ADR-0123: the script and the stylesheets are asked for at ${SERVER ? 'the fingerprinted address' : 'the plain path with the file\'s version'}`, async ({ page }) => {
        await page.goto('/sheet');
        await expect(page.locator('.ex-grid').first().locator('.ex-row').first()).toBeVisible();

        const links = await page.evaluate(() => [...document.querySelectorAll('head link[rel=stylesheet]')]
            .map((link) => link.getAttribute('href')).filter((href) => /_content\/Ex(Grid|Sheet)\//.test(href)));
        const scripts = await page.evaluate(() => performance.getEntriesByType('resource')
            .map((entry) => new URL(entry.name).pathname + new URL(entry.name).search)
            .filter((path) => /^\/_content\/ExGrid\/ex-grid\.min[^/]*\.js(\?|$)/.test(path)));

        if (SERVER) {
            // A Blazor Web App on MapStaticAssets: the host's fingerprinted files, served immutable.
            expect(links).toEqual([
                expect.stringMatching(/^_content\/ExGrid\/ex-grid\.min\.[a-z0-9]+\.css$/),
                expect.stringMatching(/^_content\/ExSheet\/ex-sheet\.min\.[a-z0-9]+\.css$/),
            ]);
            expect(scripts).toEqual([expect.stringMatching(/^\/_content\/ExGrid\/ex-grid\.min\.[a-z0-9]+\.js$/)]);
        } else {
            // A standalone application: the plain path, with the file's content hash.
            expect(links).toEqual([
                expect.stringMatching(/^_content\/ExGrid\/ex-grid\.min\.css\?v=[0-9a-f]{12}$/),
                expect.stringMatching(/^_content\/ExSheet\/ex-sheet\.min\.css\?v=[0-9a-f]{12}$/),
            ]);
            expect(scripts).toEqual([expect.stringMatching(/^\/_content\/ExGrid\/ex-grid\.min\.js\?v=[0-9a-f]{12}$/)]);
        }
        // Whichever the address, it is the file the package ships: its stylesheet's notice is hidden.
        await expect(page.locator('.ex-grid > [data-ex-stylesheet="ex-grid.css"]').first()).toBeHidden();
    });

    test('ERR-9 / ADR-0123: a page with a HeadContent of its own keeps the packages\' stylesheets, and adds its own', async ({ page }) => {
        // /pivot puts ExPivot's stylesheet, and its page's own head, through a HeadContent; on the
        // WebAssembly host ExGrid's and ExSheet's are root components, on the Server host App.razor's.
        const count = (pkg) => page.evaluate((p) => document.querySelectorAll(`head link[rel=stylesheet][href*="_content/${p}/"]`).length, pkg);
        await page.goto('/pivot');
        await expect(page.locator('.ex-pivot .ex-grid').first().locator('.ex-row').first()).toBeVisible();
        expect([await count('ExGrid'), await count('ExSheet'), await count('ExPivot')]).toEqual([1, 1, 1]);

        // Leaving the page takes its HeadContent with it, and nothing of the packages'.
        await page.evaluate(() => window.Blazor.navigateTo('/wide'));
        await page.waitForURL(/\/wide$/);
        await expect(page.locator('head link[rel=stylesheet][href*="_content/ExPivot/"]')).toHaveCount(0);
        expect([await count('ExGrid'), await count('ExSheet')]).toEqual([1, 1]);
        await expect(page.locator('.ex-grid > [data-ex-stylesheet="ex-grid.css"]').first()).toBeHidden();
    });
});

test.describe('ERR-10: a script that is not the one the grid was built with', () => {
    for (const [what, change] of [['another version\'s script', anotherVersion], ['a script older than the stamp', olderThanTheStamp]]) {
        test(`ERR-10 / ADR-0123: ${what} is refused before anything attaches, said in place of the rows and once in the live region`, async ({ page }) => {
            await recordAnnouncements(page);
            await serveScript(page, change);

            await page.goto('/wide');

            const grid = page.locator('.ex-grid').first();
            const refusal = grid.locator('.ex-viewport > [data-ex-notice="script"]');
            await expect(refusal).toBeVisible();
            await expect(refusal).toHaveText(SCRIPT_REFUSED);
            await expect(grid.locator('.ex-announce')).toHaveText(SCRIPT_REFUSED);
            // Nothing attached: the module's attach never ran. The grid is not busy, since nothing it
            // waits for will come, and takes no tab stop, since nothing would hear a key.
            expect(await page.evaluate(() => globalThis.__exAttaches ?? 0)).toBe(0);
            await expect(grid).not.toHaveAttribute('aria-busy');
            await expect(grid).not.toHaveAttribute('tabindex');
            await expect(grid).not.toHaveClass(/\bex-loading\b/);
            await expect(grid.locator('.ex-row')).toHaveCount(0);
            // Said once per grid, however long the page stands.
            await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 500)));
            const said = await page.evaluate(() => window.__exAnnounced);
            const grids = await page.locator('.ex-grid').count();
            expect(said.filter((s) => s.text === SCRIPT_REFUSED)).toHaveLength(grids);
        });
    }

    test('ERR-10 / ADR-0123: the script the grid was built with attaches, and nothing is refused', async ({ page }) => {
        await serveScript(page, countingAttaches);

        await page.goto('/wide');

        const grid = page.locator('.ex-grid').first();
        await expect(grid).toHaveAttribute('tabindex', /^-?\d+$/);
        expect(await page.evaluate(() => globalThis.__exAttaches)).toBe(await page.locator('.ex-grid').count());
        await expect(page.locator('[data-ex-notice="script"]')).toHaveCount(0);
        await expect(grid.locator('.ex-row').first()).toBeVisible();
    });
});

test.describe('ERR-11: each product\'s stylesheet notice', () => {
    // The three products' own notices stand in their roots. A Wrapper has no root of its own
    // (ADR-0062): its Chrome declares the stylesheet it brings, and the root of what it dresses renders
    // the notice — the grid's for ExGrid.MudBlazor and for ExSheet.MudBlazor, whose Chrome is the
    // Sheet's grid's, and ExPivot's for ExPivot.MudBlazor (ADR-0123, 2026-10-11).
    const products = [
        { product: 'ExGrid', path: '/wide', root: '.ex-grid', name: 'ex-grid' },
        { product: 'ExSheet', path: '/sheet', root: '.ex-sheet', name: 'ex-sheet' },
        { product: 'ExPivot', path: '/pivot', root: '.ex-pivot', name: 'ex-pivot' },
        { product: 'ExGrid.MudBlazor', path: '/mud', root: '.ex-grid', name: 'mud-ex-grid' },
        { product: 'ExSheet.MudBlazor', path: '/sheet?chrome=mud', root: '.ex-sheet .ex-grid', name: 'mud-ex-sheet' },
        { product: 'ExPivot.MudBlazor', path: '/pivot?chrome=mud', root: '.ex-pivot', name: 'mud-ex-pivot' },
    ];

    for (const { product, path, root, name } of products) {
        const notice = (page) => page.locator(`${root} > [data-ex-stylesheet="${name}.css"]`).first();

        test(`ERR-11 / ADR-0123: ${product}'s own stylesheet hides its notice`, async ({ page }) => {
            await page.goto(path);

            await expect(notice(page)).toBeAttached();
            await expect(notice(page)).toBeHidden();
            // Hidden by the stylesheet's own rule, not only by the inline display it starts with.
            expect(await notice(page).evaluate((element) => {
                element.style.removeProperty('display');
                const display = getComputedStyle(element).display;
                element.style.display = 'none';
                return display;
            })).toBe('none');
        });

        test(`ERR-11 / ADR-0123: another version's stylesheet shows ${product}'s notice, with no script`, async ({ page }) => {
            await serveStylesheet(page, stylesheetOf(product, name),
                (css) => css.replace(new RegExp(`(\\[data-ex-stylesheet="${name}\\.css"\\]\\[data-ex-stamp=")[0-9a-f]{64}`), `$1${ANOTHER_STAMP}`));

            await page.goto(path);

            await expect(notice(page)).toBeVisible();
            await expect(notice(page)).toHaveText(STYLESHEET_NOTICE);
            // The stylesheet's check is the stylesheet's alone: nothing is said in a live region.
            await expect(page.locator('.ex-announce', { hasText: STYLESHEET_NOTICE })).toHaveCount(0);
        });
    }

    test('ERR-11 / ADR-0123: while the stylesheet has not arrived, the notice shows nothing', async ({ page }) => {
        // In a Blazor Web App the stylesheet is in the head the server renders, and nothing runs before
        // it arrives. A standalone application links it once it has started, so the grid can stand
        // before its stylesheet has arrived: the cold load's unstyled moment, without a notice.
        test.skip(SERVER, 'a Blazor Web App\'s stylesheet arrives before anything is drawn');
        let arrive;
        const arrived = new Promise((resolve) => { arrive = resolve; });
        await page.route(stylesheetOf('ExGrid', 'ex-grid'), async (route) => {
            await arrived;
            await route.continue();
        });

        await page.goto('/wide', { waitUntil: 'commit' });

        const grid = page.locator('.ex-grid').first();
        await expect(grid.locator('.ex-row').first()).toBeAttached();
        // No stylesheet yet: the grid stands unstyled (its root is not positioned), and the notice is
        // not shown.
        expect(await grid.evaluate((element) => getComputedStyle(element).position)).toBe('static');
        await expect(grid.locator(':scope > [data-ex-stylesheet="ex-grid.css"]')).toBeHidden();

        arrive();
        await expect.poll(() => grid.evaluate((element) => getComputedStyle(element).position)).toBe('relative');
        await expect(grid.locator(':scope > [data-ex-stylesheet="ex-grid.css"]')).toBeHidden();
    });
});
