import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The core, and the data it reads, are read from their sources, as the bundle reads them, so the bridge's tests
// run against the core of this checkout without a build.
export default defineConfig({
    // The Shared Script's stamp, which its build writes in (ADR-0411).
    define: { EX_SHARED_SHA256: JSON.stringify('the-shared-script') },
    resolve: {
        alias: {
            '@ex-grid/blazor-shared': fileURLToPath(new URL('src/shared.ts', import.meta.url)),
            '@ex-grid/core': fileURLToPath(new URL('../core/src/index.ts', import.meta.url)),
            '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)),
        },
    },
});
