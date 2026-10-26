import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The core, and the data it reads, are read from their sources, as tsconfig.json reads them, so the adapter's
// tests run against the core of this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
        },
    },
});
