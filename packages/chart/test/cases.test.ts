// Runs the shared cases of tests/cases/chart/ (ADR-0370, section 2) against @ex-grid/chart; xUnit runs the
// same files against the C# engine (dotnet/tests/ExChart.Engine.Tests/Cases/ChartCaseTests.cs).
// tests/cases/README.md says what a file holds, and chart-case.ts reads one case.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { ChartCase, isObject, parse, type ChartFile } from './chart-case';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../tests/cases/chart');

function read(name: string): ChartFile {
    const json = parse(fs.readFileSync(path.join(root, name), 'utf8'));
    if (!isObject(json) || typeof json['about'] !== 'string' || !Array.isArray(json['cases'])) {
        throw new Error(`chart/${name} is not a file of cases.`);
    }
    if (json['kind'] !== 'chart') {
        throw new Error(`chart/${name} is of kind ${JSON.stringify(json['kind'])}, and only chart files live there.`);
    }
    const cases = json['cases'].filter(isObject);
    if (cases.length !== json['cases'].length) {
        throw new Error(`chart/${name} holds a case that is not an object.`);
    }
    const near = json['near'];
    const definitions = json['definitions'];
    const tables = json['tables'];
    if ((near !== undefined && typeof near !== 'number') || (definitions !== undefined && !isObject(definitions))
        || (tables !== undefined && !isObject(tables))) {
        throw new Error(`chart/${name} has a near, definitions or tables the runner cannot read.`);
    }
    return {
        about: json['about'], kind: json['kind'], cases,
        ...(near === undefined ? {} : { near }),
        ...(definitions === undefined ? {} : { definitions }),
        ...(tables === undefined ? {} : { tables }),
    };
}

const files = fs.readdirSync(root).filter(name => name.endsWith('.json')).toSorted();

it('ADR-0370: the chart cases are there to run', () => {
    expect(files.length).toBeGreaterThan(0);
});

for (const name of files) {
    const file = read(name);
    describe(`chart/${name}`, () => {
        const names = file.cases.map(c => c['name']);
        it('names each case once', () => {
            expect(new Set(names).size).toBe(names.length);
        });
        for (const entry of file.cases) {
            const caseName = entry['name'];
            if (typeof caseName !== 'string') {
                throw new Error(`chart/${name} holds a case with no name.`);
            }
            it(caseName, async () => {
                expect.hasAssertions();
                await new ChartCase(file, entry).run();
            });
        }
    });
}
