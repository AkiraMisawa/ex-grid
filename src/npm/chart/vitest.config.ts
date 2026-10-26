import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// @ex-grid/data is read from its sources, as tsconfig.json reads it, so the chart's tests run against the
// data of this checkout without a build.
export default defineConfig({
    resolve: {
        alias: { '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)) },
    },
});
