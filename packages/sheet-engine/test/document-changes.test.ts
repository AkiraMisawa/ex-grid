// The shared cases of kind `documentChanges` (tests/cases/sheet/document-changes.json; ADR-0370 section 1's note of
// 2026-10-30): DocumentChanges writes each document raised as the case says, and xUnit holds SheetWire's reader to
// reading what is written back as the document raised.
import { describe, expect, it } from 'vitest';
import { DocumentChanges, WorkbookDocument } from '../src/index';
import { casesOf, findCase, list, object } from './sheet-case';

describe('document changes', () => {
    it.each(casesOf('documentChanges'))('%s: %s', (file, name) => {
        const changes = new DocumentChanges();
        for (const [at, step] of list(findCase(file, name).entry['steps'], 'the steps').entries()) {
            const taken = object(step, 'a step');
            if (taken['forget'] === true) {
                changes.forget();
                continue;
            }
            const raised = WorkbookDocument.fromJson(JSON.stringify(taken['raise']));
            expect(JSON.parse(changes.write(raised)), `step ${String(at)}`).toEqual(taken['written']);
        }
    });
});
