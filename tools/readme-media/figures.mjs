// Draws the Docs Site's figures from the pages whose parts they name (ADR-0110).
//
//   node figures.mjs --base http://localhost:5310/ [--only pivot-anatomy] [--write]
//   node figures.mjs --names        # the names alone, when only their words changed
//
// The site must already be running (see README.md). Each figure in figures/ opens its page, waits
// for it to be ready, and numbers the parts it lists: a ring around each part, and its number beside
// it with a line to the ring. The parts' names are written beside the picture as text, never into
// it, so they read at any width a README or a page shows the picture at, and can be copied. The
// numbers are drawn over the page, outside the application's elements, and change nothing it does;
// only the frame's margin grows, to make room for them.
//
// --write puts the PNGs into the Docs Site's wwwroot/figures, and writes the names from the
// figures' definitions: the Docs Site's Figures/DrawnFigures.g.cs, which its DocsFigure component
// reads, and each README block between `<!-- figure NAME -->` and `<!-- /figure NAME -->`. Without
// it the PNGs stay in out/. --names writes the names alone, from the definitions and the PNGs
// already in wwwroot/figures: a note reworded needs no new picture, a part added or moved does.
//
// A figure may `prepare` its page first with a user's gestures — a selection, an edit opened —
// which the numbers then point at. A part says where its number stands: `left` or `right` of the
// frame, or of the element `of` names; or in a row `top` of the frame; or `below` it, or below
// `of`. Its line meets the ring at `at`, a fraction along the ring's side that faces the number. A
// number over or under its part stands centred on that point, with a straight line, and a row
// further out when it would cover another number or line. A part's target is a locator, or a box a
// figure works out from the page (a cell by its column's header and its row); `within` cuts it to
// what an element shows, so a row panned part out of sight is ringed where it is seen.

import { chromium } from 'playwright';
import { mkdirSync, readdirSync, copyFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repo = join(here, '..', '..');
const args = Object.fromEntries(process.argv.slice(2).reduce((pairs, arg, i, all) => {
  if (arg.startsWith('--')) pairs.push([arg.slice(2), all[i + 1]?.startsWith('--') || all[i + 1] === undefined ? true : all[i + 1]]);
  return pairs;
}, []));

const base = String(args.base ?? 'http://localhost:5310/').replace(/\/?$/, '/');
const only = args.only ? String(args.only).split(',') : null;
const out = join(here, 'out');
const published = join(repo, 'samples', 'ExGrid.Docs', 'wwwroot', 'figures');
const site = 'https://akiramisawa.github.io/ex-grid/';
mkdirSync(out, { recursive: true });

// A number's circle, the gap between it and what it stands beside, the space between two numbers,
// and the margin around the whole picture.
const badge = 26;
const gap = 18;
const apart = 8;
const margin = 12;

const all = [];
for (const file of readdirSync(join(here, 'figures')).filter(f => f.endsWith('.mjs')).sort())
  all.push((await import(join(here, 'figures', file))).default);
if (args.names) {
  writeDocsNames(all);
  writeReadmeNames(all);
  process.exit(0);
}
const figures = all.filter(figure => !only || only.includes(figure.name));
if (figures.length === 0) throw new Error('No figure matches --only.');

const browser = await chromium.launch({
  // A container's bundled Chromium when there is one; otherwise Playwright's own.
  executablePath: process.env.CHROMIUM_PATH || undefined,
});

for (const figure of figures) {
  // Twice the pixels, so the figure stays sharp where a README or a page scales it.
  const context = await browser.newContext({ viewport: { width: 1700, height: 1200 }, deviceScaleFactor: 2, colorScheme: 'light' });
  const page = await context.newPage();
  const problems = [];
  page.on('console', m => { if (m.type() === 'error' && !m.text().startsWith('Failed to load resource')) problems.push(m.text()); });
  page.on('response', r => { if (r.status() >= 400 && !r.request().isNavigationRequest()) problems.push(`${r.status()} ${r.url()}`); });
  page.on('pageerror', e => problems.push(e.message));

  await page.goto(`${base}${figure.path}`);
  await figure.ready(page);
  await whole(page, figure.whole ?? []);
  if (figure.prepare) await figure.prepare(page);

  // Where each number stands says how much room the frame needs around it; then the room, and the
  // places again on the moved page.
  const room = roomFor(layout(figure, await measure(page, figure)));
  await page.locator(figure.frame).evaluate((el, r) => { el.style.margin = `${r.top}px ${r.right}px ${r.bottom}px ${r.left}px`; }, room);
  await page.evaluate(() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done))));
  await figure.ready(page);
  await whole(page, figure.whole ?? []);
  const boxes = await measure(page, figure);
  const { placed } = layout(figure, boxes);
  await page.evaluate(drawNumbers, { badge, places: placed.map(p => onPage(p, boxes.frame)) });

  if (problems.length > 0)
    throw new Error(`${figure.name}: the page reported problems, so its figure is not used:\n${problems.join('\n')}`);

  const clip = {
    x: boxes.frame.x - room.left, y: boxes.frame.y - room.top,
    width: boxes.frame.width + room.left + room.right, height: boxes.frame.height + room.top + room.bottom,
  };
  const file = join(out, `${figure.name}.png`);
  await page.screenshot({ path: file, clip, animations: 'disabled' });
  await context.close();
  console.log(`${figure.name}: ${Math.round(clip.width)}×${Math.round(clip.height)} -> ${file}`);

  if (args.write) {
    mkdirSync(published, { recursive: true });
    copyFileSync(file, join(published, `${figure.name}.png`));
    console.log(`  written to ${join(published, `${figure.name}.png`)}`);
  }
}

