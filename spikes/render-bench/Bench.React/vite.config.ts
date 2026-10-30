// The core comparison's React host builds the Script Core and its React adapter from source, as the React
// DemoHost does, so the numbers are the tree's.
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const at = (path: string): string => fileURLToPath(new URL(path, import.meta.url));

export default defineConfig({
    resolve: {
        alias: {
            '@ex-grid/core': at('../../../src/npm/core/src/index.ts'),
            '@ex-grid/react': at('../../../src/npm/react/src/index.ts'),
            '@ex-grid/data': at('../../../src/npm/data/src/index.ts'),
        },
        dedupe: ['react', 'react-dom'],
    },
    oxc: { jsx: { runtime: 'automatic' } },
    server: { fs: { allow: [at('../../..')] } },
    build: { target: 'es2022', cssTarget: ['chrome123'] },
});
