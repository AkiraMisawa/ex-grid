// The TypeScript engine's Worker (ADR-0370 section 6, the third arm): ts-engine/host.ts serving the
// Worker's scope. serve.mjs serves it at engine-worker.js's path when it is started with `ts`.

import { serve, type EnginePort } from './ts-engine/host';

serve(globalThis as unknown as EnginePort, performance.now());
