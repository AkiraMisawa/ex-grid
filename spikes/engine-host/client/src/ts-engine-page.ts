// The TypeScript engine on the page's own thread: ts-engine/host.ts serving one end of a MessageChannel,
// whose other end engine-client.ts holds in place of a Worker. Every answer then costs the page its
// frame for as long as the engine takes, which is what serve.mjs's `ts-page` measures: whether a
// TypeScript engine needs a Worker at all.

import { serve } from './ts-engine/host';

export const start = (port: MessagePort): void => {
    serve(port as unknown as Parameters<typeof serve>[0], performance.now());
    port.start();
};
