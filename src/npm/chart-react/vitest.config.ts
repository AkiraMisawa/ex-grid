import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The chart and the core are read from their sources, as tsconfig.json reads them, so the adapter's tests
// run against this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/chart': fileURLToPath(new URL('../chart/src/index.ts', import.meta.url)),
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
        },
    },
});
