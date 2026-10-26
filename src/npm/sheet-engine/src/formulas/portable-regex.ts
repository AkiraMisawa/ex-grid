// XLOOKUP's match_mode 3, regular expressions (C#'s PortableRegex.cs; ADR-0047, "What the observation settled"
// and "What the second observation settled"). Excel's expressions are PCRE2's; a pattern is accepted only when
// every construct in it means the same in PCRE2 and in the engine, and is translated so that the engine's
// regular expressions give PCRE2's answer. Any other pattern is refused.
//
// The C# engine translates into .NET's syntax; this one into ECMAScript's, compiled with the `u` flag (which
// `\p{…}` needs). The accepted set and the refusals are the C# engine's, and each translation reads as the
// .NET one does over the text it is let read: `.` and a negated class match a character outside the Basic
// Multilingual Plane as one, as the .NET translation's surrogate pair does; a pattern using a Unicode class is
// refused against a text holding a surrogate, where .NET (one UTF-16 unit at a time) and ECMAScript's `u` mode
// (one code point) would read it differently; and `$` is PCRE2's and .NET's, at the end or before a final
// newline. .NET stops a match after a second; ECMAScript cannot, so a pathological pattern runs.
import { isAsciiDigit } from '../dotnet';

const word = '\\p{L}\\p{N}\\p{Mn}\\p{Pc}';
const digit = '\\p{Nd}';
const space = '\\t\\n\\v\\f\\r\\u0085\\u180E\\p{Z}';
const metacharacters = '.\\*+?()[]{}|^$/-';

/** The Unicode general categories both engines name alike. */
const categories = new Set([
    'L', 'Lu', 'Ll', 'Lt', 'Lm', 'Lo', 'M', 'Mn', 'Mc', 'Me', 'N', 'Nd', 'Nl', 'No',
    'P', 'Pc', 'Pd', 'Ps', 'Pe', 'Pi', 'Pf', 'Po', 'S', 'Sm', 'Sc', 'Sk', 'So',
    'Z', 'Zs', 'Zl', 'Zp', 'C', 'Cc', 'Cf', 'Co', 'Cn',
]);

interface Compiled {
    readonly regex: RegExp;
    readonly unicode: boolean;
}

const cache = new Map<string, Compiled | undefined>();

/** A text to search, at its position along the lookup array. */
export interface IndexedText {
    readonly index: number;
    readonly text: string;
}

/**
 * The position of the first text, in the order given, that `pattern` matches: `{ found }`, `found` undefined
 * for none; `undefined` when the pattern is refused, or when a text it has to read would be read differently.
 */
export function firstMatch(pattern: string, texts: Iterable<IndexedText>): { found: number | undefined } | undefined {
    const compiled = compile(pattern);
    if (compiled === undefined) return undefined;
    for (const { index, text } of texts) {
        if (compiled.unicode && /[\uD800-\uDFFF]/.test(text)) return undefined;
        if (compiled.regex.test(text)) return { found: index };
    }
    return { found: undefined };
}

function compile(pattern: string): Compiled | undefined {
    if (cache.has(pattern)) return cache.get(pattern);
    if (cache.size >= 256) cache.clear();
    const translation = translate(pattern);
    const compiled = translation === undefined ? undefined : construct(translation);
    cache.set(pattern, compiled);
    return compiled;
}

function construct(translation: { text: string; unicode: boolean }): Compiled | undefined {
    // A translation the engine does not take is refused, as .NET's ArgumentException is.
    if (!compilesAsRegex(translation.text)) return undefined;
    return { regex: new RegExp(translation.text, 'u'), unicode: translation.unicode };
}

function compilesAsRegex(text: string): boolean {
    try {
        void new RegExp(text, 'u');
        return true;
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a pattern the engine does not take is refused, as .NET's ArgumentException is
        if (error instanceof SyntaxError) return false;
        throw error;
    }
}

