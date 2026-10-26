import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The core and the engine are read from their sources, as tsconfig.json reads them, so the Script Core's
// tests run against the core and the engine of this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/sheet-engine': fileURLToPath(new URL('../sheet-engine/src/index.ts', import.meta.url)),
        },
    },
});
