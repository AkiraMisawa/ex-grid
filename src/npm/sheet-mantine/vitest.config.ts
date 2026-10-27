import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The ExGrid Wrapper, the adapters, the Sheet, the core, the data and the engine are read from their sources, as
// tsconfig.json reads them, so the Wrapper's tests run against this checkout's without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
            '@ex-grid/react': fileURLToPath(new URL('../react/src/index.ts', import.meta.url)),
            '@ex-grid/mantine': fileURLToPath(new URL('../mantine/src/index.ts', import.meta.url)),
            '@ex-grid/sheet': fileURLToPath(new URL('../sheet/src/index.ts', import.meta.url)),
            '@ex-grid/sheet-engine': fileURLToPath(new URL('../sheet-engine/src/index.ts', import.meta.url)),
            '@ex-grid/sheet-react': fileURLToPath(new URL('../sheet-react/src/index.ts', import.meta.url)),
        },
    },
});