await browser.close();

if (args.write) {
  writeDocsNames(all);
  writeReadmeNames(all);
}

/** Refuses a picture in which an element named by `selectors` would scroll: a figure shows each part whole. */
async function whole(page, selectors) {
  for (const selector of selectors) {
    const cut = await page.locator(selector).evaluateAll(els => els.some(el =>
      el.scrollHeight > el.clientHeight + 1 || el.scrollWidth > el.clientWidth + 1));
    if (cut) throw new Error(`${selector} scrolls, so part of it would be cut out of the picture: give the page room`);
  }
}

/** The frame's box on the page, and each part's and each `of` element's box from the frame's corner. */
async function measure(page, figure) {
  const frame = await boxOf(page.locator(figure.frame), 'the frame');
  const from = box => ({ x: box.x - frame.x, y: box.y - frame.y, width: box.width, height: box.height });
  const parts = [];
  for (const part of figure.parts) {
    let target = await boxOf(part.target(page), part.term);
    if (part.within) target = cut(target, await boxOf(part.within(page), `what ${part.term} is seen within`), part.term);
    parts.push({
      target: from(target),
      of: part.of ? from(await boxOf(part.of(page), `what ${part.term} stands beside`)) : null,
    });
  }
  return { frame, parts };
}

/** The part of `box` that `within` shows. */
function cut(box, within, what) {
  const x = Math.max(box.x, within.x), y = Math.max(box.y, within.y);
  const right = Math.min(box.x + box.width, within.x + within.width), bottom = Math.min(box.y + box.height, within.y + within.height);
  if (right <= x || bottom <= y) throw new Error(`${what}: not within what shows it`);
  return { x, y, width: right - x, height: bottom - y };
}

/**
 * A part's box on the page, or a refusal that names the part: a figure never names what is not
 * there. A target is a locator for one element; `{ all: locator }`, for the box around every
 * element it finds on screen; or a promise of a box the figure worked out itself.
 */
async function boxOf(target, what) {
  target = await target;
  // A locator has a method named `all` too, so it is told apart by `count` first.
  if (typeof target?.count === 'function') {
    if (await target.count() !== 1)
      throw new Error(`${what}: the page shows ${await target.count()} of it, not one`);
    const box = await target.boundingBox();
    if (!box) throw new Error(`${what}: not on screen`);
    return box;
  }
  if (target?.all) {
    const boxes = (await Promise.all((await target.all.all()).map(l => l.boundingBox())))
      .filter(b => b && b.width > 0 && b.height > 0);
    if (boxes.length === 0) throw new Error(`${what}: not on screen`);
    const x = Math.min(...boxes.map(b => b.x)), y = Math.min(...boxes.map(b => b.y));
    return { x, y, width: Math.max(...boxes.map(b => b.x + b.width)) - x, height: Math.max(...boxes.map(b => b.y + b.height)) - y };
  }
  if (!target || !(target.width > 0) || !(target.height > 0)) throw new Error(`${what}: not on screen`);
  return target;
}

