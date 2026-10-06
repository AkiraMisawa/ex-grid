import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import assert from 'node:assert/strict';

const root = path.resolve('verification/2026-10-06-macos-pivot-boundary-bench');
const proxy = process.env.BOUNDARY_URL || 'http://127.0.0.1:5895';
const direct = process.env.BOUNDARY_DIRECT || 'http://127.0.0.1:5894';
const control = process.env.BOUNDARY_CONTROL || 'http://127.0.0.1:5896';
const leavesList = (process.env.BOUNDARY_LEAVES || '2976,100000,400000').split(',').map(Number);
const rtts = (process.env.BOUNDARY_RTTS || '0,150').split(',').map(Number);
const repeats = Number(process.env.BOUNDARY_REPEATS || 7);
const actions = [['initial', 0, true], ['update', 1, true], ['update', 1, false],
  ['update', 1000, true], ['update', 1000, false], ['expand', 0, true],
  ['expand', 0, true], ['sort', 0, true]];
const errors = [];
const output = { started: new Date().toISOString(), cpu: os.cpus()[0], platform: os.platform(),
  arch: os.arch(), memory: os.totalmem(), repeats, records: 1000000, samples: [], checks: [] };
const expected = new Map();
const outputPath = path.join(root, 'raw', process.env.BOUNDARY_OUTPUT || 'measurements.json');
function save() { fs.writeFileSync(outputPath, JSON.stringify(output, null, 2)); }
async function latency(rtt) {
  const response = await fetch(`${control}/?rtt=${rtt}`, { method: 'POST' });
  assert.ok(response.ok);
}
const browser = await chromium.launch({ channel: 'chrome', headless: true });
output.browser = browser.version();
try {
  for (const leaves of leavesList) for (const rtt of rtts) {
    // Each case gets a fresh document. Boot is at RTT 0 and is outside the sample.
    for (const mode of (rtt === 0 ? ['A', 'B'] : ['B', 'A'])) {
      await latency(0);
      const context = await browser.newContext({ viewport: { width: 1000, height: 1100 } });
      const page = await context.newPage();
      page.on('pageerror', error => errors.push(String(error)));
      page.on('console', message => {
        if (message.type() === 'error' || message.type() === 'warning') errors.push(`${message.type()}: ${message.text()}`);
      });
      const cdp = await context.newCDPSession(page);
      await cdp.send('Performance.enable');
      await page.goto(proxy);
      await page.waitForFunction(() => window.bench?.ready);
      // Warm the shared parse/render/delta/sort paths on the small shape before timings.
      await page.evaluate(mode => window.bench.prepare(mode, 2976, 1000000), mode);
      for (const [action, batch, visible] of actions)
        await page.evaluate(args => window.bench.run(...args), [action, batch, visible]);
      await page.evaluate(() => window.bench.verify());
      await latency(rtt);
      for (let repeat = 0; repeat < repeats; repeat++) {
        const generated = await page.evaluate(args => window.bench.prepare(...args), [mode, leaves, 1000000]);
        await page.evaluate(() => window.bench.memory());
        for (let i = 0; i < actions.length; i++) {
          const [action, batch, visible] = actions[i];
          const result = await page.evaluate(args => window.bench.run(...args), [action, batch, visible]);
          const windowHash = await page.evaluate(() => window.bench.windowHash());
          const key = `${leaves}:${repeat}:${i}`;
          if (expected.has(key)) assert.equal(windowHash, expected.get(key), `A/B or RTT mismatch at ${key}`);
          else expected.set(key, windowHash);
          output.samples.push({ leaves, records: 1000000, rtt, mode, repeat, index: i, generatedMs: generated.generatedMs,
            windowHash, ...result });
          // Every operation has an independent fresh-fold check on the first repetition.
          if (repeat === 0) output.checks.push({ leaves, rtt, mode, repeat, index: i,
            ...await page.evaluate(() => window.bench.verify()) });
          save();
        }
        const fresh = await page.evaluate(() => window.bench.verify());
        const clientMemory = await page.evaluate(() => window.bench.memory());
        const runtimeMemory = await cdp.send('Runtime.getHeapUsage');
        const serverMemory = await (await fetch(`${direct}/api/memory`)).json();
        output.checks.push({ leaves, rtt, mode, repeat, fresh, clientMemory, runtimeMemory, serverMemory });
        assert.deepEqual(errors, [], 'Unexpected browser console/runtime message');
        save();
        console.log(JSON.stringify({ leaves, rtt, mode, repeat, clientMemory }));
      }
      await context.close();
    }
  }
  output.finished = new Date().toISOString();
  output.errors = errors;
  save();
} catch (error) {
  output.errors = errors;
  output.failure = String(error);
  save();
  console.error(errors);
  throw error;
} finally {
  await latency(0);
  await browser.close();
}