/** The pattern in ECMAScript's syntax, meaning what it means to PCRE2; `undefined` when it uses a construct outside the accepted set. */
export function translate(pattern: string): { text: string; unicode: boolean } | undefined {
    let unicode = false;
    let output = '';
    const groups: Group[] = [];
    let captures = 0;
    let quantifiable = false;
    for (let i = 0; i < pattern.length; i++) {
        const c = pattern.charAt(i);
        switch (c) {
            case '\\': {
                if (++i >= pattern.length) return undefined;
                const escaped = pattern.charAt(i);
                if (escaped === 'd') {
                    output += `[${digit}]`;
                    unicode = true;
                    quantifiable = true;
                } else if (escaped === 'w') {
                    output += `[${word}]`;
                    unicode = true;
                    quantifiable = true;
                } else if (escaped === 's') {
                    output += `[${space}]`;
                    unicode = true;
                    quantifiable = true;
                } else if (escaped === 'b') {
                    output += `(?:(?<=[${word}])(?![${word}])|(?<![${word}])(?=[${word}]))`;
                    unicode = true;
                    quantifiable = false;
                } else if (escaped === 'p') {
                    const close = pattern.charAt(i + 1) === '{' ? pattern.indexOf('}', i + 2) : -1;
                    if (close < 0 || !categories.has(pattern.slice(i + 2, close))) return undefined;
                    output += `\\p{${pattern.slice(i + 2, close)}}`;
                    i = close;
                    unicode = true;
                    quantifiable = true;
                } else if (escaped >= '1' && escaped <= '9') {
                    if (groups.includes('lookbehind')) return undefined;
                    // A backreference to a group already opened; \10 and up, and a digit after, read differently.
                    if (escaped.charCodeAt(0) - 48 > captures || (i + 1 < pattern.length && isAsciiDigit(pattern.charCodeAt(i + 1)))) return undefined;
                    output += `\\${escaped}`;
                    quantifiable = true;
                } else {
                    if (!metacharacters.includes(escaped)) return undefined;
                    // ECMAScript's `u` mode takes no `\-` outside a class: the hyphen there is itself.
                    output += escaped === '-' ? '-' : `\\${escaped}`;
                    quantifiable = true;
                }
                break;
            }
            case '.':
                output += '[^\\n]';
                quantifiable = true;
                break;
            case '[': {
                const read = characterClass(pattern, i);
                if (read === undefined) return undefined;
                output += read.text;
                unicode ||= read.unicode;
                i = read.end;
                quantifiable = true;
                break;
            }
            case '(':
                // Inside a lookbehind nothing opens: PCRE2 wants each of its branches a fixed length.
                if (groups.includes('lookbehind')) return undefined;
                if (pattern.charAt(i + 1) === '?') {
                    const third = pattern.charAt(i + 2);
                    if (third === '=' || third === '!') {
                        output += `(?${third}`;
                        i += 2;
                        groups.push('lookahead');
                    } else if (third === '<' && (pattern.charAt(i + 3) === '=' || pattern.charAt(i + 3) === '!')) {
                        output += `(?<${pattern.charAt(i + 3)}`;
                        i += 3;
                        groups.push('lookbehind');
                    } else return undefined;
                } else {
                    output += '(';
                    captures++;
                    groups.push('capture');
                }
                quantifiable = false;
                break;
            case ')': {
                const closed = groups.pop();
                if (closed === undefined) return undefined;
                output += ')';
                // A quantified lookaround is read differently by the two engines' versions: refused.
                quantifiable = closed === 'capture';
                break;
            }
            case '|':
            case '^':
                output += c;
                quantifiable = false;
                break;
            case '$':
                // PCRE2's and .NET's $: at the end, or before a newline that ends the text.
                output += '(?=\\n?$)';
                quantifiable = false;
                break;
            case '*':
            case '+':
            case '?': {
                if (!quantifiable || groups.includes('lookbehind')) return undefined;
                output += c;
                const lazy = lazyAfter(pattern, i);
                if (lazy === undefined) return undefined;
                output += lazy.text;
                i = lazy.at;
                quantifiable = false;
                break;
            }
            case '{': {
                if (!quantifiable || groups.includes('lookbehind')) return undefined;
                const closeBrace = pattern.indexOf('}', i);
                if (closeBrace < 0 || !bounds(pattern.slice(i + 1, closeBrace))) return undefined;
                output += pattern.slice(i, closeBrace + 1);
                const lazy = lazyAfter(pattern, closeBrace);
                if (lazy === undefined) return undefined;
                output += lazy.text;
                i = lazy.at;
                quantifiable = false;
                break;
            }
            case ']':
            case '}':
                return undefined;
            default:
                if (isSurrogate(c)) return undefined;
                output += literal(c);
                quantifiable = true;
                break;
        }
    }
    return groups.length === 0 ? { text: output, unicode } : undefined;
}

