import { test, expect } from './fixtures.mjs';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// ERR-9 (ADR-0123, rewritten 2026-10-11): a deploy of a new version is met with the new files on a
// normal reload, in a standalone WebAssembly application served with GitHub Pages' headers
// (Cache-Control: max-age=600, with an ETag and a Last-Modified). Measured on 2026-10-11 before the
// decision: a normal reload ran the new assembly with the old script and the old stylesheet, both
// from the memory cache, with no request. The browser does that only within a cache, so only a
// browser can show the address beat it.
//
// dotnet/tests/FreshnessApp is a standalone application as small as one grid. It is published twice, from two
// copies of dotnet/src/ExGrid that are two versions': each copy's Script Core module is bundled from a source
// that marks itself, so it answers a stamp of its own, and the build records that stamp and the module's
// version as it bundles (ADR-0350 section 7); each stylesheet hides its notice for a stamp of its own and
// marks itself, and each copy's ShippedAssets.g.cs and the Script Core's shipped-stylesheet.ts record them,
// as tools/assets/minify.mjs would.
// Nothing is built in this checkout: a running DemoHost is never rebuilt under itself. The first version
// is served and loaded, the files are replaced by the second's in place, and the page is reloaded.
//
// It drives a host of its own, so it needs neither DemoHost, and it runs the same beside either: SRV-2
// lets a test skip on the Server host only for its four named exceptions, and this is none of them.

const repository = fileURLToPath(new URL('../..', import.meta.url));
const sha256 = (text) => createHash('sha256').update(text.replace(/\r\n/g, '\n'), 'utf8').digest('hex');

/** Copies `from` to `to`, leaving out every bin/ and obj/. */
const copy = (from, to) => fs.cpSync(from, to, { recursive: true, filter: (source) => !/[\\/](bin|obj|node_modules)$/.test(source) });

/** Inserts `text` before a shipped file's source map comment. */
const beforeMapComment = (file, text) => {
    const at = file.lastIndexOf('\n//# sourceMappingURL=') >= 0 ? file.lastIndexOf('\n//# sourceMappingURL=') : file.lastIndexOf('\n/*# sourceMappingURL=');
    expect(at, 'the shipped file ends with its source map comment').toBeGreaterThan(0);
    return `${file.slice(0, at)}\n${text}${file.slice(at)}`;
};

/**
 * Publishes FreshnessApp against a copy of ExGrid whose shipped files are version `mark`'s, and
 * answers where its wwwroot is and the stamps and versions it was built with.
 */
