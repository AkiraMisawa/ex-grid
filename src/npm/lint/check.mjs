// `npm run lint`: first holds the lint to its fixtures, then lints the workspace. A rule that stopped
// firing (an oxlint upgrade that drops a JS plugin, a rule renamed, a config key misspelt) would
// otherwise turn the lint green over exactly the code it exists to refuse.
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const workspace = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const oxlint = path.join(workspace, 'node_modules', '.bin', process.platform === 'win32' ? 'oxlint.cmd' : 'oxlint');
const flags = ['--type-aware', '--deny-warnings', '--report-unused-disable-directives'];

function run(args) {
    return spawnSync(oxlint, args, { cwd: workspace, encoding: 'utf8', shell: process.platform === 'win32' });
}

/** What the fixture's markers say each line must draw: `line -> sorted rule codes`. */
function expected(file) {
    const wanted = new Map();
    fs.readFileSync(path.join(workspace, file), 'utf8').split('\n').forEach((text, index) => {
        const marker = /\/\/ expect(-next)?: (.+)$/.exec(text);
        if (marker === null) {
            return;
        }
        const line = index + 1 + (marker[1] === '-next' ? 1 : 0);
        wanted.set(line, marker[2].split(',').map(code => code.trim()).toSorted(byText));
    });
    return wanted;
}

const byText = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
const listed = (codes, line) => (codes.has(line) ? codes.get(line).join(', ') : 'nothing');

let failed = false;
for (const name of fs.readdirSync(path.join(workspace, 'lint/fixtures')).filter(file => file.endsWith('.ts')).toSorted(byText)) {
    const file = `lint/fixtures/${name}`;
    const result = run([...flags, '-f', 'json', file]);
    if (result.stdout === '') {
        throw new Error(`oxlint printed nothing for ${file}: ${result.stderr}`);
    }
    const drawn = new Map();
    for (const diagnostic of JSON.parse(result.stdout).diagnostics) {
        const line = diagnostic.labels[0].span.line;
        drawn.set(line, [...(drawn.has(line) ? drawn.get(line) : []), diagnostic.code].toSorted(byText));
    }
    const wanted = expected(file);
    for (const line of new Set([...wanted.keys(), ...drawn.keys()])) {
        const want = listed(wanted, line);
        const got = listed(drawn, line);
        if (want !== got) {
            failed = true;
            console.error(`${file}:${line}: the lint must draw ${want}, and drew ${got}.`);
        }
    }
}
if (failed) {
    console.error('The lint no longer refuses what its fixtures hold (lint/fixtures/). Fix the configuration before trusting a green lint.');
    process.exit(1);
}

// The fixtures break every rule on purpose; they were held to their markers above.
const result = spawnSync(oxlint, [...flags, '--ignore-pattern', 'lint/fixtures/**'], { cwd: workspace, stdio: 'inherit', shell: process.platform === 'win32' });
// A run that ended on a signal has no status, and has not passed.
process.exit(result.status === 0 ? 0 : 1);
