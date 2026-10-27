import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The chart, the core, the data and the grid's adapter are read from their sources, as tsconfig.json reads them, so the adapter's tests
// run against this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/chart': fileURLToPath(new URL('../chart/src/index.ts', import.meta.url)),
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
            '@ex-grid/react': fileURLToPath(new URL('../react/src/index.ts', import.meta.url)),
        },
    },
});
