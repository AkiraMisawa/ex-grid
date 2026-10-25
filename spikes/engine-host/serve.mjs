// Serves the Engine Host spike at render-bench's paths, so tools/cdp-core.mjs drives it as it drives the
// Blazor hosts:
//
//   /core/sheet, /core/pivot   the pages (client/src/sheet-page.ts, pivot-page.ts)
//   /probe                     the engine steps timed alone in the Worker (client/src/probe-page.ts)
//   /check                     the TypeScript engine's answers against the C# engine's (client/src/check-page.ts)
//   /api/wire                  a page that is not JSON: there is no circuit here, as on Bench.Host
//   /dist/*                    the bundled Worker and pages (client/build.mjs)
//   /_framework/*              a published EngineHost: the interpreter's or the AOT build's
//   /core-bench.js             render-bench's driver (Bench.Live/wwwroot/core-bench.js)
//   /ex-grid.min.css           ExGrid's shipped stylesheet, which the Script Core draws against
//
//   node serve.mjs <published wwwroot> [port]
//   node serve.mjs ts [port]
//   node serve.mjs ts-page [port]
//
// With `ts`, the TypeScript engine's Worker (client/src/ts-engine-worker.ts) is served at the .NET Worker's
// path, /dist/engine-worker.js, and there is no _framework: the pages are the same bytes either way. With
// `ts-page` there is no Worker: the pages run the TypeScript engine on their own thread
// (client/src/ts-engine-page.ts), told so by one line in their head.
//
// A file is sent Brotli-compressed when the browser accepts it: the publish's own .br where it wrote one,
// as a production host would send it; so the bytes the page records are the bytes a user downloads. No
// dependencies.
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import zlib from 'node:zlib';

const here = path.dirname(new URL(import.meta.url).pathname);
const [published, port = '5493'] = process.argv.slice(2);
if (!published) {
    console.error('usage: node serve.mjs <published wwwroot> [port]');
    process.exit(2);
}
const onPage = published === 'ts-page';
const typescript = published === 'ts' || onPage;
const types = { '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.css': 'text/css', '.map': 'application/json', '.dat': 'application/octet-stream', '.html': 'text/html; charset=utf-8' };

const bare = (name) => `<!DOCTYPE html>
<html lang="en">
<head><meta charset="utf-8" /><title>Engine Host: ${name}</title><link rel="icon" href="data:," /></head>
<body><script type="module" src="/dist/${name}-page.js"></script></body>
</html>
`;

const page = (scenario) => `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <title>Engine Host: /core/${scenario}</title>
    <link rel="icon" href="data:," />
    <link rel="stylesheet" href="/ex-grid.min.css" />
    <script src="/core-bench.js"></script>
${onPage ? `    <script>window.engineOnPage = '/dist/ts-engine-page.js';</script>
` : ''}
</head>
<body>
    <div id="core-bench" style="width: 1200px">
${scenario === 'pivot' ? `        <div class="core-controls">
            <button type="button" id="core-amend">Amend</button>
            <button type="button" id="core-layout">Swap columns</button>
            <span id="core-state"></span>
        </div>
` : ''}        <div id="${scenario}"></div>
    </div>
    <script type="module" src="/dist/${scenario}-page.js"></script>
</body>
</html>
`;

const files = {
    '/core-bench.js': path.join(here, '../render-bench/Bench.Live/wwwroot/core-bench.js'),
    '/ex-grid.min.css': path.join(here, '../../src/ExGrid/wwwroot/ex-grid.min.css'),
};

// The page's own files (the bundles, the stylesheet, the driver) have no .br beside them: they are
// compressed as they are sent, at Brotli's highest quality, as a publish compresses the runtime's.
// Kept by modification time, so a bundle rebuilt while the server runs is sent as rebuilt.
const compressed = new Map();
const brotliOf = (file) => {
    const key = `${file}@${fs.statSync(file).mtimeMs}`;
    if (!compressed.has(key)) {
        compressed.set(key, zlib.brotliCompressSync(fs.readFileSync(file), { params: { [zlib.constants.BROTLI_PARAM_QUALITY]: 11 } }));
    }
    return compressed.get(key);
};

const send = (request, response, file) => {
    const accepts = /\bbr\b/.test(request.headers['accept-encoding'] ?? '');
    const headers = { 'content-type': types[path.extname(file)] ?? 'application/octet-stream', 'cache-control': 'no-store' };
    if (accepts) headers['content-encoding'] = 'br';
    response.writeHead(200, headers);
    if (!accepts) fs.createReadStream(file).pipe(response);
    else if (fs.existsSync(`${file}.br`)) fs.createReadStream(`${file}.br`).pipe(response);
    else response.end(brotliOf(file));
};

http.createServer((request, response) => {
    const url = new URL(request.url, 'http://localhost');
    const at = decodeURIComponent(url.pathname);
    const scenario = /^\/core\/(sheet|pivot)$/.exec(at)?.[1];
    if (scenario) {
        response.writeHead(200, { 'content-type': types['.html'], 'cache-control': 'no-store' });
        response.end(page(scenario));
        return;
    }
    if (at === '/probe' || at === '/check' || at === '/api/wire') {
        // core-bench.js reads a host with no circuit by an answer that is not JSON (its `wire`), as
        // Bench.Host's fallback page answers; a 404 would be logged by the browser as an error.
        response.writeHead(200, { 'content-type': types['.html'], 'cache-control': 'no-store' });
        response.end(at === '/api/wire' ? '<!DOCTYPE html><title>No circuit</title>' : bare(at.slice(1)));
        return;
    }
    let file = files[at];
    if (!file && at.startsWith('/dist/')) {
        const served = typescript ? at.replace(/^\/dist\/engine-worker\.js(\.map)?$/, '/dist/ts-engine-worker.js$1') : at;
        file = path.join(here, 'client', served);
    }
    if (!file && at.startsWith('/_framework/') && !typescript) file = path.join(published, at);
    if (!file || at.includes('..') || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
        response.writeHead(404, { 'content-type': 'text/plain' });
        response.end(`no ${at}`);
        return;
    }
    send(request, response, file);
}).listen(Number(port), '127.0.0.1', () => console.log(`Engine Host spike on http://127.0.0.1:${port}, ${typescript ? `the TypeScript engine${onPage ? ' on the page' : ''}` : `_framework from ${published}`}`));