/** Where each number, ring and line stands, from the frame's corner. */
function layout(figure, boxes) {
  const frame = { x: 0, y: 0, width: boxes.frame.width, height: boxes.frame.height };
  const placed = figure.parts.map((part, i) => {
    // A ring stands just inside its part unless the part says otherwise, so the rings of two
    // parts that touch never overlap.
    const grow = part.ring ?? -2;
    const t = boxes.parts[i].target;
    const ring = { x: t.x - grow, y: t.y - grow, width: t.width + 2 * grow, height: t.height + 2 * grow };
    const at = part.at ?? 0.5;
    const meet = part.side === 'left' ? { x: ring.x, y: ring.y + at * ring.height }
      : part.side === 'right' ? { x: ring.x + ring.width, y: ring.y + at * ring.height }
      : part.side === 'top' ? { x: ring.x + at * ring.width, y: ring.y }
      : { x: ring.x + at * ring.width, y: ring.y + ring.height };
    return { number: i + 1, term: part.term, side: part.side, of: boxes.parts[i].of ?? frame, ring, meet, name: { width: badge, height: badge } };
  });

  // Left and right: at the gap from what it stands beside, level with where its line meets the
  // ring, or below the number before it.
  const beside = box => `${box.x},${box.y},${box.width},${box.height}`;
  for (const side of ['left', 'right']) {
    for (const column of Map.groupBy(placed.filter(p => p.side === side), p => beside(p.of)).values()) {
      let floor = -Infinity;
      for (const p of column.sort((a, b) => a.meet.y - b.meet.y)) {
        p.name.x = side === 'left' ? p.of.x - gap - p.name.width : p.of.x + p.of.width + gap;
        p.name.y = Math.max(p.meet.y - p.name.height / 2, floor);
        floor = p.name.y + p.name.height + apart;
      }
    }
  }

  // Top and below: in rows, each number centred on where its line meets the ring, its line straight.
  const covers = (a, b) => a.name.x < b.name.x + b.name.width + apart && b.name.x < a.name.x + a.name.width + apart;
  const crosses = (x, q) => x > q.name.x - 3 && x < q.name.x + q.name.width + 3;
  for (const side of ['top', 'below']) {
    const numbers = placed.filter(p => p.side === side).sort((a, b) => a.meet.x - b.meet.x);
    const pitch = badge + apart;
    const rows = new Map();
    for (const p of numbers) {
      p.name.x = p.meet.x - p.name.width / 2;
      const key = beside(p.of);
      if (!rows.has(key)) rows.set(key, []);
      const lines = rows.get(key);
      // A row further out never takes a line off a number nearer in: two numbers whose lines pass
      // through each other are refused, for the figure to move one of them.
      for (const q of numbers.filter(q => q !== p && q.name.y !== undefined))
        if (crosses(p.meet.x, q) && crosses(q.meet.x, p))
          throw new Error(`${figure.name}: the numbers of "${p.term}" and "${q.term}" stand too close; move one's \`at\``);
      let row = 0;
      while ((lines[row] ?? []).some(q => covers(p, q))
        || lines.slice(0, row).flat().some(q => crosses(p.meet.x, q))
        || lines.slice(row + 1).flat().some(q => crosses(q.meet.x, p))) {
        if (lines.slice(0, row + 1).flat().some(q => crosses(p.meet.x, q)))
          throw new Error(`${figure.name}: the line of "${p.term}" would pass through the number of another; move its \`at\``);
        row++;
      }
      (lines[row] ??= []).push(p);
      p.name.y = side === 'top'
        ? p.of.y - gap - p.name.height - row * pitch
        : p.of.y + p.of.height + gap + row * pitch;
    }
  }
  return { frame, placed };
}

/** The margin the frame needs for every number and ring, with the picture's own margin around it. */
function roomFor({ frame, placed }) {
  const edges = placed.flatMap(p => [p.name, p.ring]);
  const left = Math.min(0, ...edges.map(e => e.x));
  const top = Math.min(0, ...edges.map(e => e.y));
  const right = Math.max(frame.width, ...edges.map(e => e.x + e.width));
  const bottom = Math.max(frame.height, ...edges.map(e => e.y + e.height));
  return {
    left: Math.ceil(margin - left), top: Math.ceil(margin - top),
    right: Math.ceil(right - frame.width + margin), bottom: Math.ceil(bottom - frame.height + margin),
  };
}

/** A place from the frame's corner, on the page. */
function onPage({ number, name, ring, meet }, frame) {
  return {
    number,
    centre: { x: name.x + name.width / 2 + frame.x, y: name.y + name.height / 2 + frame.y },
    ring: { ...ring, x: ring.x + frame.x, y: ring.y + frame.y },
    meet: { x: meet.x + frame.x, y: meet.y + frame.y },
  };
}

