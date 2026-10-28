import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The chart, the grid core and the data they read are read from their sources, as the bundle reads them, so the
// bridge's tests run against this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
            '@ex-grid/chart': fileURLToPath(new URL('../chart/src/index.ts', import.meta.url)),
        },
    },
});