/** What an open group is: one that captures, or a lookaround. */
type Group = 'capture' | 'lookahead' | 'lookbehind';

function isSurrogate(c: string): boolean {
    const code = c.charCodeAt(0);
    return code >= 0xD800 && code <= 0xDFFF;
}

/** A character outside a class as ECMAScript's `u` mode takes it literally. */
function literal(c: string): string {
    return '^$\\.*+?()[]{}|/'.includes(c) ? `\\${c}` : c;
}

/** A lazy `?` after a quantifier is copied; a possessive `+` is refused. */
function lazyAfter(pattern: string, at: number): { text: string; at: number } | undefined {
    const next = pattern.charAt(at + 1);
    if (next === '+') return undefined;
    if (next === '?') return { text: '?', at: at + 1 };
    return { text: '', at };
}

/** `n`, `n,` or `n,m` with n ≤ m: the inside of a quantifier both engines read alike. */
function bounds(inside: string): boolean {
    const parts = inside.split(',');
    const low = count(parts[0] as string); // oxlint-disable-line typescript/no-unsafe-type-assertion -- split gives at least one part
    if (parts.length > 2 || low === undefined) return false;
    if (parts.length === 1 || (parts[1] as string).length === 0) return true; // oxlint-disable-line typescript/no-unsafe-type-assertion -- two parts
    const high = count(parts[1] as string); // oxlint-disable-line typescript/no-unsafe-type-assertion -- two parts
    return high !== undefined && low <= high;
}

function count(digits: string): number | undefined {
    return digits.length > 0 && digits.length <= 5 && /^[0-9]+$/.test(digits) ? Number(digits) : undefined;
}

/** A character class from `open`, translated: its text, whether it uses a Unicode class, and the index of its closing `]`; `undefined` when it is refused. */
function characterClass(pattern: string, open: number): { text: string; unicode: boolean; end: number } | undefined {
    let i = open + 1;
    const negated = pattern.charAt(i) === '^';
    if (negated) i++;
    let items = '';
    let itemCount = 0;
    let unicode = false;
    let previous: string | undefined; // the last single literal character, which may start a range
    for (;;) {
        if (i >= pattern.length) return undefined;
        const c = pattern.charAt(i);
        if (c === ']') {
            if (itemCount === 0) return undefined;
            break;
        }
        if (c === '[' || isSurrogate(c)) return undefined;
        if (c === '\\') {
            if (i + 1 >= pattern.length) return undefined;
            const escaped = pattern.charAt(i + 1);
            i += 2;
            if (escaped === 'd') {
                items += digit;
                unicode = true;
                previous = undefined;
            } else if (escaped === 'w') {
                items += word;
                unicode = true;
                previous = undefined;
            } else if (escaped === 's') {
                items += space;
                unicode = true;
                previous = undefined;
            } else {
                if (!metacharacters.includes(escaped)) return undefined;
                items += `\\${escaped}`;
                previous = escaped;
            }
            itemCount++;
            continue;
        }
        if (c === '-') {
            const last = pattern.charAt(i + 1) === ']';
            if (itemCount === 0 || last) {
                items += '\\-';
                previous = '-';
                itemCount++;
                i++;
                continue;
            }
            // A range: a single literal character on each side, in order.
            if (previous === undefined || i + 1 >= pattern.length) return undefined;
            const high = pattern.charAt(i + 1);
            if (high === '\\' || high === '[' || high === ']' || high === '-' || isSurrogate(high) || high < previous) return undefined;
            items += `-${classLiteral(high)}`;
            previous = undefined;
            i += 2;
            itemCount++;
            continue;
        }
        items += classLiteral(c);
        previous = c;
        itemCount++;
        i++;
    }
    return { text: `[${negated ? '^' : ''}${items}]`, unicode, end: i };
}

function classLiteral(c: string): string {
    return c === '^' || c === '\\' || c === ']' || c === '[' || c === '-' ? `\\${c}` : c;
}
