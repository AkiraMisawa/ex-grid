// /probe: EngineHost's Probe (EngineProbe/Probe.cs) run in the Worker, on whichever build serve.mjs
// serves, so its engine steps are timed on the interpreter or AOT as they are on the JIT. The answer is
// window.probeResult; tools/cdp-probe.mjs reads it.

import { EngineClient } from './engine-client';

const main = async (): Promise<void> => {
    const params = new URLSearchParams(location.search);
    const runtime = params.get('runtime');
    const repeats = Number(params.get('repeats'));
    if (runtime === null || !Number.isSafeInteger(repeats) || repeats < 1) throw new Error('/probe?runtime=<label>&repeats=<n>');
    const engine = new EngineClient('/dist/engine-worker.js');
    await engine.ready;
    const answer = await engine.call<object>('ProbeRun', runtime, repeats);
    (window as unknown as { probeResult: object }).probeResult = answer.value;
};

main().catch((error: unknown) => {
    console.error(error);
    (window as unknown as { probeResult: object }).probeResult = { failed: String(error) };
});