// In the page: a layer of its own over everything, with each part's ring, the line from its number
// to the ring, a dot where the line meets it, and the number in a circle over the line's other end.
function drawNumbers({ badge, places }) {
  const accent = '#c2255c';
  const font = getComputedStyle(document.body).fontFamily;
  const layer = document.createElement('div');
  layer.id = 'figure-numbers';
  Object.assign(layer.style, { position: 'fixed', left: '0', top: '0', width: '0', height: '0', zIndex: '2147483647', pointerEvents: 'none' });
  document.documentElement.appendChild(layer);
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  Object.assign(svg.style, { position: 'absolute', left: '0', top: '0', overflow: 'visible' });
  svg.setAttribute('width', '1');
  svg.setAttribute('height', '1');
  layer.appendChild(svg);
  const add = (tag, attributes, text) => {
    const el = document.createElementNS('http://www.w3.org/2000/svg', tag);
    for (const [k, v] of Object.entries(attributes)) el.setAttribute(k, String(v));
    if (text !== undefined) el.textContent = text;
    svg.appendChild(el);
  };
  for (const { ring, centre, meet } of places) {
    add('rect', { x: ring.x, y: ring.y, width: ring.width, height: ring.height, rx: 4, fill: 'none', stroke: accent, 'stroke-width': 2 });
    add('line', { x1: centre.x, y1: centre.y, x2: meet.x, y2: meet.y, stroke: accent, 'stroke-width': 1.5 });
    add('circle', { cx: meet.x, cy: meet.y, r: 3.5, fill: accent });
  }
  for (const { number, centre } of places) {
    add('circle', { cx: centre.x, cy: centre.y, r: badge / 2, fill: accent, stroke: '#ffffff', 'stroke-width': 2 });
    add('text', {
      x: centre.x, y: centre.y, fill: '#ffffff', 'text-anchor': 'middle', 'dominant-baseline': 'central',
      style: `font: 700 14px ${font}`,
    }, String(number));
  }
}

/** A drawn figure's size in CSS pixels, from its PNG, which has twice as many. */
function sizeOf(name) {
  const file = join(published, `${name}.png`);
  if (!existsSync(file)) throw new Error(`${name}: no ${file}; draw it with --write`);
  const png = readFileSync(file);
  return { width: png.readUInt32BE(16) / 2, height: png.readUInt32BE(20) / 2 };
}

/** The Docs Site's names for every figure, read by its DocsFigure component. */
function writeDocsNames(figures) {
  const text = s => JSON.stringify(s);
  const entries = figures.map(figure => {
    const { width, height } = sizeOf(figure.name);
    const parts = figure.parts.map(p => `                new(${text(p.term)}, ${text(p.note)}),`).join('\n');
    return `        [${text(figure.name)}] = new(\n            ${text(figure.name)}, ${width}, ${height},\n            ${text(figure.alt)},\n            [\n${parts}\n            ]),`;
  }).join('\n');
  const file = join(repo, 'samples', 'ExGrid.Docs', 'Figures', 'DrawnFigures.g.cs');
  writeFileSync(file, `// <auto-generated>
// Written by tools/readme-media/figures.mjs from its figures/*.mjs, beside the PNGs it writes to
// wwwroot/figures: each figure's size, what it shows, and the names its numbers stand for. Draw the
// figures again rather than edit this file (ADR-0110).
// </auto-generated>

namespace ExGrid.Docs.Figures;

internal static class DrawnFigures
{
    internal static readonly IReadOnlyDictionary<string, DrawnFigure> ByName = new Dictionary<string, DrawnFigure>
    {
${entries}
    };
}
`);
  console.log(`names written to ${file}`);
}

/** Each figure's block in the README: the picture, linking to its Overview, and the names under it. */
function writeReadmeNames(figures) {
  const file = join(repo, 'README.md');
  let readme = readFileSync(file, 'utf8');
  for (const figure of figures.filter(f => f.overview)) {
    const start = `<!-- figure ${figure.name} -->`, end = `<!-- /figure ${figure.name} -->`;
    const from = readme.indexOf(start), to = readme.indexOf(end);
    if (from < 0 || to < from) throw new Error(`README.md has no block for ${figure.name}: ${start} … ${end}`);
    const names = figure.parts.map((p, i) => `${i + 1}. **${p.term}**: ${p.note}`).join('\n');
    const block = `${start}\n[![${figure.alt}](samples/ExGrid.Docs/wwwroot/figures/${figure.name}.png)](${site}${figure.overview})\n\n${names}\n`;
    readme = readme.slice(0, from) + block + readme.slice(to);
  }
  writeFileSync(file, readme);
  console.log(`names written to ${file}`);
}
