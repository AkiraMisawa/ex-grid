// The Docs Site's React Examples (ADR-0350 section 12, ADR-0110): one page, built ahead of time into the site's
// static files, that runs the Example its address names in a frame of its own, so React never runs inside the
// Blazor app. The Examples are the .tsx files beside the Blazor ones under ExGrid.Docs/Examples, and the packages
// are built from source, as the React DemoHost builds them, so a change to either is in the next build of the site.
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const at = (path: string): string => fileURLToPath(new URL(path, import.meta.url));

export default defineConfig({
    // Relative addresses: the site is served under /ex-grid/ on GitHub Pages and at / locally.
    base: './',
    resolve: {
        alias: {
            '@ex-grid/core': at('../../src/npm/core/src/index.ts'),
            '@ex-grid/react': at('../../src/npm/react/src/index.ts'),
            '@ex-grid/data': at('../../src/npm/data/src/index.ts'),
            '@ex-grid/sheet-engine': at('../../src/npm/sheet-engine/src/index.ts'),
            '@ex-grid/sheet': at('../../src/npm/sheet/src/index.ts'),
            '@ex-grid/sheet-react': at('../../src/npm/sheet-react/src/index.ts'),
            '@ex-grid/data-xlsx': at('../../src/npm/data-xlsx/src/index.ts'),
            '@ex-grid/sheet-xlsx': at('../../src/npm/sheet-xlsx/src/index.ts'),
            '@ex-grid/mantine': at('../../src/npm/mantine/src/index.ts'),
            '@ex-grid/sheet-mantine': at('../../src/npm/sheet-mantine/src/index.ts'),
            '@ex-grid/mui': at('../../src/npm/mui/src/index.ts'),
        },
        // The Examples and the packages' sources sit outside this folder; one React for all, or its hooks see two,
        // and one of each design system, or a Wrapper's components read another provider than the frame's; the
        // Examples' other imports are this project's too.
        dedupe: ['react', 'react-dom', '@mantine/core', '@mantine/hooks', '@mui/material', '@emotion/react', '@emotion/styled', 'temporal-polyfill'],
    },
    oxc: { jsx: { runtime: 'automatic' } },
    server: { fs: { allow: [at('../..')] } },
    build: {
        target: 'es2022',
        // The shipped stylesheet as written: its defaults are light-dark() pairs, which a lower CSS target rewrites into
        // variables that only an ancestor's color-scheme of light or dark sets, so under the page's own scheme the
        // colours would resolve to nothing. light-dark() is in Chrome and Edge from 123.
        cssTarget: ['chrome123', 'edge123', 'firefox120', 'safari17.5'],
        // Into the site's own static files, where the Docs project's build puts them (ExGrid.Docs.csproj).
        outDir: at('../ExGrid.Docs/wwwroot/react'),
        emptyOutDir: true,
        // ExSheet, its engine and the grid are one chunk, shared by every Sheet Example and fetched once.
        chunkSizeWarningLimit: 1024,
        rolldownOptions: {
            // Mantine and MUI mark their modules "use client" for React Server Components; the frame runs only on the
            // client, so the directive has nothing to keep, and its warning, one per module, would bury any other.
            onwarn(warning, warn) {
                if (warning.code === 'MODULE_LEVEL_DIRECTIVE' && warning.message.includes('"use client"')) return;
                warn(warning);
            },
        },
    },
});
