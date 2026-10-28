import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The Sheet, the grid it draws through, its engine and the data they read are read from their sources, as the bundle
// reads them, so the bridge's tests run against this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/blazor-shared': fileURLToPath(new URL('../blazor/src/shared.ts', import.meta.url)),
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
            '@ex-grid/sheet': fileURLToPath(new URL('../sheet/src/index.ts', import.meta.url)),
            '@ex-grid/sheet-engine': fileURLToPath(new URL('../sheet-engine/src/index.ts', import.meta.url)),
        },
    },
});
