// A volatility surface drawn in 3D and turned with the pointer. The mesh — its labels, its values,
// its colour scale — is decided in .NET and handed over whole; this module only projects it and
// follows a drag, so a turn costs no round trip. It tells .NET the angles when a drag ends, so the
// view is the page's state and a redraw keeps it. Allowed by the user for 3D rotation (2026-10-13);
// the spike's ADR is still to be written.
const NS = 'http://www.w3.org/2000/svg';
const W = 900, H = 560;

export function attach(host, dotnet) {
  const svg = document.createElementNS(NS, 'svg');
  svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
  svg.setAttribute('class', 'ex-surface-svg');
  svg.setAttribute('role', 'img');
  host.appendChild(svg);
  const state = { mesh: null, yaw: -35, pitch: 28, drag: null };

  const down = e => {
    state.drag = { x: e.clientX, y: e.clientY, yaw: state.yaw, pitch: state.pitch };
    svg.setPointerCapture(e.pointerId);
  };
  const move = e => {
    if (!state.drag) return;
    state.yaw = state.drag.yaw + (e.clientX - state.drag.x) * 0.4;
    state.pitch = Math.max(0, Math.min(90, state.drag.pitch + (e.clientY - state.drag.y) * 0.3));
    draw(svg, state);
  };
  const up = e => {
    if (!state.drag) return;
    state.drag = null;
    svg.releasePointerCapture(e.pointerId);
    dotnet.invokeMethodAsync('OnViewChanged', Math.round(state.yaw), Math.round(state.pitch));
  };
  svg.addEventListener('pointerdown', down);
  svg.addEventListener('pointermove', move);
  svg.addEventListener('pointerup', up);

  return {
    render(mesh, yaw, pitch) { state.mesh = mesh; state.yaw = yaw; state.pitch = pitch; draw(svg, state); },
    svgText() { return new XMLSerializer().serializeToString(svg); },
    dispose() {
      svg.removeEventListener('pointerdown', down);
      svg.removeEventListener('pointermove', move);
      svg.removeEventListener('pointerup', up);
      svg.remove();
    },
  };
}

// The heat ramp, low to high: green, yellow, orange, red.
const RAMP = [[26, 152, 80], [145, 207, 96], [254, 224, 139], [252, 141, 89], [215, 48, 39]];
function heat(t) {
  t = Math.max(0, Math.min(1, t)) * (RAMP.length - 1);
  const i = Math.min(RAMP.length - 2, Math.floor(t)), f = t - i;
  const c = RAMP[i].map((v, k) => Math.round(v + (RAMP[i + 1][k] - v) * f));
  return `rgb(${c[0]},${c[1]},${c[2]})`;
}

function el(name, attrs, text) {
  const e = document.createElementNS(NS, name);
  for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v);
  if (text !== undefined) e.textContent = text;
  return e;
}

// Carried inside the drawing, so that the saved picture looks as the page does.
const STYLE = `
  .ex-surface-svg { font: 11px system-ui, sans-serif; }
  .ex-surface-walls polygon { fill: var(--ex-surface-wall, #f8fafc); stroke: var(--ex-surface-edge, #cbd5e1); }
  .ex-surface-grid { stroke: var(--ex-surface-grid, #e2e8f0); stroke-width: 1; }
  .ex-surface-mesh polygon { stroke: var(--ex-surface-mesh, rgba(15, 23, 42, 0.35)); stroke-width: 0.6; }
  .ex-surface-points circle { fill: var(--ex-surface-point, #0f172a); opacity: 0.55; }
  .ex-surface-points circle:hover { opacity: 1; r: 4; }
  .ex-surface-label text, text.ex-surface-label { fill: var(--ex-surface-text, #475569);
    stroke: var(--ex-surface-halo, #fff); stroke-width: 3px; paint-order: stroke; stroke-linejoin: round; }
  .ex-surface-axis { font-weight: 600; fill: var(--ex-surface-text, #334155); }`;

