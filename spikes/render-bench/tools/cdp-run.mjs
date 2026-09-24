// Minimal Chrome DevTools Protocol driver for the render bench — no npm deps
// (Node 24 has global fetch + WebSocket).
//
// Usage:
//   nix develop .#browser -c node tools/cdp-run.mjs <url> [iterations] [waitSeconds] [step] [cols] [buttonLabel] [save]
//
// Pass `save` as the last argument to press "Save results to the server" after a
// completed run, so the JSON lands in results/ like a manual run's does.
//
// Headless Chromium must already be listening on :9222, e.g.
//   "$CHROMIUM_BIN" --headless=new --no-sandbox --remote-debugging-port=9222 --user-data-dir=/tmp/p about:blank
//
// Headless renders in software, so the ABSOLUTE numbers are not comparable to a real
// browser. Use this to reproduce exceptions and to compare modes; take real numbers in
// Chrome or Edge (see README.md).

const CDP = 'http://127.0.0.1:9222';
const URL_ = process.argv[2] ?? 'http://127.0.0.1:5199/';
const ITER = process.argv[3] ?? '40';
const WAIT_S = Number(process.argv[4] ?? 90);
const STEP = process.argv[5] ?? null;
const COLS = process.argv[6] ?? null;
const BTN = process.argv[7] ?? 'Measure all modes';
const SAVE = process.argv[8] === 'save';

const logs = [];
let nextId = 1;
const pending = new Map();

function rpc(ws, method, params = {}) {
  const id = nextId++;
  ws.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    setTimeout(() => { if (pending.delete(id)) reject(new Error(`timeout: ${method}`)); }, 60000);
  });
}

async function evaluate(ws, expression, awaitPromise = false) {
  const r = await rpc(ws, 'Runtime.evaluate', {
    expression, returnByValue: true, awaitPromise, allowUnsafeEvalBlockedByCSP: true,
  });
  if (r.exceptionDetails) {
    throw new Error('eval threw: ' + JSON.stringify(r.exceptionDetails.exception ?? r.exceptionDetails));
  }
  return r.result?.value;
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const res = await fetch(`${CDP}/json/new?${encodeURIComponent(URL_)}`, { method: 'PUT' });
  if (!res.ok) throw new Error(`open tab failed: ${res.status} ${await res.text()}`);
  const target = await res.json();

  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    ws.addEventListener('open', resolve, { once: true });
    ws.addEventListener('error', reject, { once: true });
  });

  ws.addEventListener('message', (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.id && pending.has(msg.id)) {
      const { resolve, reject } = pending.get(msg.id);
      pending.delete(msg.id);
      msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result);
      return;
    }
    switch (msg.method) {
      case 'Runtime.exceptionThrown': {
        const d = msg.params.exceptionDetails;
        logs.push(`[EXCEPTION] ${d.exception?.description ?? d.text}`);
        break;
      }
      case 'Runtime.consoleAPICalled': {
        const text = msg.params.args.map((a) => a.value ?? a.description ?? a.type).join(' ');
        logs.push(`[console.${msg.params.type}] ${text}`);
        break;
      }
      case 'Log.entryAdded': {
        const e = msg.params.entry;
        logs.push(`[log.${e.level}] ${e.text}${e.url ? ' @ ' + e.url : ''}`);
        break;
      }
    }
  });

  await rpc(ws, 'Runtime.enable');
  await rpc(ws, 'Log.enable');
  await rpc(ws, 'Page.enable');

  // Wait for Blazor to boot: the run button only exists once the component rendered.
  let booted = false;
  for (let i = 0; i < 60; i++) {
    const found = await evaluate(ws, `!![...document.querySelectorAll('button')].find(b => b.textContent.includes('Measure all'))`);
    if (found) { booted = true; break; }
    await sleep(1000);
  }
  console.log(booted ? 'BOOTED' : 'NOT BOOTED (blazor never rendered)');
  if (!booted) { dump(); process.exit(1); }

  // Lower the iteration count so the headless run finishes quickly.
  await evaluate(ws, `
    (() => {
      const inputs = [...document.querySelectorAll('.controls input')];
      const set = (i, v) => { if (inputs[i] && v !== 'null') { inputs[i].value = v; inputs[i].dispatchEvent(new Event('change', { bubbles: true })); } };
      set(3, '${ITER}');
      set(2, '${STEP}');
      set(1, '${COLS}');
      return inputs.map(i => i.value);
    })()
  `);
  await sleep(500);

  console.log('CLICKING RUN');
  await evaluate(ws, `[...document.querySelectorAll('button')].find(b => b.textContent.includes('${BTN}')).click()`);

  // Poll until the results table or the error box appears.
  let outcome = 'TIMEOUT';
  for (let i = 0; i < WAIT_S; i++) {
    await sleep(1000);
    const state = await evaluate(ws, `
      (() => ({
        err: document.querySelector('.err-box')?.innerText ?? null,
        rows: [...document.querySelectorAll('table.results tbody tr')].map(r => r.innerText.replace(/\\n|\\t/g, ' | ')),
        running: !![...document.querySelectorAll('button')].find(b => b.textContent.includes('measuring')),
        blazorErr: getComputedStyle(document.getElementById('blazor-error-ui')).display !== 'none'
      }))()
    `);
    if (state.err) { console.log('\\n=== ON-PAGE ERROR BOX ===\\n' + state.err); outcome = 'ERROR_BOX'; break; }
    if (state.blazorErr) { console.log('\\n=== BLAZOR UNHANDLED ERROR UI SHOWN ==='); outcome = 'BLAZOR_ERROR_UI'; break; }
    if (!state.running && state.rows.length > 0) {
      console.log('\\n=== RESULTS ===');
      state.rows.forEach((r) => console.log('  ' + r));
      outcome = 'COMPLETED';
      break;
    }
  }
  if (SAVE && outcome === 'COMPLETED') {
    await evaluate(ws, `[...document.querySelectorAll('button')].find(b => b.textContent.includes('Save results')).click()`);
    let saved = 'not confirmed';
    for (let i = 0; i < 15; i++) {
      await sleep(1000);
      const label = await evaluate(ws, `[...document.querySelectorAll('button')].map(b => b.textContent).find(t => t.includes('saved') || t.includes('failed')) ?? ''`);
      if (label) { saved = label; break; }
    }
    console.log('SAVE: ' + saved);
  }
  console.log('\\nOUTCOME: ' + outcome);
  dump();
  process.exit(outcome === 'COMPLETED' ? 0 : 2);
}

function dump() {
  console.log('\\n=== BROWSER LOG (' + logs.length + ' entries) ===');
  for (const l of logs.slice(-60)) console.log('  ' + l);
}

main().catch((e) => { console.error('DRIVER FAILED: ' + e.message); dump(); process.exit(3); });
