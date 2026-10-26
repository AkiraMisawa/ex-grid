import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

const at = (path: string): string => fileURLToPath(new URL(path, import.meta.url));

// The pivot and what it is built on are read from their sources, as tsconfig.json reads them, so the
// adapter's tests run against this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': at('../core/src/index.ts'),
            '@ex-grid/data': at('../data/src/index.ts'),
            '@ex-grid/pivot-engine': at('../pivot-engine/src/index.ts'),
            '@ex-grid/pivot': at('../pivot/src/index.ts'),
        },
    },
});
