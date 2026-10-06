// Serves the Docs Site as `dotnet publish -c Release` writes it, the way GitHub Pages serves it
// (ADR-0110), for record.mjs: a recording shows the build a reader runs, never the Debug build
// `dotnet run` serves, which runs the WebAssembly several times slower — the blotter scene took
// 65 seconds under it, against 12 in Release.
//
// - a file under the root is served as it is, with its media type;
// - any other path is the app, as the workflow's 404.html makes it on Pages, so a Showcase's deep
//   link loads;
// - no Blazor-Environment header: Pages sends none, so the app runs as Production.
//
//   node serve.mjs <published wwwroot> <port>

import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';

const [root, port] = process.argv.slice(2);
if (!root || !port) {
    console.error('usage: node serve.mjs <published wwwroot> <port>');
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
    '.wasm': 'application/wasm',
    '.dat': 'application/octet-stream',
    '.dll': 'application/octet-stream',
    '.webcil': 'application/octet-stream',
    '.pdb': 'application/octet-stream',
    '.blat': 'application/octet-stream',
    '.svg': 'image/svg+xml',
    '.png': 'image/png',
    '.ico': 'image/x-icon',
    '.woff': 'font/woff',
    '.woff2': 'font/woff2',
    '.ttf': 'font/ttf',
    '.map': 'application/json; charset=utf-8',
};

http.createServer((request, response) => {
    const url = new URL(request.url ?? '/', 'http://localhost');
    const file = path.join(base, decodeURIComponent(url.pathname));
    const served = file.startsWith(base) && fs.existsSync(file) && fs.statSync(file).isFile() ? file : index;
    response.writeHead(200, { 'Content-Type': types[path.extname(served).toLowerCase()] ?? 'application/octet-stream' });
    fs.createReadStream(served).pipe(response);
}).listen(Number(port), () => console.log(`serving ${base} at http://localhost:${port}/`));
