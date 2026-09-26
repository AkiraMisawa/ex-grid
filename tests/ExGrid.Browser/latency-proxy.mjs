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
    // Chunks waiting on a timer. A timer fires late, so a chunk whose own wait has already
    // run out must still go behind every chunk ahead of it: written at once while an
    // earlier one was pending, it overtook it, and the circuit's WebSocket read a torn
    // message ("Incomplete message") on a busy run.
    let pending = 0;
    from.on('data', (chunk) => {
        // Never earlier than the chunk before it, even if the delay was lowered since.
        releaseAt = Math.max(Date.now() + halfTripMs, releaseAt);
        const wait = releaseAt - Date.now();
        if (wait <= 0 && pending === 0) {
            to.write(chunk);
        } else {
            pending += 1;
            setTimeout(() => {
                pending -= 1;
                to.write(chunk);
            }, Math.max(0, wait));
        }
    });
    from.on('end', () => setTimeout(() => to.end(), Math.max(0, releaseAt - Date.now())));
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
