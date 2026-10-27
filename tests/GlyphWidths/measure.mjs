// Measures what Chrome or Edge paints for every string in corpus.json, and for every glyph they
// hold, in one face, at 14px and 12px, at weights 400, 500, 600 and 700, with tabular digits — the
// numbers the per-class estimate has to cover (ADR-0016; tickets 82 and 83). README.md beside this
// file says when to run it and what reads its output.
//
//   node tests/GlyphWidths/measure.mjs roboto
//   node tests/GlyphWidths/measure.mjs system-ui
//   node tests/GlyphWidths/measure.mjs system-ui --browser msedge
//   node tests/GlyphWidths/measure.mjs dejavu-sans --dejavu "$(nix build --no-link --print-out-paths nixpkgs#dejavu_fonts)/share/fonts/truetype"
//
// Writes <face>.<platform>.json here, measured in Google Chrome. `--browser chrome` or
// `--browser msedge` names the browser, and the record is then <face>.<platform>.<browser>.json,
// so a platform measured in both keeps both (Windows). Each is looked for where it installs
// itself; `--chrome <path>` names the executable instead. `--text <string>`, given once or more,
// measures those strings instead and prints them, writing nothing.
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { classed } from './classes.mjs';

// fileURLToPath, not the URL's pathname: on Windows the pathname is /C:/…, which is no path there.
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');

const args = process.argv.slice(2);
const face = args[0];
const option = (name) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
};

const platform = { darwin: 'macos', linux: 'linux', win32: 'windows' }[process.platform] ?? process.platform;
const sizes = [14, 12];
const weights = [400, 500, 600, 700];

const fontData = (file, type) => `url(data:${type};base64,${fs.readFileSync(file).toString('base64')})`;

// Each face as a grid paints it: the family stack that is set, and the @font-face rules behind it,
// inlined so that a file:// page can load them and nothing else can answer.
const faces = {
  // ExGrid.MudBlazor: MudExGridFont.Roboto.Family, over the files and rules the demo pages serve.
  roboto: () => {
    const dir = path.join(repo, 'samples/ExGrid.DemoPages/wwwroot/fonts/roboto');
    const css = fs.readFileSync(path.join(dir, 'roboto.css'), 'utf8')
      .replace(/url\(([^)]+\.woff2)\)/g, (_, file) => fontData(path.join(dir, file), 'font/woff2'));
    return { css, family: 'Roboto, "Helvetica Neue", Arial, sans-serif' };
  },
  // The core's default: ex-grid.css's own stack, so whatever the platform resolves it to.
  'system-ui': () => ({ css: '', family: 'system-ui, sans-serif' }),
  // What system-ui resolves to on Linux, loaded as a web font so any platform can measure it.
  // Book and Bold only, as a Linux system has them: 500 matches Book, and 600 matches Bold.
  'dejavu-sans': () => {
    const dir = option('--dejavu');
    if (!dir) throw new Error('dejavu-sans needs --dejavu <directory holding DejaVuSans.ttf and DejaVuSans-Bold.ttf>');
    const rule = (weight, file) => `@font-face { font-family: 'DejaVu Sans Measured'; font-weight: ${weight}; `
      + `src: ${fontData(fs.realpathSync(path.join(dir, file)), 'font/ttf')}; }`;
    return { css: rule(400, 'DejaVuSans.ttf') + '\n' + rule(700, 'DejaVuSans-Bold.ttf'), family: '"DejaVu Sans Measured", sans-serif' };
  },
  // What Linux paints for Helvetica and Arial, which @ex-grid/mantine's stack names after Roboto (ADR-0390):
  // Liberation Sans, loaded as a web font as DejaVu Sans is. Regular and Bold only, as a Linux system has them.
  'liberation-sans': () => {
    const dir = option('--liberation');
    if (!dir) throw new Error('liberation-sans needs --liberation <directory holding LiberationSans-Regular.ttf and LiberationSans-Bold.ttf>');
    const rule = (weight, file) => `@font-face { font-family: 'Liberation Sans Measured'; font-weight: ${weight}; `
      + `src: ${fontData(fs.realpathSync(path.join(dir, file)), 'font/ttf')}; }`;
    return {
      css: rule(400, 'LiberationSans-Regular.ttf') + '\n' + rule(700, 'LiberationSans-Bold.ttf'),
      family: '"Liberation Sans Measured", sans-serif',
    };
  },
};
if (!faces[face]) throw new Error(`Unknown face '${face}'. One of: ${Object.keys(faces).join(', ')}.`);
const { css, family } = faces[face]();

