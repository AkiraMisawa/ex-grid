// Serves the WebAssembly DemoHost as `dotnet publish -c Release` wrote it, for CI's layer 3
// (playwright.config.mjs, EXGRID_HOSTS). The published host is static files, so something has
// to serve them; this does what the SDK's dev server does for `dotnet run`, and nothing more:
//
// - a file under the root is served as it is, with its media type;
// - a path that names no file (no extension in its last segment) is the app, index.html,
//   as MapFallbackToFile's {*path:nonfile} gives — a deep link like /wide loads the app;
// - a missing file is a 404, so a reference the publish dropped fails the console capture
//   (CON-1) instead of quietly loading the app in its place;
// - every response says Blazor-Environment: Development, as the dev server's does, so the
//   app runs in the environment it runs in under `dotnet run`.
//
// The precompressed copies (.br, .gz) are not used: the browser asks for the plain file, and
// on localhost the bytes cost nothing.
//
//   node static-host.mjs <root> <port>

import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';

const [root, port] = process.argv.slice(2);
if (!root || !port) {
    console.error('usage: node static-host.mjs <root> <port>');
    process.exit(2);
}
const base = path.resolve(root);
const index = path.join(base, 'index.html');
if (!fs.existsSync(index)) {
    console.error(`${index} does not exist; is ${base} a published wwwroot?`);
    process.exit(2);
}

const types = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.mjs': 'text/javascript; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.json': 'application/json; charset=utf-8',
    '.map': 'application/json; charset=utf-8',
    '.wasm': 'application/wasm',
    '.dat': 'application/octet-stream',
    '.pdb': 'application/octet-stream',
    '.svg': 'image/svg+xml',
    '.png': 'image/png',
    '.ico': 'image/x-icon',
    '.woff': 'font/woff',
    '.woff2': 'font/woff2',
    '.ttf': 'font/ttf',
    '.txt': 'text/plain; charset=utf-8',
};

function send(res, status, file, method) {
    const headers = {
        'Blazor-Environment': 'Development',
        'Cache-Control': 'no-store',
    };
    if (!file) {
        res.writeHead(status, { ...headers, 'Content-Type': 'text/plain; charset=utf-8' });
        res.end(method === 'HEAD' ? undefined : 'Not found');
        return;
    }
    const type = types[path.extname(file).toLowerCase()] ?? 'application/octet-stream';
    res.writeHead(status, { ...headers, 'Content-Type': type, 'Content-Length': fs.statSync(file).size });
    if (method === 'HEAD') {
        res.end();
        return;
    }
    fs.createReadStream(file).pipe(res);
}

http.createServer((req, res) => {
    if (req.method !== 'GET' && req.method !== 'HEAD') {
        res.writeHead(405, { Allow: 'GET, HEAD' });
        res.end();
        return;
    }
    let pathname;
    try {
        pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    } catch {
        send(res, 400, null, req.method);
        return;
    }
    const file = path.join(base, pathname);
    // Never outside the root.
    if (file !== base && !file.startsWith(base + path.sep)) {
        send(res, 404, null, req.method);
        return;
    }
    if (fs.existsSync(file) && fs.statSync(file).isFile()) {
        send(res, 200, file, req.method);
        return;
    }
    const last = pathname.split('/').pop();
    send(res, path.extname(last) === '' ? 200 : 404, path.extname(last) === '' ? index : null, req.method);
}).listen(Number(port), 'localhost', () => {
    console.log(`Serving ${base} at http://localhost:${port}`);
});
