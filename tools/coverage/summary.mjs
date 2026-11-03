// `node tools/coverage/summary.mjs`, after `npm run coverage`: the Script Core's coverage per product, as the tests job
// reports the .NET packages' (ADR-0041). Reads Vitest's coverage/ts/coverage-summary.json and writes beside it
// Summary.md, the table CI puts into the run's summary, and products.json, which the badges job reads. A package is
// counted under its product by the list below; one the list does not name stops the script, because counting it
// nowhere, or under a guessed product, would make a figure that is quietly wrong.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const workspace = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const directory = path.join(workspace, 'coverage', 'ts');

/** Each product's packages, by their directory under packages/: the order is the tests job's. */
const products = [
    ['ExGrid', ['core', 'react', 'mantine', 'mui', 'blazor']],
    ['ExSheet', ['sheet', 'sheet-engine', 'sheet-xlsx', 'sheet-react', 'sheet-mantine', 'sheet-mui', 'sheet-blazor']],
    ['ExPivot', ['pivot', 'pivot-engine', 'pivot-react', 'pivot-mantine', 'pivot-mui', 'pivot-blazor']],
    ['ExChart', ['chart', 'chart-react', 'chart-mantine', 'chart-mui', 'chart-blazor']],
    ['ExBoard', ['board', 'board-react', 'board-blazor']],
    ['Data', ['data', 'data-xlsx']],
];
const productOf = new Map(products.flatMap(([product, members]) => members.map(member => [member, product])));

const report = JSON.parse(fs.readFileSync(path.join(directory, 'coverage-summary.json'), 'utf8'));

const empty = () => ({ lines: 0, linesCovered: 0, branches: 0, branchesCovered: 0 });
const byPackage = new Map();
for (const [file, figures] of Object.entries(report)) {
    if (file === 'total') {
        continue;
    }
    const match = /[\\/]packages[\\/]([^\\/]+)[\\/]src[\\/]/.exec(file);
    if (match === null) {
        throw new Error(`${file} is not under a package's src/, which is all the coverage includes.`);
    }
    const name = match[1];
    if (!productOf.has(name)) {
        throw new Error(`packages/${name} belongs to no product: name it in tools/coverage/summary.mjs.`);
    }
    if (!byPackage.has(name)) {
        byPackage.set(name, empty());
    }
    const sum = byPackage.get(name);
    sum.lines += figures.lines.total;
    sum.linesCovered += figures.lines.covered;
    sum.branches += figures.branches.total;
    sum.branchesCovered += figures.branches.covered;
}
for (const name of productOf.keys()) {
    if (!byPackage.has(name)) {
        throw new Error(`packages/${name} is named under a product but the coverage holds none of its sources.`);
    }
}

const add = (into, sum) => {
    into.lines += sum.lines;
    into.linesCovered += sum.linesCovered;
    into.branches += sum.branches;
    into.branchesCovered += sum.branchesCovered;
    return into;
};
// One decimal, as ReportGenerator gives the .NET figures, rounded down so a figure never reads higher than it is.
const percent = (covered, total) => {
    if (total === 0) {
        throw new Error('A figure over no lines or branches has no percentage.');
    }
    return Math.floor((1000 * covered) / total) / 10;
};
const figure = sum => ({ line: percent(sum.linesCovered, sum.lines), branch: percent(sum.branchesCovered, sum.branches) });

const total = empty();
const perProduct = products.map(([product, members]) => {
    const sum = members.reduce((into, member) => add(into, byPackage.get(member)), empty());
    add(total, sum);
    return [product, sum];
});

const rows = [
    '## TypeScript coverage per product',
    '',
    '| Product | Line | Branch |',
    '|---|---|---|',
    ...perProduct.map(([product, sum]) => `| ${product} | ${figure(sum).line}% | ${figure(sum).branch}% |`),
    `| **Total** | **${figure(total).line}%** (${total.linesCovered} of ${total.lines}) | **${figure(total).branch}%** (${total.branchesCovered} of ${total.branches}) |`,
    '',
    '<details><summary>Per package</summary>',
    '',
    '| Package | Product | Line | Branch | Lines |',
    '|---|---|---|---|---|',
    ...[...productOf].map(([name, product]) => {
        const sum = byPackage.get(name);
        return `| \`packages/${name}\` | ${product} | ${figure(sum).line}% | ${figure(sum).branch}% | ${sum.lines} |`;
    }),
    '',
    '</details>',
    '',
];
fs.writeFileSync(path.join(directory, 'Summary.md'), `${rows.join('\n')}\n`);
fs.writeFileSync(
    path.join(directory, 'products.json'),
    `${JSON.stringify({ total: figure(total), products: Object.fromEntries(perProduct.map(([product, sum]) => [product, figure(sum)])) }, null, 4)}\n`,
);
process.stdout.write(fs.readFileSync(path.join(directory, 'Summary.md'), 'utf8'));
