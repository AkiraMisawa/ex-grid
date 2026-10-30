// Layer 1: the report source cases (tests/cases/pivot; ADR-0370 sections 2, 4 and 5, ADR-0151 to ADR-0153),
// kind "pivotReportSource", run against the Script Core's report source (report-source-cases.ts).
// dotnet/tests/ExPivot.Engine.Tests/Cases/PivotReportSourceCaseTests.cs is the C# runner, and the one that writes the
// answers.
import { describe, expect, it } from 'vitest';
import { caseFiles, must, text } from './cases';
import { reportSourceAnswer } from './report-source-cases';

for (const file of caseFiles('pivotReportSource')) {
    describe(`pivot/${file.file}`, () => {
        for (const entry of file.cases) {
            it(text(must(entry, 'name')), async () => {
                expect(await reportSourceAnswer(file.document, entry)).toEqual(entry['scriptExpect'] ?? must(entry, 'expect'));
            });
        }
    });
}
