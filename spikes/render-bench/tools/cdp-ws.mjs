// M2's circuit half: drives Bench.Server's live-tick page over the Chrome DevTools Protocol and
// reads what each tick puts on the circuit. No npm deps (Node 24 has global fetch + WebSocket).
//
// Usage:
//   CDP_PORT=9391 node tools/cdp-ws.mjs <url> <out.json> [ticks] [warmup]
//
// Per configuration (columns 20/50, k 1/5/40, 3 changed cells or all) it clicks Tick A and Tick B
// in turn, one click at a time. A tick is over when the client has received exactly one more
// JS.RenderBatch message and sent one more OnRenderCompleted — no wait on a clock. For each tick it
// records:
//   - payload: the RenderBatch message as Chrome reports it (Network.webSocketFrameReceived), which
//     is the SignalR (BlazorPack) message after any per-message decompression;
//   - wire: what Kestrel wrote to the WebSocket's TCP connection during the tick (GET /api/wire,
//     Bench.Server's WireCounters): the bytes on the wire, compressed when compression is on.
// The client's own messages (the click's event, OnRenderCompleted) are recorded as payloads.

const CDP = `http://127.0.0.1:${process.env.CDP_PORT ?? 9222}`;
const URL_ = process.argv[2] ?? 'http://127.0.0.1:5199/';
const OUT = process.argv[3] ?? 'ws.json';
const TICKS = Number(process.argv[4] ?? 50);
const WARMUP = Number(process.argv[5] ?? 5);
const WIRE = new URL('/api/wire', URL_).toString();

const fs = await import('node:fs');

let nextId = 1;
const pending = new Map();
const frames = []; // { dir: 'in' | 'out', bytes, kind }
const counts = new Map();
let handshake = null;
const logs = [];

const KINDS = ['JS.RenderBatch', 'OnRenderCompleted', 'DispatchBrowserEvent', 'BeginInvokeDotNetFromJS', 'EndInvokeJSFromDotNet',
  'JS.BeginInvokeJS', 'JS.EndInvokeDotNet', 'UpdateRootComponents', 'StartCircuit', 'ConnectCircuit', 'JS.AttachComponent',
  'JS.Error', 'JS.Log', 'JS.SetPlatformPolicy', 'ReceiveByteArray', 'OnLocationChanged'];

function classify(buffer) {
  const text = buffer.toString('latin1', 0, Math.min(buffer.length, 96));
  for (const kind of KINDS) if (text.includes(kind)) return kind;
  return buffer.length <= 8 ? 'small (ping or ack)' : 'other';
}

function count(dir, kind) { return counts.get(`${dir}:${kind}`) ?? 0; }

function rpc(ws, method, params = {}) {
  const id = nextId++;
  ws.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    setTimeout(() => { if (pending.delete(id)) reject(new Error(`timeout: ${method}`)); }, 60000);
  });
}

