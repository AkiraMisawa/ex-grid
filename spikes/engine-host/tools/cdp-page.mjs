// Opens a page in a browser already listening for CDP and answers a global the page sets when it is done
// (window.probeResult, window.checkResult), polling for it; closes the page. Shared by cdp-probe.mjs and
// cdp-check.mjs; no npm dependencies. CDP_HOST and CDP_PORT name the browser's debugging endpoint.

const host = process.env.CDP_HOST ?? '127.0.0.1';
const CDP = `http://${host}:${process.env.CDP_PORT ?? 9222}`;

/** The value of <expression> once the page has set it, or undefined after <minutes>. */
export const readWhenSet = async (url, expression, minutes) => {
    const target = await (await fetch(`${CDP}/json/new?${encodeURIComponent(url)}`, { method: 'PUT' })).json();
    const ws = new WebSocket(target.webSocketDebuggerUrl.replace('127.0.0.1', host).replace('localhost', host));
    await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
    let nextId = 1;
    const pending = new Map();
    ws.onmessage = (event) => {
        const message = JSON.parse(event.data);
        if (message.id && pending.has(message.id)) {
            pending.get(message.id)(message.result);
            pending.delete(message.id);
        }
    };
    const evaluate = (source) => {
        const id = nextId++;
        ws.send(JSON.stringify({ id, method: 'Runtime.evaluate', params: { expression: source, returnByValue: true } }));
        return new Promise((resolve) => pending.set(id, (result) => resolve(result?.result?.value)));
    };
    let result;
    for (let i = 0; i < minutes * 120 && result === undefined; i++) {
        result = await evaluate(expression);
        if (result === undefined) await new Promise((resolve) => setTimeout(resolve, 500));
    }
    await fetch(`${CDP}/json/close/${target.id}`);
    ws.close();
    return result;
};
