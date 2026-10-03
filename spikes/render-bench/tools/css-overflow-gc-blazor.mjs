// The GC check through Blazor (docs/research/css-decided-overflow.md, §3b). After a bench
// run with ?hold=1, the CSS mode is still on screen: this measures idle frames as left, after
// the page's own window.gc(), and after CDP's HeapProfiler.collectGarbage — to tell
// "departed cells are still reachable" from "the collection was not thorough". Spike code.
//
//   node tools/css-overflow-gc-blazor.mjs "overflow=Css&scenario=ScrollFling&hold=1"
// (Chromium on :9222 started with --js-flags=--expose-gc; the tab is the one cdp-run opened.)
const list = await (await fetch('http://127.0.0.1:9222/json/list')).json();
const t = list.find(x => x.type === 'page' && x.url.includes(process.argv[2]));
await fetch('http://127.0.0.1:9222/json/activate/' + t.id);
const ws = new WebSocket(t.webSocketDebuggerUrl);
await new Promise(r => ws.addEventListener('open', r, { once: true }));
let id = 0; const pend = new Map();
ws.addEventListener('message', ev => { const m = JSON.parse(ev.data); if (pend.has(m.id)) { pend.get(m.id)(m); pend.delete(m.id); } });
const send = (method, params = {}) => new Promise(r => { const i = ++id; pend.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
const measure = `new Promise(res => { const out = []; const ch = new MessageChannel(); let mark = 0, n = 0;
  ch.port1.onmessage = () => { out.push(performance.now() - mark); if (out.length === 200) { out.sort((a,b)=>a-b); res(out[100]); } };
  const tick = () => { mark = performance.now(); ch.port2.postMessage(0); if (++n < 200) requestAnimationFrame(tick); }; requestAnimationFrame(tick); })`;
const ev = async (e) => (await send('Runtime.evaluate', { expression: e, awaitPromise: true, returnByValue: true })).result.result.value;
console.log('mode now', await ev(`document.querySelectorAll('.window .c.hs, .window .c.hx').length + ' hs cells, ' + document.querySelectorAll('.window .c').length + ' cells'`));
console.log('idle p50, as left', await ev(measure));
await ev('window.gc(), 0'); console.log('idle p50 after window.gc()', await ev(measure));
await send('HeapProfiler.enable'); await send('HeapProfiler.collectGarbage');
console.log('idle p50 after CDP collectGarbage', await ev(measure));
process.exit(0);
