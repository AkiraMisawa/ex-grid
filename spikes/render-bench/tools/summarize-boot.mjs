// Prints what tools/cdp-boot.mjs recorded side by side: one column per result file, one table per
// scenario. Times are medians over the rounds, ms from navigation; bytes are the first round's (the cache
// is off, so every round downloads the same), split by what they carry.
//
//   node tools/summarize-boot.mjs results/…/*-boot-*.json
//
// A page that records its own start (the Engine Host spike's window.engineBoot) adds its phases: the
// Worker made (or, on the page, the engine's channel), the engine ready in it, the document open in the engine,
// its first Window answered, painted.
import fs from 'node:fs';
import path from 'node:path';

const files = process.argv.slice(2);
if (files.length === 0) {
    console.error('usage: node tools/summarize-boot.mjs <boot.json>...');
    process.exit(2);
}
const runs = files.map((f) => ({ file: path.basename(f), ...JSON.parse(fs.readFileSync(f, 'utf8')) }));
const median = (values) => {
    const v = values.filter((x) => typeof x === 'number').sort((a, b) => a - b);
    if (v.length === 0) return null;
    return v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2;
};
const kind = (url) => {
    const at = new URL(url).pathname;
    if (/^\/_framework\/(dotnet\.|icudt)/.test(at) || /\/_framework\/blazor\./.test(at)) return 'runtime';
    if (/^\/_framework\/(System|Microsoft|netstandard|mscorlib)\./.test(at)) return 'framework assemblies';
    if (at.startsWith('/_framework/')) return 'product and app assemblies';
    return 'page';
};
const rows = [
    ['first cell with text: ms', (s) => median(s.runs.map((r) => r.firstCellAt))],
    ['  Worker made', (s) => median(s.runs.map((r) => r.engineBoot?.boot.createdAt))],
    ['  the engine ready in it (.NET started)', (s) => median(s.runs.map((r) => r.engineBoot?.boot.readyAt))],
    ['  document open (engine ms)', (s) => median(s.runs.map((r) => r.engineBoot?.openEngineMs))],
    ['  document open', (s) => median(s.runs.map((r) => r.engineBoot?.openedAt))],
    ['  first Window answered', (s) => median(s.runs.map((r) => r.engineBoot?.firstWindowAt))],
    ['bytes downloaded', (s) => s.runs[0].bytes],
    ...['runtime', 'framework assemblies', 'product and app assemblies', 'page'].map((k) =>
        [`  ${k}`, (s) => s.runs[0].files.filter((f) => kind(f.url) === k).reduce((sum, f) => sum + f.bytes, 0)]),
    ['requests', (s) => s.runs[0].files.length],
    ['complaints', (s) => s.runs.reduce((sum, r) => sum + r.complaints.length, 0)],
];
const scenarios = [...new Set(runs.flatMap((r) => r.scenarios.map((s) => s.scenario)))];
const width = 42;
for (const scenario of scenarios) {
    console.log(`/core/${scenario}`);
    console.log(' '.repeat(width) + runs.map((r) => r.label.padStart(24)).join(''));
    for (const [name, read] of rows) {
        const cells = runs.map((r) => {
            const s = r.scenarios.find((x) => x.scenario === scenario);
            const v = s ? read(s) : null;
            return (v === null || v === undefined ? '-' : Math.round(v).toLocaleString('en-US')).padStart(24);
        });
        console.log(name.padEnd(width) + cells.join(''));
    }
    console.log('');
}
for (const r of runs) console.log(`${r.label}: ${r.file}, ${r.base}, ${r.at}`);
