import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The engine and the file layer are read from their sources, as tsconfig.json reads them, so the tests run against the
// engine and the file layer of this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/data-xlsx': fileURLToPath(new URL('../data-xlsx/src/index.ts', import.meta.url)),
            '@ex-grid/sheet-engine': fileURLToPath(new URL('../sheet-engine/src/index.ts', import.meta.url)),
        },
    },
});
