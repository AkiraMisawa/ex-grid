// Serves a published Bench.Client (`dotnet publish -c Release Bench.Client -o <dir>`) as a static host
// serves a standalone Blazor WebAssembly app in production: each file Brotli-compressed when the publish
// wrote its .br and the browser accepts it, and index.html for every path that names no file (the
// client's routes, /core/sheet among them; /api/wire too, which core-bench.js reads as no circuit).
//
//   node tools/serve-published.mjs <published wwwroot> [port]
//
// Bench.Host serves the build output instead (`dotnet run`), whose runtime is configured for debugging
// (debugLevel -1), and cannot serve a publish: its index.html keeps its asset placeholders there. This is
// what the boot and the speed of a published client are measured against. No dependencies.
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';

const [root, port = '5495'] = process.argv.slice(2);
if (!root) {
    console.error('usage: node tools/serve-published.mjs <published wwwroot> [port]');
    process.exit(2);
}
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.css': 'text/css', '.dat': 'application/octet-stream', '.svg': 'image/svg+xml', '.png': 'image/png', '.woff2': 'font/woff2' };

http.createServer((request, response) => {
    const at = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
    let file = path.join(root, at);
    if (at.includes('..') || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
        if (path.extname(at) !== '') console.log(`index.html for ${at}`);
        file = path.join(root, 'index.html');
    }
    const brotli = fs.existsSync(`${file}.br`) && /\bbr\b/.test(request.headers['accept-encoding'] ?? '');
    const headers = { 'content-type': types[path.extname(file)] ?? 'application/octet-stream', 'cache-control': 'no-store' };
    if (brotli) headers['content-encoding'] = 'br';
    response.writeHead(200, headers);
    fs.createReadStream(brotli ? `${file}.br` : file).pipe(response);
}).listen(Number(port), '127.0.0.1', () => console.log(`${root} on http://127.0.0.1:${port}`));
