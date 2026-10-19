// Summarises the Input Order's runs (cdp-input.mjs) into one line per configuration and build:
// medians over every round of every pass, and how many rounds of each phase timed out.
//
//   node tools/summarize-input.mjs <dir-of-json>
import fs from 'node:fs';
import path from 'node:path';

const dir = process.argv[2];
const groups = new Map();
for (const name of fs.readdirSync(dir).filter((n) => n.endsWith('.json')).sort()) {
    const r = JSON.parse(fs.readFileSync(path.join(dir, name), 'utf8'));
    const [build, ...rest] = r.label.split('-');
    const config = rest.filter((p) => !/^p\d+$/.test(p)).join('-');
    const key = `${config}\t${build}`;
    if (!groups.has(key)) groups.set(key, { config, build, rounds: [], complaints: 0, runFailed: 0 });
    const g = groups.get(key);
    g.rounds.push(...(r.rounds ?? []));
    g.complaints += r.complaints?.length ?? 0;
    if (r.failed) g.runFailed++;
}
const median = (values) => {
    const v = values.filter((x) => typeof x === 'number').sort((a, b) => a - b);
    if (v.length === 0) return NaN;
    return v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2;
};
const p95 = (values) => {
    const v = values.filter((x) => typeof x === 'number').sort((a, b) => a - b);
    return v.length ? v[Math.min(v.length - 1, Math.floor(0.95 * v.length))] : NaN;
};
const f = (x) => (Number.isNaN(x) ? 'nan' : x.toFixed(1));
const cols = ['config', 'build', 'n', 'type ms', 'type esc', 'press ms', 'press p95', 'menu burst', 'burst fail', 'open p50', 'open p95', 'close p50', 'close p95', 'fails', 'complaints'];
const widths = [12, 7, 3, 8, 9, 9, 10, 11, 11, 9, 9, 10, 10, 6, 11];
const line = (cells) => cells.map((c, i) => (i < 2 ? String(c).padEnd(widths[i]) : String(c).padStart(widths[i]))).join(' ');
console.log(line(cols));
const order = ['server-0ms', 'server-60ms', 'wasm'];
for (const g of [...groups.values()].sort((a, b) => order.indexOf(a.config) - order.indexOf(b.config) || (a.build === 'main' ? -1 : 1))) {
    const R = g.rounds;
    const opens = R.flatMap((e) => e.menuOpenMs ?? []);
    const closes = R.flatMap((e) => e.menuCloseMs ?? []);
    const otherFails = R.filter((e) => e.typingFailed || e.pressesFailed || e.menuStepsFailed).length + g.runFailed;
    console.log(line([
        g.config, g.build, R.length,
        f(median(R.map((e) => e.typingMs))), f(median(R.map((e) => e.typingEscapeMs))),
        f(median(R.map((e) => e.pressesMs))), f(p95(R.map((e) => e.pressesMs))),
        f(median(R.map((e) => e.menuBurstMs))), `${R.filter((e) => e.menuBurstFailed).length}/${R.length}`,
        f(median(opens)), f(p95(opens)), f(median(closes)), f(p95(closes)),
        otherFails, g.complaints,
    ]));
}
