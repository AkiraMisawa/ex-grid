// Builds the script a Blazor adapter imports (ADR-0350 sections 7 and 9, ADR-0370 section 1, ADR-0411): the
// product's bridge and Script Core, read from their sources and bundled into one minified ES module with its source
// map, which the product's NuGet package ships: ExGrid's `ex-grid-core`, from src/npm/blazor, ExPivot's `ex-pivot`,
// from src/npm/pivot-blazor, ExSheet's `ex-sheet`, from src/npm/sheet-blazor, ExChart's `ex-chart-core`, from
// src/npm/chart-blazor, and ExBoard's `ex-board`, from src/npm/board-blazor. Nothing here is published to npm.
//
// `@ex-grid/core` and `@ex-grid/data` are bundled once, into the Shared Script ExGrid ships beside its bridge
// (`ex-grid-script.min.js`, from blazor/src/shared.ts), and every product's module imports them from it by one
// address, the plain one with the first twelve hex digits of the Shared Script's stamp as `?v=`, so a page with
// several products loads them once (ADR-0411). ExGrid's build writes it (--ships-shared); every other product's reads
// the same sources for the stamp, which its address carries and its module expects, and needs no build of it: two
// builds of the same sources by another esbuild still name one address.
//
// Each package asks for the files it was built with (ADR-0123): the module answers `stamp()`, the SHA-256 of
// every source it was bundled from and of the Shared Script's stamp, and the component compares it with the stamp it
// was built with before it attaches anything. The module answers another text when the Shared Script the browser
// handed it is not the one it was built with, so that one is refused the same way. Beside the module this writes `<name>.stamp.json`: the stamp, and the version the module's
// address carries, the first twelve hex digits of the shipped file's own SHA-256. Given --cs, it also writes both as
// the C# constants the product's build compiles (`ScriptCoreAsset`, in --namespace).
//
// Run from the adapter's workspace, as its `npm run build`, or by the product's build:
//
//   node ../scripts/build-blazor.mjs [--name <module>] [--out <directory>] [--stamp <file>] [--cs <file>] [--namespace <C# namespace>] [--ships-shared]
//
// The module and its map go to --out, dist/ unless given; the stamp to --stamp, beside them unless given. The ExGrid
// build puts the stamp and the constants outside what the package serves.
import { build } from 'esbuild';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = process.cwd();
const workspace = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const argument = (flag, otherwise) => {
    const at = process.argv.indexOf(flag);
    return at < 0 ? otherwise : process.argv[at + 1];
};
const out = path.resolve(argument('--out', path.join(here, 'dist')));
const name = argument('--name', 'ex-grid-core');
const namespace = argument('--namespace', 'ExGrid.Script');
const stampFile = path.resolve(argument('--stamp', path.join(out, `${name}.stamp.json`)));
const cs = argument('--cs', null);
const csFile = cs === null ? null : path.resolve(cs);
const shipsShared = process.argv.includes('--ships-shared');

/** The Shared Script's name, and where a product's module finds it: beside ExGrid's bridge, or in ExGrid's folder of
 * `_content/` from any other package's. */
const sharedName = 'ex-grid-script';
const sharedFolder = shipsShared ? './' : '../ExGrid/';

/** The packages are read from their sources, so the bundle is the checkout's code and needs no build of them. */
const alias = {
    '@ex-grid/core': path.join(workspace, 'core/src/index.ts'),
    '@ex-grid/data': path.join(workspace, 'data/src/index.ts'),
    '@ex-grid/pivot': path.join(workspace, 'pivot/src/index.ts'),
    '@ex-grid/chart': path.join(workspace, 'chart/src/index.ts'),
    '@ex-grid/pivot-engine': path.join(workspace, 'pivot-engine/src/index.ts'),
    '@ex-grid/sheet': path.join(workspace, 'sheet/src/index.ts'),
    '@ex-grid/sheet-engine': path.join(workspace, 'sheet-engine/src/index.ts'),
    '@ex-grid/board': path.join(workspace, 'board/src/index.ts'),
    // ExPivot's bridge for a C# Pivot Source, which ExBoard's asks a pivot over a source through too, and its Razor slots' kinds.
    '@ex-grid/pivot-blazor': path.join(workspace, 'pivot-blazor/src/for-board.ts'),
    // ExChart's Razor slots' kinds, which ExBoard's dresses its charts with.
    '@ex-grid/chart-blazor': path.join(workspace, 'chart-blazor/src/razor-slots.ts'),
};

const hashOf = (text) => createHash('sha256').update(text.replace(/\r\n/g, '\n'), 'utf8').digest('hex');

const common = {
    bundle: true,
    format: 'esm',
    platform: 'browser',
    target: 'es2022',
    alias,
    legalComments: 'none',
    logLevel: 'warning',
};

/** The SHA-256 of every source a build reads, in an order that never depends on the file system, by path relative
 * to the workspace and content, after what else the stamp is to cover. */
