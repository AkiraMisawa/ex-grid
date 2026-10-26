import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The core is read from its sources, as tsconfig.json reads it, so the chart's tests run against the core
// of this checkout without a build.
export default defineConfig({
    resolve: {
        alias: { '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)) },
    },
});
