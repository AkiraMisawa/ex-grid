// Draws the Docs Site's figures from the pages whose parts they name (ADR-0110).
//
//   node figures.mjs --base http://localhost:5310/ [--only pivot-anatomy] [--write]
//
// The site must already be running (see README.md). Each figure in figures/ opens its page, waits
// for it to be ready, and names the parts it lists: a ring around each part, and the part's name
// beside it with a line to the ring. The names are drawn over the page, outside the application's
// elements, and change nothing it does; only the frame's margin grows, to make room for them.
// --write puts the PNGs into the Docs Site's wwwroot/figures, which the Docs Site and the README
// show; without it they stay in out/.
//
// A figure may `prepare` its page first with a user's gestures — a selection, an edit opened —
// which the names then point at. A part says where its name stands: `left` or `right` of the
// frame, or of the element `of` names; or in a row `top` of the frame; or `below` it, or below
// `of`. Its line meets the ring at `at`, a fraction along the ring's side that faces the name. A
// name over or under its part stands centred on that point, with a straight line, and a row
// further out when it would cover another name or line. A part's target is a locator, or a box a
// figure works out from the page (a cell by its column's header and its row); `within` cuts it to
// what an element shows, so a row panned part out of sight is ringed where it is seen.

import { chromium } from 'playwright';
import { mkdirSync, readdirSync, copyFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const args = Object.fromEntries(process.argv.slice(2).reduce((pairs, arg, i, all) => {
  if (arg.startsWith('--')) pairs.push([arg.slice(2), all[i + 1]?.startsWith('--') || all[i + 1] === undefined ? true : all[i + 1]]);
  return pairs;
}, []));

const base = String(args.base ?? 'http://localhost:5310/').replace(/\/?$/, '/');
const only = args.only ? String(args.only).split(',') : null;
const out = join(here, 'out');
mkdirSync(out, { recursive: true });

// The gap between a name and what it stands beside, the space between two names, and the margin
// around the whole picture.
const gap = 26;
const apart = 12;
const margin = 14;

const figures = [];
for (const file of readdirSync(join(here, 'figures')).filter(f => f.endsWith('.mjs')).sort()) {
  const figure = (await import(join(here, 'figures', file))).default;
  if (!only || only.includes(figure.name)) figures.push(figure);
}
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

  // The names, unplaced, for their sizes; then where each stands, which says how much room the
  // frame needs around it; then the room, and the places again on the moved page.
  const sizes = await page.evaluate(writeNames, figure.parts.map(p => ({ term: p.term, note: p.note, side: p.side })));
  const room = roomFor(layout(figure, sizes, await measure(page, figure)));
  await page.locator(figure.frame).evaluate((el, r) => { el.style.margin = `${r.top}px ${r.right}px ${r.bottom}px ${r.left}px`; }, room);
  await page.evaluate(() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done))));
  await figure.ready(page);
  await whole(page, figure.whole ?? []);
  const boxes = await measure(page, figure);
  const { placed } = layout(figure, sizes, boxes);
  await page.evaluate(drawNames, placed.map(p => onPage(p, boxes.frame)));

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
    const target = join(here, '..', '..', 'samples', 'ExGrid.Docs', 'wwwroot', 'figures', `${figure.name}.png`);
    mkdirSync(dirname(target), { recursive: true });
    copyFileSync(file, target);
    console.log(`  written to ${target}`);
  }
}

await browser.close();

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

