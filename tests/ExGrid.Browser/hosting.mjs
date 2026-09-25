import os from 'node:os';
import path from 'node:path';

// Which host layer 3 drives (ADR-0019): the standalone WebAssembly DemoHost by default,
// the Blazor Server host with EXGRID_HOSTING=server (Definition of Done §24). Shared by
// playwright.config.mjs and the fixture, both of which are loaded in every process of a
// run, so everything here is computed the same way wherever it is read.
export const HOSTING = process.env.EXGRID_HOSTING ?? 'wasm';
if (HOSTING !== 'wasm' && HOSTING !== 'server') {
    throw new Error(`EXGRID_HOSTING is "${HOSTING}"; it is "wasm" (the default) or "server".`);
}
export const SERVER = HOSTING === 'server';

// The URL the browser is pointed at. On Server that is the latency proxy, which the
// host sits behind; the host itself listens a thousand ports up.
export const BASE_URL = process.env.EXGRID_BASE_URL
    ?? (SERVER ? 'http://localhost:5298' : 'http://localhost:5299');
const port = Number(new URL(BASE_URL).port);
export const HOST_PORT = SERVER ? port + 1000 : port;
// Where the tests set the proxy's round trip (latency-proxy.mjs).
export const LATENCY_CONTROL_URL = `http://localhost:${port + 2000}`;

// The Server host's log, which CON-6 reads per test: a circuit's unhandled exception is
// written there, not to the browser console (it is on WebAssembly). One file per port,
// so two checkouts on two ports never read each other's.
export const HOST_LOG = path.join(os.tmpdir(), `exgrid-server-host-${HOST_PORT}.log`);
