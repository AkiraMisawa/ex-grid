// Serves <root> as GitHub Pages does: /<prefix>/path -> file, a directory -> index.html,
// anything missing -> <prefix>/404.html with status 404. Usage: node pages-server.mjs <root> <prefix> <port>
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
const [root, prefix, port] = process.argv.slice(2);
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.wasm': 'application/wasm', '.map': 'application/json', '.svg': 'image/svg+xml',
  '.png': 'image/png', '.ico': 'image/x-icon', '.woff2': 'font/woff2', '.woff': 'font/woff', '.ttf': 'font/ttf',
  '.dat': 'application/octet-stream', '.txt': 'text/plain', '.webmanifest': 'application/manifest+json' };
http.createServer((req, res) => {
  const url = decodeURIComponent(req.url.split('?')[0]);
  const send = (file, status) => {
    res.writeHead(status, { 'content-type': types[path.extname(file)] ?? 'application/octet-stream', 'cache-control': 'max-age=600' });
    fs.createReadStream(file).pipe(res);
  };
  if (!url.startsWith(`/${prefix}/`) && url !== `/${prefix}`) { res.writeHead(404); res.end('outside the site'); return; }
  let file = path.join(root, url.slice(prefix.length + 1));
  if (!file.startsWith(path.resolve(root))) { res.writeHead(403); res.end(); return; }
  if (fs.existsSync(file) && fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
  if (fs.existsSync(file)) send(file, 200); else send(path.join(root, '404.html'), 404);
}).listen(Number(port), '127.0.0.1', () => console.log(`serving ${root} at http://127.0.0.1:${port}/${prefix}/`));
