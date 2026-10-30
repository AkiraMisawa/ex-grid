import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The packages a Board reads through are read from their sources, as tsconfig.json reads them, so the Board's
// tests run against this checkout without a build.
const source = (name: string): string => fileURLToPath(new URL(`../${name}/src/index.ts`, import.meta.url));

export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/chart': source('chart'),
            '@ex-grid/core': source('core'),
            '@ex-grid/data': source('data'),
            '@ex-grid/pivot': source('pivot'),
            '@ex-grid/pivot-engine': source('pivot-engine'),
        },
    },
});
