// Builds one package of the workspace (ADR-0350, section 13), run from the package's directory:
//
//   1. tsc writes the declarations of src/ into temp/types;
//   2. esbuild bundles src/index.ts into dist/index.js, an ES module with its source map;
//   3. API Extractor rolls the declarations up into dist/index.d.ts and compares the package's public
//      surface with its report, etc/<name>.api.md.
//
// The report is the counterpart of a NuGet package's PublicAPI.Unshipped.txt (ADR-0201): an export
// that is not in it, or that is in it and gone, fails the build, and so does an exported member
// without a doc comment (ae-undocumented). `npm run api` builds with --local, which writes the report
// instead of comparing it; read its diff, as a line written by the analyzer is read, because a line
// you did not mean is a member made public by mistake.
import { build } from 'esbuild';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { Extractor, ExtractorConfig } from '@microsoft/api-extractor';

const local = process.argv.includes('--local');
const packageDir = process.cwd();
const manifest = JSON.parse(fs.readFileSync(path.join(packageDir, 'package.json'), 'utf8'));
const require = createRequire(import.meta.url);

fs.rmSync(path.join(packageDir, 'temp'), { recursive: true, force: true });
fs.rmSync(path.join(packageDir, 'dist'), { recursive: true, force: true });

const tsc = path.join(path.dirname(require.resolve('typescript/package.json')), 'bin/tsc');
execFileSync(process.execPath, [tsc, '-p', 'tsconfig.build.json'], {
    cwd: packageDir,
    stdio: 'inherit',
});

await build({
    entryPoints: [path.join(packageDir, 'src/index.ts')],
    outfile: path.join(packageDir, 'dist/index.js'),
    bundle: true,
    format: 'esm',
    platform: 'browser',
    target: 'es2022',
    sourcemap: true,
    // A package's dependencies stay imports of their own; only its own sources are bundled.
    external: [
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- npm: a manifest without the field has none
        ...Object.keys(manifest.dependencies ?? {}),
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- npm: a manifest without the field has none
        ...Object.keys(manifest.peerDependencies ?? {}),
    ],
    logLevel: 'warning',
});

if (local) {
    fs.mkdirSync(path.join(packageDir, 'etc'), { recursive: true });
}
const config = ExtractorConfig.loadFileAndPrepare(path.join(packageDir, 'api-extractor.json'));
const result = Extractor.invoke(config, { localBuild: local, showVerboseMessages: false });
if (!result.succeeded) {
    console.error(
        `${manifest.name}: API Extractor reported ${result.errorCount} error(s) and ${result.warningCount} warning(s).` +
        (result.apiReportChanged && !local
            ? ' The public surface differs from etc/ — run `npm run api` and read the diff (ADR-0201).'
            : ''));
    process.exitCode = 1;
}
