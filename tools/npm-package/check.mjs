// The npm packages, taken as a Consumer takes them (ADR-0042, ADR-0370 section 9): the counterpart of
// dotnet/tests/ExGrid.PackageSmoke/check.sh for what npm publishes.
//
//   node tools/npm-package/check.mjs [version]        (from the repository's root, after npm ci && npm run build)
//
// It packs every published package at the version (pack.mjs), then reads each tarball back:
//
// - it holds exactly package.json, README.md, LICENSE and the files its manifest names, and nothing else;
// - its manifest says the version, MIT and its own directory, and depends on the family at exactly the version;
// - every module its script and its declarations import is a dependency or a peer dependency it declares;
// - a stylesheet it carries is byte for byte the one its product's NuGet package serves, and the stamp in it is the
//   one the package's script hides its stylesheet notice for (ADR-0123).
//
// Then it installs every tarball into an application of its own outside the workspace, with the peers at the
// versions the workspace builds against, and takes them as that application would: TypeScript checks a module that
// imports every package, its declarations included (skipLibCheck off); esbuild bundles it with every stylesheet a
// package exports; and Node runs the packages that compute (consumer/run.mjs).
//
// The version defaults to a throwaway 0.0.0-smoke.<time>; CI passes its own, and the release workflow the tag's.
// The tarballs are left in tools/npm-package/.tarballs with publish-order.txt, which is what the release publishes:
// the files this script tested, in that order.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { build } from 'esbuild';
import { packAll, workspace } from './pack.mjs';
import { stylesheets } from '../scripts/stylesheets.mjs';

const version = process.argv[2] === undefined ? `0.0.0-smoke.${Date.now()}` : process.argv[2];
const out = path.join(workspace, 'tools/npm-package/.tarballs');
const failures = [];
const refuse = message => failures.push(message);
const shell = process.platform === 'win32';

console.log(`== pack ${version}`);
const packed = packAll(version, out);
fs.writeFileSync(path.join(out, 'publish-order.txt'), `${packed.map(({ tarball }) => path.basename(tarball)).join('\n')}\n`);
console.log(packed.map(({ tarball }) => `   ${path.basename(tarball)}`).join('\n'));

