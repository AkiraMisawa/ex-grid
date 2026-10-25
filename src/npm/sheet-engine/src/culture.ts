// The culture a Sheet reads and writes text in: what the C# engine reads from a CultureInfo, as a table
// (ADR-0370, section 10). TypeScript has no current culture, so a Sheet is told its locale and never reads
// the browser's on its own. The values are .NET's as it reads them from ICU, so the two engines answer
// alike; where Excel's text differs from them, the engine corrects it where it is read (ADR-0071), as the
// C# engine does.

import { elementAt } from './must';

/** The parts of a culture the engine reads: `CultureInfo`'s number and date formats, for one locale. */
export interface CultureData {
    /** The locale's BCP 47 tag, as `CultureInfo.Name`; empty for the invariant culture. */
    readonly name: string;
    /** `TwoLetterISOLanguageName`: `en`, `de`, `ja`; `iv` for the invariant culture. */
    readonly twoLetterLanguage: string;
    readonly numberDecimalSeparator: string;
    readonly numberGroupSeparator: string;
    /** `NumberFormatInfo.NumberGroupSizes`: the digits in each group, the units' group first. */
    readonly numberGroupSizes: readonly number[];
    readonly negativeSign: string;
    readonly currencySymbol: string;
    /** `NumberFormatInfo.CurrencyPositivePattern`: 0 is `$n`, 1 `n$`, 2 `$ n`, 3 `n $`. */
    readonly currencyPositivePattern: number;
    /** `NumberFormatInfo.CurrencyNegativePattern`, 0 to 16. */
    readonly currencyNegativePattern: number;
    readonly currencyDecimalDigits: number;
    readonly shortDatePattern: string;
    readonly longTimePattern: string;
    readonly shortTimePattern: string;
    readonly dateSeparator: string;
    /** The twelve months' names, January first. */
    readonly monthNames: readonly string[];
    readonly abbreviatedMonthNames: readonly string[];
    /** The seven days' names, Sunday first. */
    readonly dayNames: readonly string[];
    readonly abbreviatedDayNames: readonly string[];
}

const englishMonths = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const englishDays = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const englishShortDays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const japaneseMonths = ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月'];

