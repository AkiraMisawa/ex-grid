// Bundles the two Workers (.NET's and the TypeScript engine's), the TypeScript engine for the page's own
// thread, the two pages, the probe's page and the
// check's page into dist/ (ES modules with source maps). The Worker's import of
// /_framework/dotnet.js stays a runtime import: it is the published .NET build's, served beside dist/.
import { build } from 'esbuild';
import path from 'node:path';

const here = path.dirname(new URL(import.meta.url).pathname);
await build({
    entryPoints: ['engine-worker', 'ts-engine-worker', 'ts-engine-page', 'sheet-page', 'pivot-page', 'probe-page', 'check-page'].map((name) => path.join(here, 'src', `${name}.ts`)),
    outdir: path.join(here, 'dist'),
    bundle: true,
    format: 'esm',
    platform: 'browser',
    target: 'es2022',
    sourcemap: true,
    // Minified, as a Consumer's bundler would ship it, so the bytes recorded are a production page's.
    minify: true,
    logLevel: 'warning',
});
