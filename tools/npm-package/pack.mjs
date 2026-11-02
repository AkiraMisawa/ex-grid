// Packs the packages npm publishes, at one version, as a Consumer will take them (ADR-0042, ADR-0370 sections 5
// and 9): every member of packages/ that is not marked private, each with its README.md and the repository's
// LICENSE, its version and every dependency on a package of the family set to exactly that version, and its
// scripts left out. The workspace's manifests say 0.0.0-dev, as the C# projects do; only a tag writes a version.
//
// Each package is copied to a stage of its own first, so the working tree is never rewritten, and npm packs the
// stage. The tarballs are written in the order the packages depend on one another, which is the order the release
// publishes them in (packOrder).
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const workspace = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

/** The version the workspace's manifests carry, which only a pack replaces. */
const devVersion = '0.0.0-dev';

/**
 * A version as SemVer writes it, a prerelease suffix included and no build metadata: npm drops a `+…` as it reads a
 * version, so the version published would not be the one asked for.
 */
const semver = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$/;

/** Every published package of the workspace, `{ dir, manifest }`, each after the family's packages it depends on. */
export function packOrder() {
    const root = JSON.parse(fs.readFileSync(path.join(workspace, 'package.json'), 'utf8'));
    const members = root.workspaces
        .filter(member => member.startsWith('packages/'))
        .map(member => ({ dir: path.join(workspace, member), manifest: JSON.parse(fs.readFileSync(path.join(workspace, member, 'package.json'), 'utf8')) }))
        .filter(({ manifest }) => manifest.private !== true);
    const byName = new Map(members.map(member => [member.manifest.name, member]));
    const ordered = [];
    const placed = new Set();
    const place = (member, trail) => {
        if (placed.has(member.manifest.name)) {
            return;
        }
        if (trail.includes(member.manifest.name)) {
            throw new Error(`The packages depend on one another in a circle: ${[...trail, member.manifest.name].join(' -> ')}.`);
        }
        for (const name of familyDependencies(member.manifest)) {
            const dependency = byName.get(name);
            if (dependency === undefined) {
                throw new Error(`${member.manifest.name} depends on ${name}, which is not a published member of the workspace.`);
            }
            place(dependency, [...trail, member.manifest.name]);
        }
        placed.add(member.manifest.name);
        ordered.push(member);
    };
    for (const member of members.toSorted((a, b) => (a.manifest.name < b.manifest.name ? -1 : 1))) {
        place(member, []);
    }
    return ordered;
}

/** The names of the family's packages a manifest depends on. */
export function familyDependencies(manifest) {
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- npm: a manifest without the field has none
    return Object.keys(manifest.dependencies ?? {}).filter(name => name.startsWith('@ex-grid/'));
}

/** The file name npm gives a package's tarball: `@ex-grid/core` at 1.2.3 is `ex-grid-core-1.2.3.tgz`. */
export function tarballName(name, version) {
    return `${name.replace(/^@/, '').replace('/', '-')}-${version}.tgz`;
}

/** The manifest a package is published with: the version written, the family pinned to it, the scripts gone. */
export function publishedManifest(manifest, version) {
    const published = { ...manifest, version };
    delete published.scripts;
    delete published.devDependencies;
    if (manifest.dependencies !== undefined) {
        published.dependencies = Object.fromEntries(Object.entries(manifest.dependencies).map(([name, range]) => {
            if (!name.startsWith('@ex-grid/')) {
                return [name, range];
            }
            if (range !== devVersion) {
                throw new Error(`${manifest.name} depends on ${name} at '${range}', where the workspace writes '${devVersion}' and the pack writes the version.`);
            }
            return [name, version];
        }));
    }
    return published;
}

/** Packs every published package at `version` into `out`, which is emptied first. Returns the tarballs, in order. */
export function packAll(version, out) {
    if (!semver.test(version)) {
        throw new Error(`'${version}' is not a version as SemVer writes it, without build metadata.`);
    }
    fs.rmSync(out, { recursive: true, force: true });
    const stages = path.join(out, 'stage');
    fs.mkdirSync(stages, { recursive: true });
    const license = path.join(workspace, 'LICENSE');
    const tarballs = [];
    for (const { dir, manifest } of packOrder()) {
        if (manifest.version !== devVersion) {
            throw new Error(`${manifest.name} says version ${manifest.version}, where the workspace writes '${devVersion}' and the pack writes the version.`);
        }
        const stage = path.join(stages, path.basename(dir));
        for (const file of ['README.md', ...manifest.files]) {
            const from = path.join(dir, file);
            if (!fs.existsSync(from)) {
                throw new Error(`${manifest.name} has no ${file}${file.startsWith('dist/') || file.endsWith('.min.css') || file.endsWith('.min.css.map') ? ': build the workspace first (npm run build)' : ''}.`);
            }
            fs.mkdirSync(path.dirname(path.join(stage, file)), { recursive: true });
            fs.copyFileSync(from, path.join(stage, file));
        }
        fs.copyFileSync(license, path.join(stage, 'LICENSE'));
        fs.writeFileSync(path.join(stage, 'package.json'), `${JSON.stringify(publishedManifest(manifest, version), null, 2)}\n`);
        // --ignore-scripts: the stage has none, and a pack runs nothing a Consumer's install would not.
        execFileSync('npm', ['pack', '--ignore-scripts', '--pack-destination', out], { cwd: stage, stdio: ['ignore', 'ignore', 'inherit'], shell: process.platform === 'win32' });
        const tarball = path.join(out, tarballName(manifest.name, version));
        if (!fs.existsSync(tarball)) {
            throw new Error(`npm pack wrote no ${path.basename(tarball)} for ${manifest.name}.`);
        }
        tarballs.push({ name: manifest.name, manifest: publishedManifest(manifest, version), tarball });
    }
    fs.rmSync(stages, { recursive: true, force: true });
    return tarballs;
}
