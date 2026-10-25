// Prints the core comparison (ADR-0350 section 15) side by side: one column per result file, one
// table per scenario, each figure the median over the run's rounds. A file is what
// tools/cdp-core.mjs wrote for one label (a core on a host); pass them in the order the columns are
// wanted, e.g. the C# core's two hosts first and the Script Core's beside them.
//
//   node tools/summarize-core.mjs results/20261025-core/*.json
//
// ms are from the input to what the user sees: "dom" when the change landed in the DOM, "frame" at
// the animation frame after it. A frame's p95 is over the frames of a sustained phase. Bytes are what
// the circuit's connection carried, after compression; "-" where the host has no circuit.
import fs from 'node:fs';
import path from 'node:path';

const files = process.argv.slice(2);
if (files.length === 0) {
    console.error('usage: node tools/summarize-core.mjs <result.json>...');
    process.exit(2);
}
const runs = files.map((f) => ({ file: path.basename(f), ...JSON.parse(fs.readFileSync(f, 'utf8')) }));

const median = (values) => {
    const v = values.filter((x) => typeof x === 'number').sort((a, b) => a - b);
    if (v.length === 0) return null;
    return v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2;
};
const get = (o, keys) => keys.reduce((x, k) => (x == null ? undefined : x[k]), o);
const figure = (run, scenario, keys) => {
    const s = run.scenarios.find((x) => x.scenario === scenario);
    if (!s || s.failed) return null;
    return median(s.rounds.map((r) => get(r, keys)));
};
const failures = (run, scenario) => {
    const s = run.scenarios.find((x) => x.scenario === scenario);
    if (!s) return [];
    if (s.failed) return [`run failed: ${s.failed}`];
    const out = [];
    const walk = (o, at) => {
        for (const [k, v] of Object.entries(o ?? {})) {
            if (k.endsWith('Failed')) out.push(`${at}${k}: ${v}`);
            else if (v && typeof v === 'object') walk(v, `${at}${k}.`);
        }
    };
    s.rounds.forEach((r, i) => walk(r, `round ${i} `));
    return out;
};

const ms = (v) => (v === null ? '-' : v.toFixed(1));
const bytes = (v) => (v === null ? '-' : Math.round(v).toLocaleString('en-US'));

const gridRows = (sizes, ticks, burst) => {
    const rows = [];
    for (const n of sizes) {
        const s = ['sizes', String(n)];
        const at = n.toLocaleString('en-US');
        rows.push([`push all ${at} rows: dom ms`, [...s, 'pushAll', 'value', 'dom'], ms]);
        rows.push([`push all ${at} rows: frame ms`, [...s, 'pushAll', 'value', 'frame'], ms]);
        rows.push([`push all ${at} rows: bytes`, [...s, 'pushAll', 'bytes'], bytes]);
        rows.push([`push one of ${at}: dom ms`, [...s, 'pushOne', 'value', 'dom'], ms]);
        rows.push([`push one of ${at}: bytes`, [...s, 'pushOne', 'bytes'], bytes]);
        for (const k of ticks) {
            rows.push([`tick ${k} of ${at}: dom ms p50`, [...s, `tick${k}`, 'once', 'dom', 'p50'], ms]);
            rows.push([`tick ${k} of ${at} per frame: frame p95`, [...s, `tick${k}`, 'sustained', 'frames', 'p95'], ms]);
            rows.push([`tick ${k} of ${at} per frame: lag ms`, [...s, `tick${k}`, 'sustained', 'lagDom'], ms]);
            rows.push([`tick ${k} of ${at} per frame: bytes/tick`, [...s, `tick${k}`, 'sustained', 'bytesPerTick'], bytes]);
        }
    }
    rows.push(['mount over the largest: dom ms', ['mount', 'value', 'dom'], ms]);
    rows.push(['mount over the largest: bytes', ['mount', 'bytes'], bytes]);
    rows.push([`${burst} arrows in one task: dom ms`, ['keys', 'burst', 'dom'], ms]);
    rows.push([`${burst} arrows in one task: bytes`, ['keys', 'burst', 'bytes'], bytes]);
    rows.push(['an arrow per frame: frame p95', ['keys', 'perFrame', 'frames', 'p95'], ms]);
    rows.push(['an arrow per frame: lag ms', ['keys', 'perFrame', 'lagDom'], ms]);
    return [...rows, ...scrollRows];
};
const scrollRows = [
    ['scroll 37 px per frame: frame p95', ['scroll', 'slow', 'frames', 'p95'], ms],
    ['scroll 37 px per frame: lag ms', ['scroll', 'slow', 'lagDom'], ms],
    ['fling 50 rows per frame: frame p95', ['scroll', 'fling', 'frames', 'p95'], ms],
    ['fling 50 rows per frame: lag ms', ['scroll', 'fling', 'lagDom'], ms],
];
const opRows = (name, label, bytesKey) => [
    [`${label}: dom ms p50`, [name, 'dom', 'p50'], ms],
    [`${label}: dom ms p95`, [name, 'dom', 'p95'], ms],
    [`${label}: frame ms p50`, [name, 'frame', 'p50'], ms],
    [`${label}: bytes`, [name, bytesKey], bytes],
];

// The options each run used, as core-bench.js reports them; every size and tick any run measured gets a row.
const used = runs.flatMap((r) => r.scenarios.map((s) => s.opts).filter((o) => o));
const union = (key) => [...new Set(used.flatMap((o) => o[key]))].sort((a, b) => a - b);
const bursts = [...new Set(used.map((o) => o.burst))];
const tables = {
    grid: gridRows(union('sizes'), union('ticks'), bursts.join(' or ')),
    template: gridRows(union('sizes'), union('ticks'), bursts.join(' or ')),
    sheet: [...opRows('editAll', 'edit A1 (20,000 recalculated)', 'bytesPerEdit'), ...opRows('editOne', 'edit L1 (nothing reads it)', 'bytesPerEdit'), ...scrollRows],
    pivot: [...opRows('amend', 'one trade amended', 'bytesPerOp'), ...opRows('layout', 'columns swapped', 'bytesPerOp'), ...scrollRows],
};

const width = 44;
const col = 14;
for (const [scenario, rows] of Object.entries(tables)) {
    const present = runs.filter((r) => r.scenarios.some((s) => s.scenario === scenario));
    if (present.length === 0) continue;
    console.log(`\n/core/${scenario}`);
    console.log(''.padEnd(width) + present.map((r) => r.label.padStart(col)).join(''));
    for (const [label, keys, format] of rows) {
        console.log(label.padEnd(width) + present.map((r) => format(figure(r, scenario, keys)).padStart(col)).join(''));
    }
    for (const r of present) {
        const s = r.scenarios.find((x) => x.scenario === scenario);
        for (const f of failures(r, scenario)) console.log(`  ${r.label}: ${f}`);
        for (const c of s.complaints ?? []) console.log(`  ${r.label}: ${c.kind}: ${c.text.split('\n')[0]}`);
    }
}
console.log('');
for (const r of runs) {
    const env = r.scenarios.find((s) => s.env)?.env;
    console.log(`${r.label}: ${r.file}, ${r.base}, ${r.at}, rounds ${r.options?.rounds ?? '?'}${env ? `, ${env.userAgent}, ${env.cores} cores, dpr ${env.devicePixelRatio}` : ''}`);
}
