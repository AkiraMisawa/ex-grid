// A loopback TCP proxy that delays every chunk, in both directions, by half the round
// trip it is set to (ADR-0021's owed measurement; ED-22, SRV-5). It sits between the
// browser and the Blazor Server host, so the WebSocket carrying the circuit is delayed
// exactly as the page's own requests are — the browser's network throttling is not
// reliably applied to a WebSocket, which is why this exists. Order is kept per
// direction: a chunk is never released before one that arrived earlier.
//
//   node latency-proxy.mjs <listenPort> <targetPort> <controlPort>
//
// The round trip starts at 0. POST http://localhost:<controlPort>/?rtt=150 sets it for
// every chunk that arrives after.
import http from 'node:http';
import net from 'node:net';

const [listenPort, targetPort, controlPort] = process.argv.slice(2).map(Number);
let halfTripMs = 0;

const delayed = (from, to) => {
    let releaseAt = 0;
    // One queue per direction and one timer draining it from the head. A timer per chunk
    // does not keep order: Node fires timers of the same duration in the order they were
    // set, but two chunks due at the same moment through timers of different durations —
    // the second arriving a millisecond later with a millisecond less to wait — can fire
    // the other way round, and the circuit's WebSocket then reads a torn message
    // ("Incomplete message").
    const queue = [];
    let timer = null;
    const drain = () => {
        timer = null;
        while (queue.length > 0 && queue[0].at <= Date.now()) {
            const next = queue.shift();
            if (next.end) {
                to.end();
            } else {
                to.write(next.chunk);
            }
        }
        if (queue.length > 0) {
            timer = setTimeout(drain, Math.max(0, queue[0].at - Date.now()));
        }
    };
    from.on('data', (chunk) => {
        // Never earlier than the chunk before it, even if the delay was lowered since.
        releaseAt = Math.max(Date.now() + halfTripMs, releaseAt);
        queue.push({ chunk, at: releaseAt });
        if (timer === null) {
            drain();
        }
    });
    // The end goes behind the last chunk, through the same queue.
    from.on('end', () => {
        queue.push({ end: true, at: Math.max(Date.now(), releaseAt) });
        if (timer === null) {
            drain();
        }
    });
};

net.createServer((client) => {
    const upstream = net.connect(targetPort, '127.0.0.1');
    delayed(client, upstream);
    delayed(upstream, client);
    const close = () => {
        client.destroy();
        upstream.destroy();
    };
    client.on('error', close);
    upstream.on('error', close);
}).listen(listenPort, '127.0.0.1');

http.createServer((request, response) => {
    const rtt = Number(new URL(request.url, 'http://localhost').searchParams.get('rtt'));
    if (request.method === 'POST' && Number.isFinite(rtt) && rtt >= 0) {
        halfTripMs = rtt / 2;
        response.end(`rtt=${rtt}\n`);
    } else {
        response.statusCode = 400;
        response.end('POST /?rtt=<milliseconds>\n');
    }
}).listen(controlPort, '127.0.0.1');
