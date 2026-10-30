import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The Board, the products it lays out and the data they read are read from their sources, as the bundle reads them,
// so the bridge's tests run against this checkout without a build.
const at = (path: string): string => fileURLToPath(new URL(path, import.meta.url));

export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/blazor-shared': at('../blazor/src/shared.ts'),
            '@ex-grid/board': at('../board/src/index.ts'),
            '@ex-grid/chart': at('../chart/src/index.ts'),
            '@ex-grid/chart-blazor': at('../chart-blazor/src/razor-slots.ts'),
            '@ex-grid/core': at('../core/src/index.ts'),
            '@ex-grid/data': at('../data/src/index.ts'),
            '@ex-grid/pivot': at('../pivot/src/index.ts'),
            '@ex-grid/pivot-blazor': at('../pivot-blazor/src/for-board.ts'),
            '@ex-grid/pivot-engine': at('../pivot-engine/src/index.ts'),
        },
    },
});