function publish(work, mark) {
    const root = path.join(work, mark);
    for (const file of ['dotnet/Directory.Build.props', 'dotnet/global.json', 'dotnet/src/Directory.Build.props']) {
        fs.mkdirSync(path.dirname(path.join(root, file)), { recursive: true });
        fs.copyFileSync(path.join(repository, file), path.join(root, file));
    }
    // dotnet/src/Shared holds the sources each adapter compiles in, ExGrid's held calls among them (ADR-0005).
    for (const project of ['dotnet/src/ExGrid', 'dotnet/src/ExGrid.Data', 'dotnet/src/Shared', 'dotnet/tests/FreshnessApp']) {
        copy(path.join(repository, project), path.join(root, project));
    }
    // ExGrid's build bundles the Script Core from the npm workspace at the root (ADR-0350 section 7, ADR-0370
    // section 9): the sources it bundles, with their times, so the lock file reads as installed, and this checkout's
    // installed packages.
    for (const part of ['package.json', 'package-lock.json', 'tools/scripts', 'packages/core', 'packages/data', 'packages/blazor']) {
        fs.cpSync(path.join(repository, part), path.join(root, part), {
            recursive: true, preserveTimestamps: true, filter: (source) => !/[\\/](node_modules|dist)$/.test(source),
        });
    }
    fs.symlinkSync(path.join(repository, 'node_modules'), path.join(root, 'node_modules'), 'dir');

    // This version's script: a source that says which version it is, so the module bundled from it answers a
    // stamp of its own.
    const entry = path.join(root, 'packages/blazor/src/index.ts');
    fs.appendFileSync(entry, `\n(globalThis as Record<string, unknown>)['__exFreshness'] = '${mark}';\n`);
    // The module this checkout bundled is not this version's: the copy's build bundles its own.
    for (const built of ['ex-grid-core.min.js', 'ex-grid-core.min.js.map']) fs.rmSync(path.join(root, 'dotnet/src/ExGrid/wwwroot', built), { force: true });

    // This version's stylesheet: its notice hidden for a stamp of its own, and a mark to read.
    const wwwroot = path.join(root, 'dotnet/src/ExGrid/wwwroot');
    const stylesheetFile = path.join(wwwroot, 'ex-grid.min.css');
    const shippedStylesheet = fs.readFileSync(stylesheetFile, 'utf8');
    const [, ownStylesheetStamp] = /\[data-ex-stylesheet="ex-grid\.css"\]\[data-ex-stamp="([0-9a-f]{64})"\]/.exec(shippedStylesheet);
    const stylesheetStamp = sha256(`${mark} ${ownStylesheetStamp}`);
    const stylesheet = beforeMapComment(shippedStylesheet.replace(`[data-ex-stamp="${ownStylesheetStamp}"]`, `[data-ex-stamp="${stylesheetStamp}"]`),
        `:root{--ex-freshness:${mark}}`);
    fs.writeFileSync(stylesheetFile, stylesheet);

    // And what this build of ExGrid records of them, as minify.mjs writes it.
    const recorded = {
        StylesheetVersion: sha256(stylesheet).slice(0, 12),
        StylesheetStamp: stylesheetStamp,
    };
    const shippedAssets = path.join(root, 'dotnet/src/ExGrid/ShippedAssets.g.cs');
    let source = fs.readFileSync(shippedAssets, 'utf8');
    for (const [name, value] of Object.entries(recorded)) {
        const written = source.replace(new RegExp(`(internal const string ${name} = ")[0-9a-f]+(";)`), `$1${value}$2`);
        expect(written, `ShippedAssets.g.cs records ${name}`).not.toBe(source);
        source = written;
    }
    fs.writeFileSync(shippedAssets, source);
    // The Script Core draws its notice against the stamp it is bundled with, which minify.mjs writes beside it.
    const coreStamp = path.join(root, 'packages/core/src/dom/shipped-stylesheet.ts');
    const core = fs.readFileSync(coreStamp, 'utf8');
    const stamped = core.replace(/(export const stylesheetStamp = ')[0-9a-f]{64}(';)/, `$1${stylesheetStamp}$2`);
    expect(stamped, 'shipped-stylesheet.ts records the stylesheet\'s stamp').not.toBe(core);
    fs.writeFileSync(coreStamp, stamped);

    // A Debug build, uncompressed: nothing here is measured, and the minutes a Release publish
    // takes buy nothing the browser's cache can tell apart.
    const output = path.join(root, 'publish');
    try {
        execFileSync('dotnet', ['publish', path.join(root, 'dotnet/tests/FreshnessApp/FreshnessApp.csproj'),
            '-c', 'Debug', '-p:CompressionEnabled=false', '-o', output, '-nologo'], { stdio: 'pipe', cwd: path.join(root, 'dotnet') });
    } catch (error) {
        throw new Error(`publishing FreshnessApp as ${mark} failed:\n${error.stdout}\n${error.stderr}`);
    }
    // What the build recorded of the module it bundled, as it bundled it.
    const asset = fs.readFileSync(path.join(root, 'dotnet/src/ExGrid/obj/ScriptCore/ScriptCoreAsset.g.cs'), 'utf8');
    const constant = (name) => new RegExp(`internal const string ${name} = "([0-9a-f]+)";`).exec(asset)[1];
    return { wwwroot: path.join(output, 'wwwroot'), ScriptVersion: constant('Version'), ScriptStamp: constant('Stamp'), ...recorded };
}

const TYPES = {
    '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json',
    '.wasm': 'application/wasm', '.dat': 'application/octet-stream', '.map': 'application/json', '.pdb': 'application/octet-stream',
};

/**
 * Serves `root` as GitHub Pages serves a site: every file `Cache-Control: max-age=600`, with an ETag
 * and a Last-Modified, a conditional request answered 304. Each file is read when it is asked for,
 * so files replaced in place are a deploy. Answers its address and what was asked of it.
 */