const texts = args.flatMap((a, i) => (a === '--text' ? [args[i + 1]] : []));
const corpus = texts.length > 0 ? texts : JSON.parse(fs.readFileSync(path.join(here, 'corpus.json'), 'utf8'));
const range = (from, to) => Array.from({ length: to - from + 1 }, (_, i) => String.fromCodePoint(from + i));
const isLetter = (g) => /\p{L}/u.test(g);
// The glyphs a table may charge on their own (ticket 83): the Latin, Greek and Cyrillic letters
// month names and currency symbols are written in, and the single-glyph currency signs .NET's
// cultures use. Signs no culture uses (₯, ₧ …) are left out: a face that lacks one paints it in a
// fallback as wide as 1.4em, and the other class would have to cover it.
const tabled = [
  ...range(0x41, 0x5A), ...range(0x61, 0x7A), ...range(0xC0, 0x17F).filter(isLetter),
  ...range(0x386, 0x3CE).filter(isLetter), ...range(0x400, 0x45F),
  ...'¢¤ƒ֏؋৳฿៛₡₦₩₪₫₭₮₱₲₴₵₸₹₺₼₽₾₿',
];
// Every glyph CellTextMetrics names in a class is measured too, so each claim it makes is held to
// every face.
const glyphs = [...new Set([...corpus.flatMap((s) => [...s]), ...(texts.length > 0 ? [] : [...tabled, ...classed])])]
  .sort((a, b) => a.codePointAt(0) - b.codePointAt(0));

// Where each browser installs itself. Without --browser, Chrome, and the record's name says no browser.
const named = option('--browser');
const installed = {
  chrome: {
    darwin: '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    linux: '/usr/bin/google-chrome',
    win32: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  },
  msedge: {
    darwin: '/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge',
    linux: '/usr/bin/microsoft-edge',
    win32: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  },
};
if (named !== undefined && !installed[named]) throw new Error(`Unknown browser '${named}'. One of: ${Object.keys(installed).join(', ')}.`);
const chrome = option('--chrome') ?? installed[named ?? 'chrome'][process.platform];
if (!chrome || !fs.existsSync(chrome)) throw new Error(`No browser at '${chrome}'. Name it with --chrome <path>.`);

const work = fs.mkdtempSync(path.join(os.tmpdir(), 'glyph-widths-'));
fs.writeFileSync(path.join(work, 'page.html'), `<!doctype html><html><head><meta charset="utf-8"><style>${css}
body { margin: 0; }
.m { font-family: ${family}; font-variant-numeric: tabular-nums; white-space: pre; letter-spacing: normal;
     font-style: normal; position: absolute; left: 0; top: 0; }
</style></head><body><div id="host"></div></body></html>`);

const profile = path.join(work, 'profile');
const browser = spawn(chrome, ['--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`,
  '--no-first-run', '--no-default-browser-check', '--force-device-scale-factor=1',
  // Chrome refuses to start its sandbox as root, which a container's shell often is.
  ...(process.getuid?.() === 0 ? ['--no-sandbox'] : []), 'about:blank'], { stdio: 'ignore' });

