import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The React adapter, the core and the data are read from their sources, as tsconfig.json reads them, so the
// Wrapper's tests run against this checkout's without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
            '@ex-grid/react': fileURLToPath(new URL('../react/src/index.ts', import.meta.url)),
        },
    },
});
