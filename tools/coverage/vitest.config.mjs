// `npm run coverage`: every package's layers 1 and 2 in one Vitest run, each package as a project under its own
// configuration, with V8's coverage of the shipped sources (ADR-0041). One run, not one per package, because a
// package's tests read its neighbours' sources (the Script ExSheet's tests run the core's), and the figure counts
// every test against every shipped line, as ReportGenerator's merge does for the .NET suites. CI runs this in place
// of `npm test`; `summary.mjs` turns its report into the table per product.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

const workspace = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
// The packages `npm test -w packages` runs: the workspaces under packages/, as the root's package.json lists them.
const packages = JSON.parse(fs.readFileSync(path.join(workspace, 'package.json'), 'utf8')).workspaces.filter(
    (entry) => entry.startsWith('packages/'),
);

export default defineConfig({
    root: workspace,
    test: {
        projects: packages,
        coverage: {
            provider: 'v8',
            // What ships: each package's src/. Never its tests, a differential's harness, or a sample.
            include: ['packages/*/src/**/*.{ts,tsx}'],
            exclude: ['**/*.d.ts', '**/*.test.{ts,tsx}'],
            reporter: ['json-summary', 'text-summary', 'html'],
            reportsDirectory: 'coverage/ts',
        },
    },
});
