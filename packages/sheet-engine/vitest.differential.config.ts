import { defineConfig } from 'vitest/config';

// The differential runs against the C# engine (ADR-0370, section 2): `npm run differential`, with
// EXGRID_SHEET_ORACLE naming the command that starts dotnet/tools/sheet-oracle's `serve` (differential/README.md).
export default defineConfig({
    test: {
        include: ['differential/**/*.differential.ts'],
        testTimeout: 600_000,
    },
});
