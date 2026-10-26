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
        },
        // The Examples and the packages' sources sit outside this folder; one React for all, or its hooks see two.
        dedupe: ['react', 'react-dom'],
    },
    oxc: { jsx: { runtime: 'automatic' } },
    server: { fs: { allow: [at('../..')] } },
    build: {
        target: 'es2022',
        // Into the site's own static files, where the Docs project's build puts them (ExGrid.Docs.csproj).
        outDir: at('../ExGrid.Docs/wwwroot/react'),
        emptyOutDir: true,
        // ExSheet, its engine and the grid are one chunk, shared by every Sheet Example and fetched once.
        chunkSizeWarningLimit: 1024,
    },
});