console.log('== what each tarball holds');
const unpacked = fs.mkdtempSync(path.join(os.tmpdir(), 'ex-grid-npm-unpacked-'));
const root = JSON.parse(fs.readFileSync(path.join(workspace, 'package.json'), 'utf8'));
const stylesheetOf = Object.fromEntries(Object.entries(stylesheets).map(([dir, file]) => [`@ex-grid/${dir}`, file]));
// The packages whose script writes a stylesheet notice with the stamp of the stylesheet it was built with
// (shipped-stylesheet.ts, which tools/assets/minify.mjs writes). ExSheet's and ExPivot's draw through ExGrid's.
const stamped = new Set(['@ex-grid/core', '@ex-grid/chart']);
for (const { name, manifest, tarball } of packed) {
    const entries = execFileSync('tar', ['-tzf', tarball], { encoding: 'utf8' }).split('\n').filter(line => line !== '').toSorted();
    const expected = ['package.json', 'README.md', 'LICENSE', ...manifest.files].map(file => `package/${file}`).toSorted();
    if (entries.join('\n') !== expected.join('\n')) {
        refuse(`${name} holds ${entries.join(', ')}, where it should hold exactly ${expected.join(', ')}.`);
    }
    const dir = path.join(unpacked, path.basename(tarball, '.tgz'));
    fs.mkdirSync(dir);
    execFileSync('tar', ['-xzf', tarball, '-C', dir]);
    const at = file => path.join(dir, 'package', file);
    const text = fs.readFileSync(at('package.json'), 'utf8');
    const read = JSON.parse(text);
    if (read.version !== version) refuse(`${name} says version ${read.version}.`);
    if (text.includes('0.0.0-dev')) refuse(`${name}'s manifest still names 0.0.0-dev.`);
    if (read.license !== 'MIT') refuse(`${name} does not declare MIT.`);
    if (read.scripts !== undefined) refuse(`${name} carries scripts.`);
    if (read.repository?.directory !== `packages/${name.slice('@ex-grid/'.length)}`) {
        refuse(`${name} names the directory ${String(read.repository?.directory)} in its repository.`);
    }
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- npm: a manifest without the field has none
    const dependencies = read.dependencies ?? {};
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- npm: a manifest without the field has none
    const declared = new Set([...Object.keys(dependencies), ...Object.keys(read.peerDependencies ?? {})]);
    for (const [dependency, range] of Object.entries(dependencies)) {
        if (dependency.startsWith('@ex-grid/') && range !== version) refuse(`${name} depends on ${dependency} at '${range}', not exactly ${version}.`);
    }
    for (const file of ['dist/index.js', 'dist/index.d.ts']) {
        const source = fs.readFileSync(at(file), 'utf8');
        // A static import or re-export as a statement of its own; a dynamic import or an import type; a require that
        // esbuild left for a module it did not bundle; and a declaration file's reference to a package's types.
        const statements = /^\s*(?:import|export)\b[^'";]*?\bfrom\s*['"]([^'"]+)['"]|^\s*import\s*['"]([^'"]+)['"]|\bimport\(\s*['"]([^'"]+)['"]\s*\)|\b(?:__)?require\(\s*['"]([^'"]+)['"]\s*\)|^\/\/\/\s*<reference\s+types=['"]([^'"]+)['"]/gm;
        const specifiers = [...source.matchAll(statements)].map(match => match.slice(1).find(group => group !== undefined));
        for (const specifier of new Set(specifiers)) {
            if (specifier.startsWith('.')) {
                refuse(`${name}'s ${file} imports ${specifier}, a file of its own, where the build bundles every module it has.`);
                continue;
            }
            const parts = specifier.split('/');
            const imported = specifier.startsWith('@') ? parts.slice(0, 2).join('/') : parts[0];
            if (!declared.has(imported)) refuse(`${name}'s ${file} imports ${specifier}, and ${imported} is neither a dependency nor a peer dependency of it.`);
        }
    }
    const stylesheet = stylesheetOf[name];
    if (stylesheet !== undefined) {
        const carried = path.basename(stylesheet);
        if (read.exports['./styles.css'] !== `./${carried}`) refuse(`${name} exports ./styles.css as ${String(read.exports['./styles.css'])}, not ./${carried}.`);
        for (const file of [carried, `${carried}.map`]) {
            if (!fs.readFileSync(at(file)).equals(fs.readFileSync(path.join(workspace, path.dirname(stylesheet), file)))) {
                refuse(`${name}'s ${file} is not the file its NuGet package serves, ${path.dirname(stylesheet)}/${file}.`);
            }
        }
        // The stamp the stylesheet hides its notice for, and the one the script writes on that notice (ADR-0123).
        const stamp = /\[data-ex-stamp="([0-9a-f]{64})"\]/.exec(fs.readFileSync(at(carried), 'utf8'));
        if (stamp === null) {
            refuse(`${name}'s ${carried} has no stamp rule.`);
        } else if (stamped.has(name) && !fs.readFileSync(at('dist/index.js'), 'utf8').includes(stamp[1])) {
            refuse(`${name}'s script does not write its stylesheet's stamp, ${stamp[1]}: what it draws would show its stylesheet notice.`);
        }
    }
}
fs.rmSync(unpacked, { recursive: true, force: true });
if (failures.length > 0) {
    failures.forEach(message => console.error(`npm package check: ${message}`));
    process.exit(1);
}

