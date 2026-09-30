// A static server for a published standalone WebAssembly DemoHost: every path that is not a file
// falls back to index.html, as the app's router expects.
//
//     node static.mjs <published>/wwwroot <port>
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';

const [root, port] = process.argv.slice(2);
const types = {
    '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
    '.json': 'application/json', '.wasm': 'application/wasm', '.woff2': 'font/woff2', '.woff': 'font/woff',
    '.ttf': 'font/ttf', '.png': 'image/png', '.svg': 'image/svg+xml', '.ico': 'image/x-icon',
};
http.createServer((request, response) => {
    let file = path.join(root, decodeURIComponent(new URL(request.url, 'http://host').pathname));
    if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
        file = path.join(root, 'index.html');
    }
    response.writeHead(200, { 'content-type': types[path.extname(file)] ?? 'application/octet-stream', 'cache-control': 'no-store' });
    fs.createReadStream(file).pipe(response);
}).listen(Number(port));