try {
  const portFile = path.join(profile, 'DevToolsActivePort');
  for (let i = 0; i < 400 && !fs.existsSync(portFile); i++) await new Promise((r) => setTimeout(r, 50));
  const port = fs.readFileSync(portFile, 'utf8').split('\n')[0];
  const pageUrl = pathToFileURL(path.join(work, 'page.html')).href;
  const target = await (await fetch(`http://127.0.0.1:${port}/json/new?${pageUrl}`, { method: 'PUT' })).json();
  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((r) => socket.addEventListener('open', r));
  let next = 0;
  const pending = new Map();
  socket.addEventListener('message', (e) => {
    const m = JSON.parse(e.data);
    if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
  });
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++next;
    pending.set(id, (m) => (m.error ? reject(new Error(`${method}: ${JSON.stringify(m.error)}`)) : resolve(m.result)));
    socket.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async (expression) => {
    const r = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
    if (r.exceptionDetails) throw new Error(JSON.stringify(r.exceptionDetails));
    return r.result.value;
  };
  for (let i = 0; i < 100 && (await evaluate('document.readyState')) !== 'complete'; i++) {
    await new Promise((r) => setTimeout(r, 50));
  }

  // A glyph's width is the wider of the glyph alone and a hundred of it in a row, divided by a
  // hundred: a glyph that kerns with itself reads short in a run (Roboto's "//"), and one alone
  // is rounded to the 1/64px a layout box is measured in. Every width is in 64ths of a pixel,
  // rounded up, so a reading is never under what was painted.
  const measured = await evaluate(`(async () => {
    const glyphs = ${JSON.stringify(glyphs)}, strings = ${JSON.stringify(corpus)};
    const family = ${JSON.stringify(family)}, sizes = ${JSON.stringify(sizes)}, weights = ${JSON.stringify(weights)};
    const text = glyphs.join('') + strings.join('');
    for (const size of sizes) for (const w of weights) await document.fonts.load(w + ' ' + size + 'px ' + family, text);
    await document.fonts.ready;
    const host = document.getElementById('host');
    const widths = (texts, size, w) => {
      const spans = texts.map((t) => {
        const s = document.createElement('span');
        s.className = 'm'; s.style.fontSize = size + 'px'; s.style.fontWeight = w; s.textContent = t;
        return s;
      });
      host.append(...spans);
      const px = spans.map((s) => s.getBoundingClientRect().width);
      host.replaceChildren();
      return px;
    };
    const in64ths = (px) => Math.ceil(px * 64 - 1e-6);
    const out = { userAgent: navigator.userAgent, sizes: {}, alone: {} };
    for (const size of sizes) {
      const at = out.sizes[size] = { glyphs: {}, strings: {} };
      for (const w of weights) {
        const one = widths(glyphs, size, w), run = widths(glyphs.map((g) => g.repeat(100)), size, w);
        if (size === 14) out.alone[w] = one;
        at.glyphs[w] = one.map((px, i) => in64ths(Math.max(px, run[i] / 100)));
        at.strings[w] = widths(strings, size, w).map(in64ths);
      }
    }
    // One span per weight and glyph, left in place for the check of which face painted it.
    for (const w of weights) for (const [i, g] of glyphs.entries()) {
      const s = document.createElement('span');
      s.className = 'm'; s.style.fontSize = '14px'; s.style.fontWeight = w; s.textContent = g;
      s.dataset.w = w; s.dataset.i = i; host.append(s);
    }
    return out;
  })()`);

  // Which face painted each glyph: a glyph the family does not draw is painted by a fallback,
  // and that fallback is the platform's, not the family's.
  await send('DOM.enable');
  await send('CSS.enable');
  const { root } = await send('DOM.getDocument', { depth: -1 });
  const { nodeIds } = await send('DOM.querySelectorAll', { nodeId: root.nodeId, selector: 'span[data-w]' });
  const painters = Object.fromEntries(weights.map((w) => [w, new Array(glyphs.length)]));
  for (const nodeId of nodeIds) {
    const { attributes } = await send('DOM.getAttributes', { nodeId });
    const a = {};
    for (let i = 0; i < attributes.length; i += 2) a[attributes[i]] = attributes[i + 1];
    const { fonts } = await send('CSS.getPlatformFontsForNode', { nodeId });
    painters[a['data-w']][+a['data-i']] = fonts.map((f) => f.familyName).join(' + ');
  }
  // The widths and the painters are read at different moments. A face that changed in between, such
  // as a web font that applied after the widths were read, would leave a record whose widths are one
  // face's and whose painters another's (Roboto under a Japanese display on 2026-10-12 read Arial's
  // widths and named Roboto). So each glyph's span, its painter read, must still be as wide as the
  // glyph alone was measured at 14px, or nothing is written.
  const again = await evaluate(`[...document.querySelectorAll('span[data-w]')]
    .map((s) => [+s.dataset.w, +s.dataset.i, s.getBoundingClientRect().width])`);
  const changed = again.filter(([w, i, px]) => px !== measured.alone[w][i]);
  if (changed.length > 0) {
    throw new Error(`The face changed while measuring: ${changed.length} glyphs are another width now that their `
      + `painters are read (${changed.slice(0, 8).map(([w, i, px]) => `'${glyphs[i]}' at ${w}, ${measured.alone[w][i]}px `
      + `then ${px}px, painted by ${painters[w][i]}`).join('; ')}). Nothing was written; measure again.`);
  }
  socket.close();

  if (texts.length > 0) {
    for (const [i, text] of texts.entries()) {
      for (const size of sizes) {
        const px = weights.map((w) => (measured.sizes[size].strings[w][i] / 64).toFixed(3)).join(' ');
        console.log(`${JSON.stringify(text)} ${face} ${size}px, weights ${weights.join('/')}: ${px}`);
      }
    }
  } else {
    const out = {
      face,
      family,
      platform,
      userAgent: measured.userAgent,
      measured: new Date().toISOString().slice(0, 10),
      unit: '1/64 px, rounded up',
      corpus: corpus.length,
      // Which corpus: GlyphWidthRecord.cs refuses a record measured from another one.
      corpusSha256: createHash('sha256').update(corpus.join('\n'), 'utf8').digest('hex'),
      glyphs,
      painters,
      sizes: measured.sizes,
    };
    // One line per array, so a re-measurement diffs by weight and size rather than by number.
    const json = JSON.stringify(out, null, 1).replace(/\[\n\s*([^\][{}]*?)\n\s*\]/g,
      (_, inner) => `[${inner.split(/,\n\s*/).join(',')}]`);
    const file = path.join(here, named ? `${face}.${platform}.${named}.json` : `${face}.${platform}.json`);
    fs.writeFileSync(file, json + '\n');
    console.log(`${path.relative(repo, file)}: ${glyphs.length} glyphs, ${corpus.length} strings, ${measured.userAgent}`);
  }
} finally {
  const exited = new Promise((r) => browser.once('exit', r));
  browser.kill();
  await Promise.race([exited, new Promise((r) => setTimeout(r, 5000))]);
  fs.rmSync(work, { recursive: true, force: true });
}
