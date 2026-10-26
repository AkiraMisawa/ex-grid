// The React DemoHost builds the Script Core and its React adapter from source, as the Blazor hosts build
// ExGrid by project reference: a change to either is in the next build, with no package published between.
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const at = (path: string): string => fileURLToPath(new URL(path, import.meta.url));

export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': at('../../src/npm/core/src/index.ts'),
            '@ex-grid/react': at('../../src/npm/react/src/index.ts'),
            '@ex-grid/chart': at('../../src/npm/chart/src/index.ts'),
            '@ex-grid/chart-react': at('../../src/npm/chart-react/src/index.ts'),
            '@ex-grid/data': at('../../src/npm/data/src/index.ts'),
            '@ex-grid/pivot-engine': at('../../src/npm/pivot-engine/src/index.ts'),
            '@ex-grid/pivot': at('../../src/npm/pivot/src/index.ts'),
            '@ex-grid/pivot-react': at('../../src/npm/pivot-react/src/index.ts'),
            '@ex-grid/sheet-engine': at('../../src/npm/sheet-engine/src/index.ts'),
            '@ex-grid/sheet': at('../../src/npm/sheet/src/index.ts'),
            '@ex-grid/sheet-react': at('../../src/npm/sheet-react/src/index.ts'),
            '@ex-grid/board': at('../../src/npm/board/src/index.ts'),
            '@ex-grid/board-react': at('../../src/npm/board-react/src/index.ts'),
        },
        // The adapter's source sits outside this folder; one React for both, or its hooks see two.
        dedupe: ['react', 'react-dom'],
    },
    oxc: { jsx: { runtime: 'automatic' } },
    server: { fs: { allow: [at('../..')] } },
    build: { target: 'es2022', sourcemap: true },
});
