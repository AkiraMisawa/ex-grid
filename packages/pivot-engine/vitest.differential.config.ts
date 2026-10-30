import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

// The differential runs against the C# engine (ADR-0370, section 2): `npm run differential`, with
// EXGRID_PIVOT_ORACLE naming the command that starts dotnet/tools/pivot-oracle's `serve` (differential/README.md).
export default defineConfig({
    resolve: {
        alias: { '@ex-grid/data': fileURLToPath(new URL('../data/src/index.ts', import.meta.url)) },
    },
    test: {
        include: ['differential/**/*.differential.ts'],
        testTimeout: 600_000,
    },
});
