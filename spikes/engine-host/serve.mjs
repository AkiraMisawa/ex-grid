// Serves the Engine Host spike at render-bench's paths, so tools/cdp-core.mjs drives it as it drives the
// Blazor hosts:
//
//   /core/sheet, /core/pivot   the pages (client/src/sheet-page.ts, pivot-page.ts)
//   /probe                     the engine steps timed alone in the Worker (client/src/probe-page.ts)
//   /api/wire                  a page that is not JSON: there is no circuit here, as on Bench.Host
//   /dist/*                    the bundled Worker and pages (client/build.mjs)
//   /_framework/*              a published EngineHost: the interpreter's or the AOT build's
//   /core-bench.js             render-bench's driver (Bench.Live/wwwroot/core-bench.js)
//   /ex-grid.min.css           ExGrid's shipped stylesheet, which the Script Core draws against
//
//   node serve.mjs <published wwwroot> [port]
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
const types = { '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.css': 'text/css', '.map': 'application/json', '.dat': 'application/octet-stream', '.html': 'text/html; charset=utf-8' };

const probe = `<!DOCTYPE html>
<html lang="en">
<head><meta charset="utf-8" /><title>Engine Host: probe</title><link rel="icon" href="data:," /></head>
<body><script type="module" src="/dist/probe-page.js"></script></body>
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
const compressed = new Map();
const brotliOf = (file) => {
    if (!compressed.has(file)) {
        compressed.set(file, zlib.brotliCompressSync(fs.readFileSync(file), { params: { [zlib.constants.BROTLI_PARAM_QUALITY]: 11 } }));
    }
    return compressed.get(file);
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
    if (at === '/probe' || at === '/api/wire') {
        // core-bench.js reads a host with no circuit by an answer that is not JSON (its `wire`), as
        // Bench.Host's fallback page answers; a 404 would be logged by the browser as an error.
        response.writeHead(200, { 'content-type': types['.html'], 'cache-control': 'no-store' });
        response.end(at === '/probe' ? probe : '<!DOCTYPE html><title>No circuit</title>');
        return;
    }
    let file = files[at];
    if (!file && at.startsWith('/dist/')) file = path.join(here, 'client', at);
    if (!file && at.startsWith('/_framework/')) file = path.join(published, at);
    if (!file || at.includes('..') || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
        response.writeHead(404, { 'content-type': 'text/plain' });
        response.end(`no ${at}`);
        return;
    }
    send(request, response, file);
}).listen(Number(port), '127.0.0.1', () => console.log(`Engine Host spike on http://127.0.0.1:${port}, _framework from ${published}`));
