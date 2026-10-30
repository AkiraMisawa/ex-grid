import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

const at = (path: string): string => fileURLToPath(new URL(path, import.meta.url));

// The pivot, its React adapter, the grid's Mantine Wrapper and what they stand on are read from their sources, as
// tsconfig.json reads them, so the Wrapper's tests run against this checkout's without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': at('../core/src/index.ts'),
            '@ex-grid/data': at('../data/src/index.ts'),
            '@ex-grid/mantine': at('../mantine/src/index.ts'),
            '@ex-grid/pivot-engine': at('../pivot-engine/src/index.ts'),
            '@ex-grid/pivot-react': at('../pivot-react/src/index.ts'),
            '@ex-grid/pivot': at('../pivot/src/index.ts'),
            '@ex-grid/react': at('../react/src/index.ts'),
        },
    },
});
