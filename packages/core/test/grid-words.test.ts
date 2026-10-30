// Layer 1 (ADR-0390 section 6, ADR-0370 section 10): the built-in Chrome's words. Every id the core has English for
// has its Japanese, with the same parts to put in, so a grid handed the Japanese words shows no built-in English word
// and loses no figure from a sentence; and the Japanese answers no id the core has stopped saying. (A culture's name put
// into a refusal is the culture's English name, as the C# core's DisplayName gives it: a name, not one of the words.)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { builtInCommandLabel, builtInLabelIds } from '../src/chrome/command-labels';
import { japaneseGridWords, japaneseGridWordTable } from '../src/chrome/japanese-grid-words';
import { refusalWords } from '../src/chrome/refusal-words';
import { defined } from '../src/must';

const parts = (words: string): readonly string[] => [...words.matchAll(/\{(\w+)\}/gu)].map(part => defined(part[1], 'a part\'s name')).toSorted();

describe('the Japanese words', () => {
    it('ADR-0390 section 6: every built-in id has Japanese, with the same parts as its English', () => {
        const missing = builtInLabelIds().filter(id => japaneseGridWords(id) === undefined);
        expect(missing).toEqual([]);
        const unlike = builtInLabelIds().filter(id => parts(defined(japaneseGridWords(id), id)).join() !== parts(builtInCommandLabel(id)).join());
        expect(unlike).toEqual([]);
        const ids = new Set(builtInLabelIds());
        expect([...japaneseGridWordTable.keys()].filter(id => !ids.has(id))).toEqual([]);
    });

    it('ADR-0390 section 6: an id the core does not have is not answered, so a Consumer\'s own command keeps its own words', () => {
        expect(japaneseGridWords('my-command')).toBeUndefined();
    });

    it('ADR-0203: a refusal is asked for by the ids the C# core\'s RefusalLabelIds names, so one Consumer\'s words serve both cores', () => {
        const source = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../dotnet/src/ExGrid/Chrome/RefusalLabelIds.cs');
        const csharp = [...fs.readFileSync(source, 'utf8').matchAll(/const string \w+ = "(refused-[^"]+)";/gu)].map(match => defined(match[1], 'an id'));
        expect(refusalWords.map(([id]) => id).toSorted()).toEqual(csharp.toSorted());
    });
});