/** Where each name, ring and line stands, from the frame's corner. */
function layout(figure, sizes, boxes) {
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
    return { term: part.term, side: part.side, of: boxes.parts[i].of ?? frame, ring, meet, name: { ...sizes[i] } };
  });

  // Left and right: at the gap from what it stands beside, level with where its line meets the
  // ring, or below the name before it.
  const beside = box => `${box.x},${box.y},${box.width},${box.height}`;
  for (const side of ['left', 'right']) {
    for (const column of Map.groupBy(placed.filter(p => p.side === side), p => beside(p.of)).values()) {
      let floor = -Infinity;
      for (const p of column.sort((a, b) => a.meet.y - b.meet.y)) {
        p.name.x = side === 'left' ? p.of.x - gap - p.name.width : p.of.x + p.of.width + gap;
        p.name.y = Math.max(p.meet.y - p.name.height / 2, floor);
        floor = p.name.y + p.name.height + apart;
        const end = side === 'left' ? p.name.x + p.name.width + 6 : p.name.x - 6;
        p.line = [[end, p.name.y + p.name.height / 2], [p.meet.x, p.meet.y]];
      }
    }
  }

  // Top and below: in rows, each name centred on where its line meets the ring, its line straight.
  const covers = (a, b) => a.name.x < b.name.x + b.name.width + apart && b.name.x < a.name.x + a.name.width + apart;
  const crosses = (x, q) => x > q.name.x - 6 && x < q.name.x + q.name.width + 6;
  for (const side of ['top', 'below']) {
    const names = placed.filter(p => p.side === side).sort((a, b) => a.meet.x - b.meet.x);
    const pitch = Math.max(0, ...names.map(p => p.name.height)) + apart;
    const rows = new Map();
    for (const p of names) {
      p.name.x = p.meet.x - p.name.width / 2;
      const key = beside(p.of);
      if (!rows.has(key)) rows.set(key, []);
      const lines = rows.get(key);
      // A row further out never takes a line off a name nearer in: two names whose lines pass
      // through each other are refused, for the figure to move one of them.
      for (const q of names.filter(q => q !== p && q.name.y !== undefined))
        if (crosses(p.meet.x, q) && crosses(q.meet.x, p))
          throw new Error(`${figure.name}: the names of "${p.term}" and "${q.term}" stand too close; move one's \`at\``);
      let row = 0;
      while ((lines[row] ?? []).some(q => covers(p, q))
        || lines.slice(0, row).flat().some(q => crosses(p.meet.x, q))
        || lines.slice(row + 1).flat().some(q => crosses(q.meet.x, p))) {
        if (lines.slice(0, row + 1).flat().some(q => crosses(p.meet.x, q)))
          throw new Error(`${figure.name}: the line of "${p.term}" would pass through the name of another; move its \`at\``);
        row++;
      }
      (lines[row] ??= []).push(p);
      if (side === 'top') {
        p.name.y = p.of.y - gap - p.name.height - row * pitch;
        p.line = [[p.meet.x, p.name.y + p.name.height + 4], [p.meet.x, p.meet.y]];
      } else {
        p.name.y = p.of.y + p.of.height + gap + row * pitch;
        p.line = [[p.meet.x, p.name.y - 4], [p.meet.x, p.meet.y]];
      }
    }
  }
  return { frame, placed };
}

/** The margin the frame needs for every name and ring, with the picture's own margin around it. */
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
function onPage({ name, ring, line }, frame) {
  return {
    name: { x: name.x + frame.x, y: name.y + frame.y },
    ring: { ...ring, x: ring.x + frame.x, y: ring.y + frame.y },
    line: line.map(([x, y]) => [x + frame.x, y + frame.y]),
  };
}

// In the page: a layer of its own over everything, holding each name, unplaced. Answers each name's size.
function writeNames(names) {
  const font = getComputedStyle(document.body).fontFamily;
  const layer = document.createElement('div');
  layer.id = 'figure-names';
  Object.assign(layer.style, { position: 'fixed', left: '0', top: '0', width: '0', height: '0', zIndex: '2147483647', pointerEvents: 'none' });
  document.documentElement.appendChild(layer);
  return names.map(({ term, note, side }) => {
    const name = document.createElement('div');
    name.className = 'figure-name';
    Object.assign(name.style, {
      position: 'absolute', left: '-9999px', top: '0', whiteSpace: 'nowrap',
      font: `600 17px/1.25 ${font}`, color: '#1b2230',
      textAlign: side === 'left' ? 'right' : side === 'right' ? 'left' : 'center',
    });
    name.textContent = term;
    if (note) {
      const small = document.createElement('div');
      Object.assign(small.style, {
        font: `400 14px/1.3 ${font}`, color: '#4f5867', marginTop: '2px', whiteSpace: 'normal', width: 'max-content', maxWidth: '176px',
        marginLeft: side === 'right' ? '0' : 'auto', marginRight: side === 'left' ? '0' : 'auto',
      });
      small.textContent = note;
      name.appendChild(small);
    }
    layer.appendChild(name);
    const r = name.getBoundingClientRect();
    return { width: r.width, height: r.height };
  });
}

// In the page: puts each name in its place, and draws its ring and its line, with a dot where the
// line meets the ring.
function drawNames(places) {
  const accent = '#c2255c';
  const layer = document.getElementById('figure-names');
  const names = [...layer.querySelectorAll('.figure-name')];
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  Object.assign(svg.style, { position: 'absolute', left: '0', top: '0', overflow: 'visible' });
  svg.setAttribute('width', '1');
  svg.setAttribute('height', '1');
  layer.prepend(svg);
  const add = (tag, attributes) => {
    const el = document.createElementNS('http://www.w3.org/2000/svg', tag);
    for (const [k, v] of Object.entries(attributes)) el.setAttribute(k, String(v));
    svg.appendChild(el);
  };
  places.forEach(({ name, ring, line }, i) => {
    Object.assign(names[i].style, { left: `${name.x}px`, top: `${name.y}px` });
    add('rect', { x: ring.x, y: ring.y, width: ring.width, height: ring.height, rx: 4, fill: 'none', stroke: accent, 'stroke-width': 2 });
    const [[x1, y1], [x2, y2]] = line;
    add('line', { x1, y1, x2, y2, stroke: accent, 'stroke-width': 1.5 });
    add('circle', { cx: x2, cy: y2, r: 3.5, fill: accent });
  });
}