async function evaluate(ws, expression) {
  const r = await rpc(ws, 'Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
  if (r.exceptionDetails) throw new Error('eval threw: ' + JSON.stringify(r.exceptionDetails.exception ?? r.exceptionDetails));
  return r.result?.value;
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Waits for a condition the page or the circuit will reach; the deadline only turns a hang into
// an error, it is not what the result depends on.
async function until(condition, what, deadlineMs = 30000) {
  const start = Date.now();
  while (!(await condition())) {
    if (Date.now() - start > deadlineMs) throw new Error(`never reached: ${what}`);
    await sleep(2);
  }
}

async function wire() {
  const res = await fetch(WIRE);
  return await res.json();
}

function median(values) {
  const s = [...values].sort((a, b) => a - b);
  return s.length ? s[Math.floor(s.length / 2)] : 0;
}

async function main() {
  const version = await (await fetch(`${CDP}/json/version`)).json();
  const res = await fetch(`${CDP}/json/new?about:blank`, { method: 'PUT' });
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
      case 'Network.webSocketHandshakeResponseReceived':
        handshake = msg.params.response.headers;
        break;
      case 'Network.webSocketFrameReceived':
      case 'Network.webSocketFrameSent': {
        const r = msg.params.response;
        const dir = msg.method.endsWith('Received') ? 'in' : 'out';
        const buffer = r.opcode === 2 ? Buffer.from(r.payloadData, 'base64') : Buffer.from(r.payloadData, 'utf8');
        const kind = classify(buffer);
        frames.push({ dir, bytes: buffer.length, kind });
        counts.set(`${dir}:${kind}`, count(dir, kind) + 1);
        break;
      }
      case 'Runtime.exceptionThrown':
        logs.push(`[EXCEPTION] ${msg.params.exceptionDetails.exception?.description ?? msg.params.exceptionDetails.text}`);
        break;
      case 'Runtime.consoleAPICalled':
        logs.push(`[console.${msg.params.type}] ${msg.params.args.map((a) => a.value ?? a.description ?? a.type).join(' ')}`);
        break;
    }
  });
  await rpc(ws, 'Runtime.enable');
  await rpc(ws, 'Network.enable', { maxTotalBufferSize: 0, maxResourceBufferSize: 0 });
  await rpc(ws, 'Page.enable');
  await rpc(ws, 'Page.navigate', { url: URL_ });

  await until(async () => (await evaluate(ws, `document.getElementById('status')?.textContent ?? ''`)) === 'not configured', 'the circuit rendered the page');
  console.log('BOOTED; WebSocket extensions: ' + (handshake?.['Sec-WebSocket-Extensions'] ?? handshake?.['sec-websocket-extensions'] ?? '(none)'));

  let wsConnection = null;
  const results = [];
  for (const changed of [3, -1]) {
    for (const cols of [20, 50]) {
      for (const k of [1, 5, 40]) {
        const before = await wire();
        const batches0 = count('in', 'JS.RenderBatch');
        await evaluate(ws, `(() => {
          const set = (id, v) => { const el = document.getElementById(id); el.value = v; el.dispatchEvent(new Event('change', { bubbles: true })); };
          set('cols', '${cols}'); set('k', '${k}'); set('changed', '${changed}');
          document.getElementById('configure').click();
        })()`);
        const status = `configured: ${cols} cols, k ${k}, changed ${changed}`;
        await until(async () => count('in', 'JS.RenderBatch') > batches0
          && (await evaluate(ws, `document.getElementById('status').textContent`)) === status, status);
        await until(async () => count('out', 'OnRenderCompleted') >= count('in', 'JS.RenderBatch'), 'configure rendered');
        if (wsConnection === null) {
          // The WebSocket is the connection that wrote the most while the page was configured.
          const after = await wire();
          let best = 0;
          for (const c of after) {
            const was = before.find((b) => b.id === c.id)?.written ?? 0;
            if (c.written - was > best) { best = c.written - was; wsConnection = c.id; }
          }
          console.log(`the circuit's connection: ${wsConnection} (${best} bytes during the first configure)`);
        }

        const series = { A: [], B: [] };
        for (let i = 0; i < WARMUP + TICKS; i++) {
          for (const [variant, button] of [['A', 'tick-a'], ['B', 'tick-b']]) {
            const batches = count('in', 'JS.RenderBatch');
            const completed = count('out', 'OnRenderCompleted');
            const outBefore = frames.filter((f) => f.dir === 'out').length;
            const w0 = (await wire()).find((c) => c.id === wsConnection)?.written ?? 0;
            await evaluate(ws, `document.getElementById('${button}').click()`);
            await until(() => count('in', 'JS.RenderBatch') >= batches + 1 && count('out', 'OnRenderCompleted') >= completed + 1, `${variant} tick rendered`);
            if (count('in', 'JS.RenderBatch') !== batches + 1) throw new Error(`${variant}: more than one render batch for one tick`);
            const w1 = (await wire()).find((c) => c.id === wsConnection)?.written ?? 0;
            const batch = [...frames].reverse().find((f) => f.dir === 'in' && f.kind === 'JS.RenderBatch');
            const sent = frames.filter((f) => f.dir === 'out').slice(outBefore).reduce((sum, f) => sum + f.bytes, 0);
            if (i >= WARMUP) series[variant].push({ payload: batch.bytes, wire: w1 - w0, clientPayload: sent });
          }
        }
        const summary = (s) => ({
          payloadMedian: median(s.map((x) => x.payload)), payloadMin: Math.min(...s.map((x) => x.payload)), payloadMax: Math.max(...s.map((x) => x.payload)),
          wireMedian: median(s.map((x) => x.wire)), wireMin: Math.min(...s.map((x) => x.wire)), wireMax: Math.max(...s.map((x) => x.wire)),
          clientPayloadMedian: median(s.map((x) => x.clientPayload)),
        });
        const row = { cols, k, changed, ticks: TICKS, A: summary(series.A), B: summary(series.B), samples: series };
        results.push(row);
        console.log(`${cols} cols, k ${k}, ${changed < 0 ? 'all' : changed} cells | A payload ${row.A.payloadMedian} wire ${row.A.wireMedian} | B payload ${row.B.payloadMedian} wire ${row.B.wireMedian} | client ${row.A.clientPayloadMedian}/${row.B.clientPayloadMedian}`);
      }
    }
  }

  fs.writeFileSync(OUT, JSON.stringify({
    browser: version.Browser, url: URL_, ticks: TICKS, warmup: WARMUP,
    webSocketExtensions: handshake?.['Sec-WebSocket-Extensions'] ?? handshake?.['sec-websocket-extensions'] ?? null,
    frameKinds: Object.fromEntries(counts), results,
  }, null, 2));
  console.log(`wrote ${OUT}`);
  console.log(`=== BROWSER LOG (${logs.length}) ===`);
  for (const l of logs.slice(-30)) console.log('  ' + l);
  await fetch(`${CDP}/json/close/${target.id}`).catch(() => {});
  process.exit(logs.some((l) => l.startsWith('[EXCEPTION]') || l.startsWith('[console.error]')) ? 2 : 0);
}

main().catch((e) => {
  console.error('DRIVER FAILED: ' + e.message);
  for (const l of logs.slice(-30)) console.error('  ' + l);
  process.exit(3);
});