function draw(svg, { mesh, yaw, pitch }) {
  svg.replaceChildren();
  svg.append(el('style', {}, STYLE));
  if (!mesh || mesh.values.length === 0) return;
  const nx = mesh.xLabels.length, ny = mesh.yLabels.length;
  const { zMin, zMax } = mesh;
  const a = yaw * Math.PI / 180, b = pitch * Math.PI / 180;
  // World: x across the deltas, y along the expiries, z up; each in [-0.5, 0.5], z squashed.
  const world = (i, j, z) => [i / Math.max(1, nx - 1) - 0.5, j / Math.max(1, ny - 1) - 0.5, ((z - zMin) / (zMax - zMin || 1) - 0.5) * 0.6];
  const raw = ([x, y, z]) => {
    const x1 = x * Math.cos(a) - y * Math.sin(a), y1 = x * Math.sin(a) + y * Math.cos(a);
    return [x1, y1 * Math.sin(b) - z * Math.cos(b), y1 * Math.cos(b) + z * Math.sin(b)];
  };
  // Fitted to the drawing at every angle: the box's corners, projected, set the scale and the centre,
  // with room around for the labels. Seen from above, the floor fills what the box's height did.
  const corners = [];
  for (const cx of [-0.5, 0.5]) for (const cy of [-0.5, 0.5]) for (const cz of [-0.3, 0.3]) corners.push(raw([cx, cy, cz]));
  const minX = Math.min(...corners.map(c => c[0])), maxX = Math.max(...corners.map(c => c[0]));
  const minY = Math.min(...corners.map(c => c[1])), maxY = Math.max(...corners.map(c => c[1]));
  const s = Math.min((W - 200) / (maxX - minX), (H - 110) / (maxY - minY));
  const project = p => {
    const [x, y, depth] = raw(p);
    return [W / 2 + (x - (minX + maxX) / 2) * s, H / 2 + (y - (minY + maxY) / 2) * s, depth];
  };
  const P = (i, j, z) => project(world(i, j, z));

  // The floor and the two walls behind the surface, each with its grid; which walls are behind
  // depends on where the viewer stands.
  const backX = Math.sin(a) > 0 ? nx - 1 : 0, backY = Math.cos(a) > 0 ? ny - 1 : 0;
  const walls = el('g', { class: 'ex-surface-walls' });
  const poly = pts => el('polygon', { points: pts.map(p => `${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ') });
  walls.append(poly([P(0, 0, zMin), P(nx - 1, 0, zMin), P(nx - 1, ny - 1, zMin), P(0, ny - 1, zMin)]));
  if (pitch < 89) {
    walls.append(poly([P(0, backY, zMin), P(nx - 1, backY, zMin), P(nx - 1, backY, zMax), P(0, backY, zMax)]));
    walls.append(poly([P(backX, 0, zMin), P(backX, ny - 1, zMin), P(backX, ny - 1, zMax), P(backX, 0, zMax)]));
  }
  const line = (p, q, cls) => el('line', { x1: p[0].toFixed(1), y1: p[1].toFixed(1), x2: q[0].toFixed(1), y2: q[1].toFixed(1), class: cls });
  for (const z of mesh.zTicks) {
    if (pitch >= 89) break;
    walls.append(line(P(0, backY, z), P(nx - 1, backY, z), 'ex-surface-grid'));
    walls.append(line(P(backX, 0, z), P(backX, ny - 1, z), 'ex-surface-grid'));
    const at = P(backX, backY === 0 ? ny - 1 : 0, z);
    walls.append(el('text', { x: (at[0] + (Math.sin(a) > 0 ? 8 : -8)).toFixed(1), y: (at[1] + 4).toFixed(1),
      'text-anchor': Math.sin(a) > 0 ? 'start' : 'end', class: 'ex-surface-label' }, z.toFixed(mesh.zDigits)));
  }
  for (let i = 0; i < nx; i++) walls.append(line(P(i, 0, zMin), P(i, ny - 1, zMin), 'ex-surface-grid'));
  for (let j = 0; j < ny; j++) walls.append(line(P(0, j, zMin), P(nx - 1, j, zMin), 'ex-surface-grid'));
  svg.append(walls);

  // The surface: one quad per cell, the farthest first, coloured by its mean, the mesh drawn over it.
  const quads = [];
  for (let j = 0; j < ny - 1; j++)
    for (let i = 0; i < nx - 1; i++) {
      const v = [mesh.values[j][i], mesh.values[j][i + 1], mesh.values[j + 1][i + 1], mesh.values[j + 1][i]];
      if (v.some(x => x === null)) continue;
      const pts = [P(i, j, v[0]), P(i + 1, j, v[1]), P(i + 1, j + 1, v[2]), P(i, j + 1, v[3])];
      quads.push({ pts, depth: pts.reduce((s, p) => s + p[2], 0) / 4, mean: v.reduce((s, x) => s + x, 0) / 4, i, j });
    }
  quads.sort((p, q) => q.depth - p.depth);
  const surface = el('g', { class: 'ex-surface-mesh' });
  for (const q of quads) {
    const p = poly(q.pts);
    p.setAttribute('fill', heat((q.mean - zMin) / (zMax - zMin || 1)));
    surface.append(p);
  }
  svg.append(surface);

  // The pillars: a point at each quote, its value on hover — the data, not the faces between.
  const points = el('g', { class: 'ex-surface-points' });
  for (let j = 0; j < ny; j++)
    for (let i = 0; i < nx; i++) {
      const v = mesh.values[j][i];
      if (v === null) continue;
      const p = P(i, j, v);
      const c = el('circle', { cx: p[0].toFixed(1), cy: p[1].toFixed(1), r: 2.4 });
      c.append(el('title', {}, `${mesh.yLabels[j]} · ${mesh.xLabels[i]}: ${v.toFixed(mesh.valueDigits)}`));
      points.append(c);
    }
  svg.append(points);

  // The axes' labels along the front edges of the floor.
  const frontY = backY === 0 ? ny - 1 : 0, frontX = backX === 0 ? nx - 1 : 0;
  const labels = el('g', { class: 'ex-surface-label' });
  const stepX = Math.ceil(nx / 12), stepY = Math.ceil(ny / 12);
  for (let i = 0; i < nx; i += stepX) {
    const p = P(i, frontY + (frontY === 0 ? -0.6 : 0.6), zMin);
    labels.append(el('text', { x: p[0].toFixed(1), y: (p[1] + 4).toFixed(1), 'text-anchor': 'middle' }, mesh.xLabels[i]));
  }
  for (let j = 0; j < ny; j += stepY) {
    const p = P(frontX + (frontX === 0 ? -0.6 : 0.6), j, zMin);
    labels.append(el('text', { x: p[0].toFixed(1), y: (p[1] + 4).toFixed(1), 'text-anchor': 'middle' }, mesh.yLabels[j]));
  }
  const xt = P((nx - 1) / 2, frontY + (frontY === 0 ? -1.6 : 1.6), zMin);
  labels.append(el('text', { x: xt[0].toFixed(1), y: (xt[1] + 4).toFixed(1), 'text-anchor': 'middle', class: 'ex-surface-axis' }, mesh.xTitle));
  const yt = P(frontX + (frontX === 0 ? -1.8 : 1.8), (ny - 1) / 2, zMin);
  labels.append(el('text', { x: yt[0].toFixed(1), y: (yt[1] + 4).toFixed(1), 'text-anchor': 'middle', class: 'ex-surface-axis' }, mesh.yTitle));
  svg.append(labels);
}
