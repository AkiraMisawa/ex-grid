import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The pivot, the grid it draws through and the data they read are read from their sources, as the bundle reads
// them, so the bridge's tests run against this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/blazor-shared': fileURLToPath(new URL('../blazor/src/shared.ts', import.meta.url)),
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
            '@ex-grid/pivot': fileURLToPath(new URL('../pivot/src/index.ts', import.meta.url)),
            '@ex-grid/pivot-engine': fileURLToPath(new URL('../pivot-engine/src/index.ts', import.meta.url)),
        },
    },
});
