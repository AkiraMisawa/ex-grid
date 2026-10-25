// Runs serve.mjs's /check in a browser already listening for CDP: the TypeScript engine's answers
// compared with the C# engine's, cell by cell (client/src/check-page.ts). Writes what it found and exits
// 1 on any mismatch. Serve a published EngineHost, not `ts`: the check needs both Workers.
//
//   node tools/cdp-check.mjs <base-url>
//
// CDP_HOST and CDP_PORT name the browser's debugging endpoint; RESULTS_DIR where the JSON goes.
import fs from 'node:fs';
import path from 'node:path';
import { readWhenSet } from './cdp-page.mjs';

const [base] = process.argv.slice(2);
if (!base) {
    console.error('usage: node tools/cdp-check.mjs <base-url>');
    process.exit(2);
}
const url = `${base.replace(/\/$/, '')}/check`;
const result = await readWhenSet(url, 'window.checkResult', 10);
if (result === undefined) {
    console.error(`${url} answered nothing in 10 minutes`);
    process.exit(1);
}
const at = new Date().toISOString();
const file = path.join(process.env.RESULTS_DIR ?? path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'results'),
    `${at.replace(/[-:]/g, '').replace(/\..*/, '')}-check.json`);
fs.writeFileSync(file, JSON.stringify({ base, at, ...result }, null, 2));
if (result.failed) console.log(`FAILED: ${result.failed}`);
else console.log(`${JSON.stringify(result.compared)}: ${result.mismatchCount} mismatches; wrote ${file}`);
process.exit(result.failed || result.mismatchCount > 0 ? 1 : 0);
