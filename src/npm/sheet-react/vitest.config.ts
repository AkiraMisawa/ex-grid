import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The Sheet, the core and the engine are read from their sources, as tsconfig.json reads them, so the adapter's
// tests run against this checkout's without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
            '@ex-grid/sheet': fileURLToPath(new URL('../sheet/src/index.ts', import.meta.url)),
            '@ex-grid/sheet-engine': fileURLToPath(new URL('../sheet-engine/src/index.ts', import.meta.url)),
        },
    },
});
