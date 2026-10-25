// Layer 1: the Pivot Source cases (tests/cases/pivot; ADR-0370 sections 2 and 5, ADR-0066), kind
// "pivotSource", run against the Script Core's engine (source-cases.ts).
// tests/ExPivot.Engine.Tests/Cases/PivotSourceCaseTests.cs is the C# runner, and the one that writes the
// answers.
import { describe, expect, it } from 'vitest';
import { caseFiles, must, text } from './cases';
import { sourceAnswer } from './source-cases';

for (const file of caseFiles('pivotSource')) {
    describe(`pivot/${file.file}`, () => {
        for (const entry of file.cases) {
            it(text(must(entry, 'name')), async () => {
                expect(await sourceAnswer(file.document, entry)).toEqual(entry['scriptExpect'] ?? must(entry, 'expect'));
            });
        }
    });
}
