import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

const source = (name: string): string => fileURLToPath(new URL(`../${name}/src/index.ts`, import.meta.url));

// The Board and what it is built on are read from their sources, as tsconfig.json reads them, so the adapter's tests
// run against this checkout without a build.
export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/board': source('board'),
            '@ex-grid/chart': source('chart'),
            '@ex-grid/core': source('core'),
            '@ex-grid/data': source('data'),
            '@ex-grid/pivot': source('pivot'),
            '@ex-grid/pivot-engine': source('pivot-engine'),
        },
    },
});
