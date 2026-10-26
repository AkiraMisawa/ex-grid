// The shared cases of kind `sheetCalls` (tests/cases/README.md; ADR-0370 sections 2 and 5): the engine's surface
// called directly, each held on the ported engine as xUnit holds it on ExSheet.Engine.
import { describe, expect, it } from 'vitest';
import { casesOf, findCase } from './sheet-case';
import { runSheetCall } from './sheet-calls';

describe('sheet calls', () => {
    it.each(casesOf('sheetCalls'))('%s: %s', (file, name) => {
        const differences = runSheetCall(findCase(file, name).entry);
        expect(differences, differences.join('\n')).toEqual([]);
    });
});
