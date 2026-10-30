// The React DemoHost builds the Script Core and its React adapter from source, as the Blazor hosts build
// ExGrid by project reference: a change to either is in the next build, with no package published between.
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const at = (path: string): string => fileURLToPath(new URL(path, import.meta.url));

export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': at('../../packages/core/src/index.ts'),
            '@ex-grid/react': at('../../packages/react/src/index.ts'),
            '@ex-grid/mantine': at('../../packages/mantine/src/index.ts'),
            '@ex-grid/mui': at('../../packages/mui/src/index.ts'),
            '@ex-grid/chart': at('../../packages/chart/src/index.ts'),
            '@ex-grid/chart-react': at('../../packages/chart-react/src/index.ts'),
            '@ex-grid/chart-mantine': at('../../packages/chart-mantine/src/index.ts'),
            '@ex-grid/chart-mui': at('../../packages/chart-mui/src/index.ts'),
            '@ex-grid/data': at('../../packages/data/src/index.ts'),
            '@ex-grid/pivot-engine': at('../../packages/pivot-engine/src/index.ts'),
            '@ex-grid/pivot': at('../../packages/pivot/src/index.ts'),
            '@ex-grid/pivot-react': at('../../packages/pivot-react/src/index.ts'),
            '@ex-grid/pivot-mantine': at('../../packages/pivot-mantine/src/index.ts'),
            '@ex-grid/pivot-mui': at('../../packages/pivot-mui/src/index.ts'),
            '@ex-grid/sheet-engine': at('../../packages/sheet-engine/src/index.ts'),
            '@ex-grid/sheet': at('../../packages/sheet/src/index.ts'),
            '@ex-grid/sheet-react': at('../../packages/sheet-react/src/index.ts'),
            '@ex-grid/data-xlsx': at('../../packages/data-xlsx/src/index.ts'),
            '@ex-grid/sheet-xlsx': at('../../packages/sheet-xlsx/src/index.ts'),
            '@ex-grid/board': at('../../packages/board/src/index.ts'),
            '@ex-grid/board-react': at('../../packages/board-react/src/index.ts'),
        },
        // The adapter's source sits outside this folder; one React for both, or its hooks see two, and one
        // Mantine and one MUI for the Wrapper and the page, or the Wrapper's components see no provider.
        dedupe: ['react', 'react-dom', '@mantine/core', '@mantine/hooks', '@mui/material', '@emotion/react', '@emotion/styled'],
    },
    oxc: { jsx: { runtime: 'automatic' } },
    server: { fs: { allow: [at('../..')] } },
    build: {
        target: 'es2022',
        // The shipped stylesheet as written: its defaults are light-dark() pairs, which a lower CSS target rewrites into
        // variables that only an ancestor's color-scheme of light or dark sets, so under the page's own scheme the
        // colours would resolve to nothing. light-dark() is in Chrome and Edge from 123.
        cssTarget: ['chrome123', 'edge123', 'firefox120', 'safari17.5'],
        sourcemap: true,
    },
});
