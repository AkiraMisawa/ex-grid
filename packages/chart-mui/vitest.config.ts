import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The chart, its React adapter, the grid's MUI Wrapper and what they stand on are read from their sources, as
// tsconfig.json reads them, so the Wrapper's tests run against this checkout's without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/chart-react': fileURLToPath(new URL('../chart-react/src/index.ts', import.meta.url)),
            '@ex-grid/chart': fileURLToPath(new URL('../chart/src/index.ts', import.meta.url)),
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
            '@ex-grid/mui': fileURLToPath(new URL('../mui/src/index.ts', import.meta.url)),
            '@ex-grid/react': fileURLToPath(new URL('../react/src/index.ts', import.meta.url)),
        },
    },
});
