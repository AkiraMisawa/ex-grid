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
// every chunk that arrives after, and GET answers what it is set to.
//
// Every byte between the browser and the host passes through here, so this is also the one
// place that can say the two have stopped talking. GET /quiet?for=<ms>&within=<ms> answers
// once nothing is held here or unsent on a socket, in either direction, and nothing has
// crossed for `for` ms: 200 then, or 504 when that has not happened within `within` ms, with
// what kept crossing (circuitQuiet in fixtures.mjs).
import http from 'node:http';
import net from 'node:net';

const [listenPort, targetPort, controlPort] = process.argv.slice(2).map(Number);
let halfTripMs = 0;

// What /quiet reads. A chunk is held from the moment it arrives here until it is written on,
// and a chunk written on can still sit unsent in its socket's buffer.
let held = 0;
let lastCrossed = Date.now();
const crossed = { fromBrowser: 0, fromHost: 0 };
const sockets = new Set();

// SignalR's keep-alive: each end pings the other every 15 s when it has sent nothing else —
// the default of both HubOptions.KeepAliveInterval and the client's
// keepAliveIntervalInMilliseconds, which the Server host leaves as they are. The two ends ping
// on phases of their own, so two pings can be as little as half that apart. A window well
// under that is met at most one window after a ping: a ping delays the answer and never
// withholds it. A window this long or longer is refused rather than left to chance.
const LONGEST_QUIET_WINDOW_MS = 5_000;

const delayed = (from, to, direction) => {
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
            held--;
            lastCrossed = Date.now();
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
        held++;
        crossed[direction]++;
        lastCrossed = Date.now();
        // Never earlier than the chunk before it, even if the delay was lowered since.
        releaseAt = Math.max(Date.now() + halfTripMs, releaseAt);
        queue.push({ chunk, at: releaseAt });
        if (timer === null) {
            drain();
        }
    });
    // The end goes behind the last chunk, through the same queue.
    from.on('end', () => {
        held++;
        queue.push({ end: true, at: Math.max(Date.now(), releaseAt) });
        if (timer === null) {
            drain();
        }
    });
};

const track = (socket) => {
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
};

net.createServer((client) => {
    const upstream = net.connect(targetPort, '127.0.0.1');
    track(client);
    track(upstream);
    delayed(client, upstream, 'fromBrowser');
    delayed(upstream, client, 'fromHost');
    const close = () => {
        client.destroy();
        upstream.destroy();
    };
    client.on('error', close);
    upstream.on('error', close);
}).listen(listenPort, '127.0.0.1');

// Answers once the wire has been quiet for `quietFor` ms, or once `within` ms have passed.
function whenQuiet(quietFor, within, response) {
    const started = Date.now();
    const before = { ...crossed };
    const check = () => {
        const now = Date.now();
        const unsent = held + [...sockets].filter((socket) => socket.writableLength > 0).length;
        const still = now - lastCrossed;
        if (unsent === 0 && still >= quietFor) {
            response.end(`quiet for ${still} ms, after ${now - started} ms\n`);
            return;
        }
        if (now - started >= within) {
            response.statusCode = 504;
            response.end(`not quiet for ${quietFor} ms within ${within} ms: `
                + `${crossed.fromBrowser - before.fromBrowser} chunks from the browser and `
                + `${crossed.fromHost - before.fromHost} from the host crossed meanwhile, `
                + `${unsent} held or unsent now, the last crossing ${still} ms ago\n`);
            return;
        }
        const wait = unsent > 0 ? 5 : quietFor - still;
        setTimeout(check, Math.max(1, Math.min(wait, within - (now - started))));
    };
    check();
}

http.createServer((request, response) => {
    const url = new URL(request.url, 'http://localhost');
    if (url.pathname === '/quiet') {
        const quietFor = Number(url.searchParams.get('for'));
        const within = Number(url.searchParams.get('within'));
        if (request.method !== 'GET' || !(quietFor >= 0) || !(within >= 0)) {
            response.statusCode = 400;
            response.end('GET /quiet?for=<milliseconds>&within=<milliseconds>\n');
        } else if (quietFor >= LONGEST_QUIET_WINDOW_MS) {
            response.statusCode = 400;
            response.end(`a quiet window of ${quietFor} ms is not under ${LONGEST_QUIET_WINDOW_MS} ms, `
                + 'and the keep-alive pings could keep it from ever being met\n');
        } else {
            whenQuiet(quietFor, within, response);
        }
        return;
    }
    const rtt = Number(url.searchParams.get('rtt'));
    if (request.method === 'GET') {
        response.end(`rtt=${halfTripMs * 2}\n`);
    } else if (request.method === 'POST' && Number.isFinite(rtt) && rtt >= 0) {
        halfTripMs = rtt / 2;
        response.end(`rtt=${rtt}\n`);
    } else {
        response.statusCode = 400;
        response.end('POST /?rtt=<milliseconds>\n');
    }
}).listen(controlPort, '127.0.0.1');
