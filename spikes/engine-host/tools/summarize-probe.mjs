// Prints the Probe's engine steps side by side, one column per runtime (EngineProbe on the JIT,
// tools/cdp-probe.mjs in the Worker on the interpreter and AOT): each step's median ms over its runs,
// the first fifth of them left out, which is the JIT's warm-up and the interpreter's first pass.
//
//   node tools/summarize-probe.mjs results/…/*probe-jit.json results/…/*probe-interp.json results/…/*probe-aot.json
import fs from 'node:fs';

const files = process.argv.slice(2);
if (files.length === 0) {
    console.error('usage: node tools/summarize-probe.mjs <probe.json>...');
    process.exit(2);
}
const runs = files.map((f) => JSON.parse(fs.readFileSync(f, 'utf8')));
const median = (values) => {
    const v = [...values].sort((a, b) => a - b);
    return v.length % 2 ? v[(v.length - 1) / 2] : (v[v.length / 2 - 1] + v[v.length / 2]) / 2;
};
const steps = [...new Set(runs.flatMap((r) => Object.keys(r.steps)))];
const width = 54;
console.log(' '.repeat(width) + runs.map((r) => r.runtime.padStart(10)).join(''));
for (const step of steps) {
    const cells = runs.map((r) => {
        const times = r.steps[step];
        if (!times) return '-'.padStart(10);
        return median(times.slice(Math.floor(times.length / 5))).toFixed(1).padStart(10);
    });
    console.log(step.padEnd(width) + cells.join(''));
}