async function stampOf(settings, define, covered) {
    const probe = await build({ ...settings, write: false, metafile: true, define });
    const inputs = Object.keys(probe.metafile.inputs)
        .map(input => path.resolve(process.cwd(), input))
        .map(file => [path.relative(workspace, file).split(path.sep).join('/'), file])
        .toSorted(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0));
    const sources = inputs.map(([relative, file]) => `${relative}\n${fs.readFileSync(file, 'utf8')}`);
    return { stamp: hashOf([...covered, ...sources].join('\n')), count: inputs.length };
}

/** Builds one module into `outfile`, its map beside it naming each source by its place in the workspace; answers the
 * shipped text, which must answer `stamp`. */
async function bundle(settings, outfile, label, stamp, define) {
    await build({
        ...settings,
        outfile,
        minify: true,
        charset: 'utf8',
        sourcemap: 'linked',
        sourcesContent: true,
        banner: { js: `/*! ${label}.js sha256:${stamp} - built by src/npm/scripts/build-blazor.mjs (ADR-0123) from the Script Core's sources */` },
        define,
    });
    const mapFile = `${outfile}.map`;
    const map = JSON.parse(fs.readFileSync(mapFile, 'utf8'));
    map.sources = map.sources.map(source => path.relative(workspace, path.resolve(path.dirname(mapFile), source)).split(path.sep).join('/'));
    fs.writeFileSync(mapFile, `${JSON.stringify(map)}\n`);
    const shipped = fs.readFileSync(outfile, 'utf8');
    if (!shipped.includes(`"${stamp}"`)) throw new Error(`${outfile} does not answer its stamp; see ADR-0123`);
    return shipped;
}

fs.mkdirSync(out, { recursive: true });

// The Shared Script: the core and the data it reads, every export of each, and the stamp it answers.
const sharedSettings = { ...common, entryPoints: [path.join(workspace, 'blazor/src/shared.ts')] };
const shared = await stampOf(sharedSettings, { EX_SHARED_SHA256: '""' }, []);
const sharedVersion = shared.stamp.slice(0, 12);
const sharedFile = shipsShared ? path.join(out, `${sharedName}.min.js`) : null;
const sharedShipped = sharedFile === null
    ? null
    : await bundle(sharedSettings, sharedFile, sharedName, shared.stamp, { EX_SHARED_SHA256: JSON.stringify(shared.stamp) });

// The product's module takes the core, the data and the Shared Script's stamp from the Shared Script, by one address.
const sharedAddress = `${sharedFolder}${sharedName}.min.js?v=${sharedVersion}`;
const fromShared = {
    name: 'from-the-shared-script',
    setup(builder) {
        builder.onResolve({ filter: /^@ex-grid\/(core|data|blazor-shared)$/ }, () => ({ path: sharedAddress, external: true }));
    },
};
const settings = { ...common, entryPoints: [path.join(here, 'src/index.ts')], plugins: [fromShared] };
const expected = { EX_SHARED_EXPECTED_SHA256: JSON.stringify(shared.stamp) };
const { stamp, count } = await stampOf(settings, { EX_SOURCE_SHA256: '""', ...expected }, [`${sharedName}\n${shared.stamp}`]);
const outfile = path.join(out, `${name}.min.js`);
const shipped = await bundle(settings, outfile, name, stamp, { EX_SOURCE_SHA256: JSON.stringify(stamp), ...expected });
if (!shipped.includes(sharedAddress)) throw new Error(`${outfile} does not import the Shared Script at ${sharedAddress}; see ADR-0411`);
const version = hashOf(shipped).slice(0, 12);
fs.mkdirSync(path.dirname(stampFile), { recursive: true });
fs.writeFileSync(stampFile, `${JSON.stringify({ stamp, version, shared: { stamp: shared.stamp, version: sharedVersion } })}\n`);
if (csFile !== null) {
    fs.mkdirSync(path.dirname(csFile), { recursive: true });
    fs.writeFileSync(csFile, [
        '// <auto-generated>Written by src/npm/scripts/build-blazor.mjs as the product builds (ADR-0123, ADR-0350 section 7).</auto-generated>',
        `namespace ${namespace};`,
        '',
        '/// <summary>The Script Core\'s module as this build of the product ships it.</summary>',
        'internal static class ScriptCoreAsset',
        '{',
        '    /// <summary>The first 12 hex digits of the SHA-256 of the shipped module: the version its address carries.</summary>',
        `    internal const string Version = "${version}";`,
        '',
        '    /// <summary>The SHA-256 of every source the module was bundled from, which it answers: its stamp.</summary>',
        `    internal const string Stamp = "${stamp}";`,
        '}',
        '',
    ].join('\n'));
}
console.log(`${path.relative(process.cwd(), outfile)}: ${shipped.length} bytes from ${count} sources, version ${version}`);
if (sharedFile !== null && sharedShipped !== null) console.log(`${path.relative(process.cwd(), sharedFile)}: ${sharedShipped.length} bytes from ${shared.count} sources, version ${sharedVersion}`);
