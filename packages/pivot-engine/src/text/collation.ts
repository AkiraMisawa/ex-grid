// A culture's order of text, as the C# engine orders text Items by label: CompareInfo.Compare with
// IgnoreCase, which is ICU's collation at secondary strength, `Intl.Collator`'s `accent` sensitivity, with
// the rules .NET adds to it. Ties are broken ordinally by the caller, so the order is total.
//
// .NET keeps the kana type and the width apart even while it ignores case: it tailors each katakana to
// sort after its hiragana, and each half-width or full-width form after its usual form, as a difference of
// the first strength (System.Globalization.Native's custom rules). So in ja-JP あめ comes before アップル,
// where ICU alone puts アップル first. `Intl.Collator` takes no rules, so the difference is found here: on
// the first characters that ICU gives one primary weight but .NET ranks apart (tailored-ranks.ts), before
// any later letter differs. Character by character: where a full-width or kana form meets a contraction
// (Danish aa, which is å), or an expansion, the order can differ from .NET's.

import { tailoredRanks } from './tailored-ranks';

const collators = new Map<string, Intl.Collator>();
const baseCollators = new Map<string, Intl.Collator>();

function collator(culture: string, sensitivity: 'accent' | 'base', cache: Map<string, Intl.Collator>): Intl.Collator {
    let found = cache.get(culture);
    if (found === undefined) {
        found = new Intl.Collator(culture === 'invariant' ? 'und' : culture, { sensitivity, usage: 'sort' });
        cache.set(culture, found);
    }
    return found;
}

// Katakana, the small kana and the half-width and full-width forms: what .NET's rules rank.
const anyTailored = /[ァ-ヺ！-￮]/;

function rankOf(character: string): number {
    const codePoint = character.codePointAt(0);
    if (codePoint === undefined) throw new Error('Internal: an empty character.');
    const rank = tailoredRanks.get(codePoint);
    return rank === undefined ? 0 : rank;
}

/** -1, 0 or 1 as `a` sorts before, with or after `b` in `culture`, ignoring case, as .NET orders them. */
export function compareInCulture(culture: string, a: string, b: string): number {
    const accent = collator(culture, 'accent', collators);
    if (anyTailored.test(a) || anyTailored.test(b)) {
        const base = collator(culture, 'base', baseCollators);
        const left = Array.from(a);
        const right = Array.from(b);
        const length = Math.min(left.length, right.length);
        for (let i = 0; i < length; i++) {
            const x = left[i];
            const y = right[i];
            if (x === undefined || y === undefined) throw new Error('Internal: a character outside the text.');
            if (base.compare(x, y) !== 0) break;
            const rx = rankOf(x);
            const ry = rankOf(y);
            if (rx !== ry) return rx < ry ? -1 : 1;
        }
    }
    return Math.sign(accent.compare(a, b));
}