async function servePagesLike(root) {
    const asked = [];
    const server = http.createServer((request, response) => {
        const url = new URL(request.url, 'http://localhost');
        asked.push(url.pathname + url.search);
        let file = path.join(root, decodeURIComponent(url.pathname));
        if (url.pathname.endsWith('/')) file = path.join(file, 'index.html');
        if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
            response.writeHead(404, { 'content-type': 'text/plain' });
            response.end('Not found');
            return;
        }
        const body = fs.readFileSync(file);
        const etag = `"${createHash('sha1').update(body).digest('hex').slice(0, 16)}"`;
        const headers = {
            'content-type': TYPES[path.extname(file)] ?? 'application/octet-stream',
            'cache-control': 'max-age=600',
            etag,
            'last-modified': fs.statSync(file).mtime.toUTCString(),
        };
        if (request.headers['if-none-match'] === etag) {
            response.writeHead(304, headers);
            response.end();
            return;
        }
        response.writeHead(200, { ...headers, 'content-length': body.length });
        response.end(body);
    });
    await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
    return { url: `http://127.0.0.1:${server.address().port}/`, asked, close: () => new Promise((resolve) => server.close(resolve)) };
}

/** What the page runs: the assembly's stamp, the script's and the stylesheet's marks, and the grid's state. */
async function running(page) {
    return page.evaluate(() => {
        const grid = document.querySelector('.ex-grid');
        const notice = grid.querySelector(':scope > [data-ex-stylesheet="ex-grid.css"]');
        return {
            assembly: document.querySelector('#assembly-stamp').textContent,
            script: globalThis.__exFreshness ?? 'none',
            stylesheet: getComputedStyle(document.documentElement).getPropertyValue('--ex-freshness').trim() || 'none',
            attached: grid.hasAttribute('tabindex'),
            refused: grid.querySelector('[data-ex-notice="script"]') !== null,
            noticeShown: getComputedStyle(notice).display !== 'none',
        };
    });
}

let work;
let first;
let second;

test.beforeAll(async ({ }, testInfo) => {
    testInfo.setTimeout(600_000);
    work = fs.mkdtempSync(path.join(os.tmpdir(), 'exgrid-freshness-'));
    first = publish(work, 'v1');
    second = publish(work, 'v2');
    expect(second.ScriptVersion).not.toBe(first.ScriptVersion);
    expect(second.StylesheetVersion).not.toBe(first.StylesheetVersion);
    expect(second.ScriptStamp).not.toBe(first.ScriptStamp);
});

test.afterAll(() => {
    if (work) fs.rmSync(work, { recursive: true, force: true });
});

test('ERR-9 / ADR-0123: a deploy over GitHub Pages\' headers is met with the new script and stylesheet on a normal reload @blazor', async ({ browser }) => {
    const live = path.join(work, 'live');
    fs.rmSync(live, { recursive: true, force: true });
    copy(first.wwwroot, live);
    const host = await servePagesLike(live);
    const context = await browser.newContext();
    const page = await context.newPage();
    const errors = [];
    page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()); });
    page.on('pageerror', (error) => errors.push(String(error)));
    try {
        await page.goto(host.url);
        await expect(page.locator('#assembly-stamp')).toHaveText(first.ScriptStamp);
        await expect(page.locator('.ex-grid')).toHaveAttribute('tabindex');
        expect(await running(page)).toEqual({
            assembly: first.ScriptStamp, script: 'v1', stylesheet: 'v1', attached: true, refused: false, noticeShown: false,
        });

        // The deploy: the second version's files in place of the first's, as GitHub Pages publishes.
        fs.rmSync(live, { recursive: true, force: true });
        copy(second.wwwroot, live);
        host.asked.length = 0;

        await page.reload();

        await expect(page.locator('#assembly-stamp')).toHaveText(second.ScriptStamp);
        await expect(page.locator('.ex-grid')).toHaveAttribute('tabindex');
        // The new assembly runs the new script and paints with the new stylesheet: nothing refused,
        // no notice, and no hard refresh. Each was asked for at its new address.
        expect(await running(page)).toEqual({
            assembly: second.ScriptStamp, script: 'v2', stylesheet: 'v2', attached: true, refused: false, noticeShown: false,
        });
        expect(host.asked).toContain(`/_content/ExGrid/ex-grid-core.min.js?v=${second.ScriptVersion}`);
        expect(host.asked).toContain(`/_content/ExGrid/ex-grid.min.css?v=${second.StylesheetVersion}`);
        // And the cache stood: the framework's script, which the deploy did not change, was not asked
        // for again. Within it, a plain address would have kept the first version's files.
        expect(host.asked.filter((asked) => /^\/_framework\/blazor\.webassembly\.[a-z0-9]+\.js$/.test(asked))).toEqual([]);
        expect(errors, 'zero console errors and page errors (CON-1, CON-2)').toEqual([]);
    } finally {
        await context.close();
        await host.close();
    }
});
