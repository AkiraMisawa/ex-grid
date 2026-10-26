// The shared cases of tests/cases/sheet/ (ADR-0370 sections 2 and 5), each held on the ported engine as xUnit holds
// it on ExSheet.Engine.
import { describe, expect, it } from 'vitest';
import { casesOf, findCase, runSheetCase } from './sheet-case';

describe('sheet cases', () => {
    it.each(casesOf('sheet'))('%s: %s', (file, name) => {
        const { file: document, entry } = findCase(file, name);
        expect(runSheetCase(document, entry)).toEqual([]);
    });
});