/** .NET 10's CultureInfo on ICU for each locale the engine is held to (`tests/cases/`), read on 2026-10-25. */
const known: readonly CultureData[] = [
    {
        name: '', twoLetterLanguage: 'iv',
        numberDecimalSeparator: '.', numberGroupSeparator: ',', numberGroupSizes: [3], negativeSign: '-',
        currencySymbol: '¤', currencyPositivePattern: 0, currencyNegativePattern: 0, currencyDecimalDigits: 2,
        shortDatePattern: 'MM/dd/yyyy', longTimePattern: 'HH:mm:ss', shortTimePattern: 'HH:mm', dateSeparator: '/',
        monthNames: englishMonths,
        abbreviatedMonthNames: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
        dayNames: englishDays, abbreviatedDayNames: englishShortDays,
    },
    {
        name: 'en-US', twoLetterLanguage: 'en',
        numberDecimalSeparator: '.', numberGroupSeparator: ',', numberGroupSizes: [3], negativeSign: '-',
        currencySymbol: '$', currencyPositivePattern: 0, currencyNegativePattern: 1, currencyDecimalDigits: 2,
        shortDatePattern: 'M/d/yyyy', longTimePattern: 'h:mm:ss tt', shortTimePattern: 'h:mm tt', dateSeparator: '/',
        monthNames: englishMonths,
        abbreviatedMonthNames: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
        dayNames: englishDays, abbreviatedDayNames: englishShortDays,
    },
    {
        name: 'en-GB', twoLetterLanguage: 'en',
        numberDecimalSeparator: '.', numberGroupSeparator: ',', numberGroupSizes: [3], negativeSign: '-',
        currencySymbol: '£', currencyPositivePattern: 0, currencyNegativePattern: 1, currencyDecimalDigits: 2,
        shortDatePattern: 'dd/MM/yyyy', longTimePattern: 'HH:mm:ss', shortTimePattern: 'HH:mm', dateSeparator: '/',
        monthNames: englishMonths,
        abbreviatedMonthNames: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sept', 'Oct', 'Nov', 'Dec'],
        dayNames: englishDays, abbreviatedDayNames: englishShortDays,
    },
    {
        name: 'de-DE', twoLetterLanguage: 'de',
        numberDecimalSeparator: ',', numberGroupSeparator: '.', numberGroupSizes: [3], negativeSign: '-',
        currencySymbol: '€', currencyPositivePattern: 3, currencyNegativePattern: 8, currencyDecimalDigits: 2,
        shortDatePattern: 'dd.MM.yyyy', longTimePattern: 'HH:mm:ss', shortTimePattern: 'HH:mm', dateSeparator: '.',
        monthNames: ['Januar', 'Februar', 'März', 'April', 'Mai', 'Juni', 'Juli', 'August', 'September', 'Oktober', 'November', 'Dezember'],
        abbreviatedMonthNames: ['Jan', 'Feb', 'Mär', 'Apr', 'Mai', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dez'],
        dayNames: ['Sonntag', 'Montag', 'Dienstag', 'Mittwoch', 'Donnerstag', 'Freitag', 'Samstag'],
        abbreviatedDayNames: ['So', 'Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa'],
    },
    {
        name: 'ja-JP', twoLetterLanguage: 'ja',
        numberDecimalSeparator: '.', numberGroupSeparator: ',', numberGroupSizes: [3], negativeSign: '-',
        currencySymbol: '￥', currencyPositivePattern: 0, currencyNegativePattern: 1, currencyDecimalDigits: 0,
        shortDatePattern: 'yyyy/MM/dd', longTimePattern: 'H:mm:ss', shortTimePattern: 'H:mm', dateSeparator: '/',
        monthNames: japaneseMonths, abbreviatedMonthNames: japaneseMonths,
        dayNames: ['日曜日', '月曜日', '火曜日', '水曜日', '木曜日', '金曜日', '土曜日'],
        abbreviatedDayNames: ['日', '月', '火', '水', '木', '金', '土'],
    },
    {
        name: 'fr-FR', twoLetterLanguage: 'fr',
        numberDecimalSeparator: ',', numberGroupSeparator: ' ', numberGroupSizes: [3], negativeSign: '-',
        currencySymbol: '€', currencyPositivePattern: 3, currencyNegativePattern: 8, currencyDecimalDigits: 2,
        shortDatePattern: 'dd/MM/yyyy', longTimePattern: 'HH:mm:ss', shortTimePattern: 'HH:mm', dateSeparator: '/',
        monthNames: ['janvier', 'février', 'mars', 'avril', 'mai', 'juin', 'juillet', 'août', 'septembre', 'octobre', 'novembre', 'décembre'],
        abbreviatedMonthNames: ['janv.', 'févr.', 'mars', 'avr.', 'mai', 'juin', 'juil.', 'août', 'sept.', 'oct.', 'nov.', 'déc.'],
        dayNames: ['dimanche', 'lundi', 'mardi', 'mercredi', 'jeudi', 'vendredi', 'samedi'],
        abbreviatedDayNames: ['dim.', 'lun.', 'mar.', 'mer.', 'jeu.', 'ven.', 'sam.'],
    },
];

/**
 * A Sheet's culture, read from its locale: a BCP 47 tag, or `"invariant"` for .NET's invariant culture
 * (ADR-0370, section 10). Only the locales whose data the engine is held to are known; any other is
 * refused by name, never read as some other culture's.
 */
export class Culture {
    readonly #data: CultureData;

    private constructor(data: CultureData) {
        this.#data = data;
    }

    /** The invariant culture, which reads as .NET's `InvariantCulture` does. */
    static readonly invariant: Culture = new Culture(elementAt(known, 0));

    static readonly #byName = new Map<string, Culture>(known.map(data => [data.name.toLowerCase(), data.name === '' ? Culture.invariant : new Culture(data)]));

    /**
     * The culture of a locale: `"invariant"`, or one of `en-US`, `en-GB`, `de-DE`, `ja-JP` and `fr-FR`, in
     * any case. Another tag is refused with a `RangeError`: the engine holds no data for it that it has
     * checked against .NET's.
     */
    static of(locale: string): Culture {
        if (locale === 'invariant') return Culture.invariant;
        if (locale === '') throw new RangeError('The invariant culture is named "invariant", never an empty tag (ADR-0370, section 10).');
        const culture = Culture.#byName.get(locale.toLowerCase());
        if (culture === undefined) {
            throw new RangeError(`The locale '${locale}' is not one the engine holds data for: en-US, en-GB, de-DE, ja-JP, fr-FR and "invariant".`);
        }
        return culture;
    }

    /** `CultureInfo.Name`: the BCP 47 tag; empty for the invariant culture, as .NET writes it in a Sheet Document. */
    get name(): string {
        return this.#data.name;
    }

    /** The locale the culture was asked for by, `"invariant"` for the invariant culture. */
    get locale(): string {
        return this.#data.name === '' ? 'invariant' : this.#data.name;
    }

    /** The culture's data. */
    get data(): CultureData {
        return this.#data;
    }
}

/** The order of year, month and day in the culture's short date pattern: `mdy`, `dmy` or `ymd` (`ConstantParser.DayOrder`). */
export function dayOrder(culture: Culture): 'mdy' | 'dmy' | 'ymd' {
    let order = '';
    for (const c of culture.data.shortDatePattern) {
        const part = c === 'y' ? 'y' : c === 'M' ? 'm' : c === 'd' ? 'd' : '';
        if (part !== '' && !order.includes(part)) order += part;
    }
    return order === 'dmy' || order === 'ymd' ? order : 'mdy';
}

/** The culture's short date pattern as an Excel format code: `M/d/yyyy` becomes `m/d/yyyy` (`ConstantParser.DateCode`). */
export function dateCode(culture: Culture): string {
    const pattern = culture.data.shortDatePattern;
    let code = '';
    for (let i = 0; i < pattern.length; i++) {
        const c = pattern.charAt(i);
        switch (c) {
            case 'y':
            case 'd':
                code += c;
                break;
            case 'M':
                code += 'm';
                break;
            case '/':
                code += literal(culture.data.dateSeparator);
                break;
            case "'": {
                let close = pattern.indexOf("'", i + 1);
                if (close < 0) close = pattern.length;
                code += literal(pattern.slice(i + 1, close));
                i = close;
                break;
            }
            default:
                code += literal(c);
                break;
        }
    }
    return code;
}

/** Punctuation stands as itself in a format code; anything a code could misread is quoted. */
function literal(text: string): string {
    let code = '';
    for (const c of text) {
        if (/^[A-Za-z0-9";@*_[#?%,]$/.test(c)) {
            code += c === '"' ? '\\"' : `"${c}"`;
        } else {
            code += c;
        }
    }
    return code;
}