console.log('== an application that takes them from the tarballs alone');
const consumer = fs.mkdtempSync(path.join(os.tmpdir(), 'ex-grid-npm-consumer-'));
const pin = name => {
    const range = root.devDependencies[name];
    if (range === undefined) throw new Error(`The workspace does not build against ${name}, which a package names as a peer.`);
    return range;
};
const peers = new Set(packed.flatMap(({ manifest }) => Object.keys(manifest.peerDependencies ?? {}))); // oxlint-disable-line ex-grid/no-stand-in-default -- npm: a manifest without the field has none
const fromTarball = Object.fromEntries(packed.map(({ name, tarball }) => [name, `file:${tarball}`]));
fs.writeFileSync(path.join(consumer, 'package.json'), `${JSON.stringify({
    name: 'ex-grid-npm-consumer',
    private: true,
    type: 'module',
    dependencies: {
        ...fromTarball,
        ...Object.fromEntries([...peers].toSorted().map(name => [name, pin(name)])),
        '@types/react': pin('@types/react'),
        '@types/react-dom': pin('@types/react-dom'),
    },
    // A family package's dependency on another is the exact version, which no registry has yet: the tarball is it.
    overrides: fromTarball,
}, null, 2)}\n`);
execFileSync('npm', ['install', '--no-audit', '--no-fund', '--ignore-scripts', '--loglevel', 'error'], { cwd: consumer, stdio: 'inherit', shell });
for (const { name } of packed) {
    const installed = JSON.parse(fs.readFileSync(path.join(consumer, 'node_modules', name, 'package.json'), 'utf8'));
    if (installed.version !== version) failures.push(`the application installed ${name} ${installed.version}, not the packed ${version}.`);
}

const lines = [];
packed.forEach(({ name, manifest }, index) => {
    lines.push(`import * as package${index} from '${name}';`);
    if (manifest.exports['./styles.css'] !== undefined) lines.push(`import '${name}/styles.css';`);
});
lines.push('', `export const packages: readonly object[] = [${packed.map((_, index) => `package${index}`).join(', ')}];`, '');
fs.writeFileSync(path.join(consumer, 'consumer.ts'), lines.join('\n'));
// What a bundler's client types declare (Vite's among them), so that a stylesheet imported for its effect is a module.
fs.writeFileSync(path.join(consumer, 'stylesheets.d.ts'), "declare module '*.css';\n");
fs.writeFileSync(path.join(consumer, 'tsconfig.json'), `${JSON.stringify({
    compilerOptions: {
        target: 'es2022',
        module: 'esnext',
        moduleResolution: 'bundler',
        lib: ['esnext', 'dom', 'dom.iterable'],
        jsx: 'react-jsx',
        strict: true,
        noEmit: true,
        skipLibCheck: false,
        types: [],
    },
    files: ['stylesheets.d.ts', 'consumer.ts'],
}, null, 2)}\n`);
const tsc = path.join(workspace, 'node_modules/typescript/bin/tsc');
try {
    execFileSync(process.execPath, [tsc, '-p', 'tsconfig.json'], { cwd: consumer, stdio: 'inherit' });
} catch {
    failures.push('TypeScript refused a module that imports every package (above).');
}
try {
    await build({
        absWorkingDir: consumer,
        entryPoints: ['consumer.ts'],
        outdir: 'bundle',
        bundle: true,
        format: 'esm',
        platform: 'browser',
        target: 'es2022',
        logLevel: 'error',
    });
    const css = fs.readFileSync(path.join(consumer, 'bundle/consumer.css'), 'utf8');
    for (const [dir, file] of Object.entries(stylesheets)) {
        const named = path.basename(file).replace('.min.css', '.css');
        if (!css.includes(`[data-ex-stylesheet="${named}"]`)) failures.push(`the bundle carries no ${named} from @ex-grid/${dir}/styles.css.`);
    }
} catch {
    failures.push('esbuild could not bundle a module that imports every package and every stylesheet (above).');
}
fs.copyFileSync(path.join(workspace, 'tools/npm-package/consumer/run.mjs'), path.join(consumer, 'run.mjs'));
try {
    execFileSync(process.execPath, ['run.mjs'], { cwd: consumer, stdio: 'inherit' });
} catch {
    failures.push('Node could not run the packages that compute (above).');
}
if (process.env.EXGRID_KEEP_CONSUMER === undefined) {
    fs.rmSync(consumer, { recursive: true, force: true });
} else {
    console.log(`   the application is kept at ${consumer}`);
}

if (failures.length > 0) {
    failures.forEach(message => console.error(`npm package check: ${message}`));
    process.exit(1);
}
console.log(`npm package check: ${packed.length} packages at ${version}, taken as a Consumer takes them.`);
