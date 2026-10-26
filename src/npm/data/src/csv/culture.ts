// The culture a CSV column reads its numbers and dates in (ADR-0064), as ExGrid.Data reads a CultureInfo's.
// C# names one with a CultureInfo; the Script Core with a locale, a BCP 47 tag such as `de-DE`, and none for
// .NET's invariant culture. A number's decimal point, thousands separator, group sizes and signs, and a
// date's separators, names and calendar, come from `Intl`, which reads the same CLDR data .NET reads on ICU.
// A tag that is not BCP 47's, or whose language the browser holds no data for, is refused; one it knows only
// in part (`de-ZZ`) is read in the nearest it knows, as .NET on ICU reads it. A calendar this reader does not
// keep is refused (`calendarRefusal`).

/** How a culture writes a number. */
export interface NumberCulture {
    readonly decimalPoint: string;
    readonly thousandsSeparator: string;
    /** The sizes of the groups the thousands separator stands between, the rightmost first, the last repeating. */
    readonly groupSizes: readonly number[];
    readonly negativeSign: string;
    readonly positiveSign: string;
}

/** How a culture writes a date: the separators `/` and `:` stand for, and its names, as .NET reads them. */
export interface DateCulture {
    readonly dateSeparator: string;
    readonly timeSeparator: string;
    /** Each month's names, January first: its name alone and its name beside a day, which may differ. */
    readonly months: readonly (readonly string[])[];
    readonly abbreviatedMonths: readonly (readonly string[])[];
    /** Each day's name, Sunday first. */
    readonly days: readonly string[];
    readonly abbreviatedDays: readonly string[];
    readonly am: string;
    readonly pm: string;
    /** The years a date in the culture's calendar is ahead of the Gregorian: 543 in the Thai Buddhist. */
    readonly yearOffset: number;
    /** The year two digits stand for at most: 2049, as .NET's Gregorian calendar takes it. */
    readonly twoDigitYearMax: number;
    /** The locale names are matched in, ignoring case. */
    readonly locale: string | undefined;
}

const invariantNumber: NumberCulture = Object.freeze({
    decimalPoint: '.', thousandsSeparator: ',', groupSizes: Object.freeze([3]), negativeSign: '-', positiveSign: '+',
});

const invariantDate: DateCulture = Object.freeze({
    dateSeparator: '/',
    timeSeparator: ':',
    months: Object.freeze(['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November',
        'December'].map(name => Object.freeze([name]))),
    abbreviatedMonths: Object.freeze(['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
        .map(name => Object.freeze([name]))),
    days: Object.freeze(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']),
    abbreviatedDays: Object.freeze(['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']),
    am: 'AM',
    pm: 'PM',
    yearOffset: 0,
    twoDigitYearMax: 2049,
    locale: undefined,
});

/** Why `locale` cannot be read in: a tag that is not BCP 47's, or one whose language the browser holds no data for. */
export function localeRefusal(locale: string): string | undefined {
    if (locale === '') return 'an empty culture; leave it out for the invariant culture';
    let canonical: readonly string[];
    try {
        canonical = Intl.getCanonicalLocales(locale);
    } catch { // oxlint-disable-line ex-grid/no-catch -- Intl says a tag is not BCP 47's only by throwing; the tag is refused by name
        return `'${locale}', which is not a BCP 47 language tag`;
    }
    if (Intl.NumberFormat.supportedLocalesOf(canonical).length === 0) return `'${locale}', which this platform holds no data for`;
    return undefined;
}

function part(parts: readonly Intl.NumberFormatPart[], type: Intl.NumberFormatPart['type'], what: string): string {
    const found = parts.find(each => each.type === type);
    if (found === undefined) throw new Error(`Internal: Intl wrote no ${what}.`);
    return found.value;
}

const numberCultures = new Map<string, NumberCulture>();

/** How `locale` writes a number, or the invariant culture's for none. */
export function numberCulture(locale: string | undefined): NumberCulture {
    if (locale === undefined) return invariantNumber;
    const known = numberCultures.get(locale);
    if (known !== undefined) return known;
    const format = new Intl.NumberFormat(locale, { useGrouping: 'always', maximumFractionDigits: 1 });
    // Twelve digits show every group size a culture uses: three, or three and then two. The leftmost group
    // may be short, so it says nothing.
    const groups = format.formatToParts(123_456_789_012.5).filter(each => each.type === 'integer').map(each => each.value.length).toReversed().slice(0, -1);
    const [rightmost, next] = groups;
    if (rightmost === undefined) throw new Error(`Internal: Intl wrote no groups for ${locale}.`);
    const culture = Object.freeze({
        decimalPoint: part(format.formatToParts(1.5), 'decimal', 'decimal point'),
        thousandsSeparator: part(format.formatToParts(123_456_789_012.5), 'group', 'thousands separator'),
        groupSizes: Object.freeze(next === undefined || next === rightmost ? [rightmost] : [rightmost, next]),
        negativeSign: part(format.formatToParts(-1), 'minusSign', 'negative sign'),
        positiveSign: part(new Intl.NumberFormat(locale, { signDisplay: 'always' }).formatToParts(1), 'plusSign', 'positive sign'),
    });
    numberCultures.set(locale, culture);
    return culture;
}

/** The calendar `locale` writes its dates in, as Intl names it: `gregory`, `buddhist`, `persian`… */
function calendarOf(locale: string): string {
    return new Intl.DateTimeFormat(locale).resolvedOptions().calendar;
}

/** Why a date cannot be read in `locale`'s calendar here: only the Gregorian and the Thai Buddhist, which
 * counts the Gregorian's years from 543 BC, are kept. */
export function calendarRefusal(locale: string): string | undefined {
    const calendar = calendarOf(locale);
    return calendar === 'gregory' || calendar === 'buddhist' ? undefined : `writes its dates in the ${calendar} calendar, which is not read here`;
}

function literalBetween(parts: readonly Intl.DateTimeFormatPart[], first: Intl.DateTimeFormatPartTypes, what: string): string {
    const at = parts.findIndex(each => each.type === first);
    const after = parts[at + 1];
    if (at < 0 || after?.type !== 'literal') throw new Error(`Internal: Intl wrote no ${what}.`);
    return after.value;
}

const dateCultures = new Map<string, DateCulture>();

/** A day of 2001, at midnight UTC. */
function day(month: number, date: number): Date {
    return new Date(Date.UTC(2001, month, date));
}

/** How `locale` writes a date, or the invariant culture's for none. */
export function dateCulture(locale: string | undefined): DateCulture {
    if (locale === undefined) return invariantDate;
    const known = dateCultures.get(locale);
    if (known !== undefined) return known;
    const utc = { timeZone: 'UTC' } as const;
    const numeric = new Intl.DateTimeFormat(locale, { ...utc, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(day(1, 3));
    // The separator between the first two numbers of a short date, whichever order the culture writes them in.
    const firstNumber = numeric.find(each => each.type === 'year' || each.type === 'month' || each.type === 'day');
    const time = new Intl.DateTimeFormat(locale, { ...utc, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(day(1, 3));
    const monthNames = (width: 'long' | 'short'): readonly (readonly string[])[] => Array.from({ length: 12 }, (_, month) => {
        const alone = new Intl.DateTimeFormat(locale, { ...utc, month: width }).format(day(month, 3));
        const beside = new Intl.DateTimeFormat(locale, { ...utc, month: width, day: 'numeric' }).formatToParts(day(month, 3))
            .find(each => each.type === 'month')?.value;
        return Object.freeze(beside === undefined || beside === alone ? [alone] : [alone, beside]);
    });
    // 2001-01-07 is a Sunday.
    const dayNames = (width: 'long' | 'short'): readonly string[] =>
        Object.freeze(Array.from({ length: 7 }, (_, weekday) => new Intl.DateTimeFormat(locale, { ...utc, weekday: width }).format(day(0, 7 + weekday))));
    const period = (hour: number): string => {
        const found = new Intl.DateTimeFormat(locale, { ...utc, hour: 'numeric', hourCycle: 'h12' }).formatToParts(new Date(Date.UTC(2001, 1, 3, hour)))
            .find(each => each.type === 'dayPeriod')?.value;
        if (found === undefined) throw new Error('Internal: Intl wrote no AM or PM.');
        return found;
    };
    const buddhist = calendarOf(locale) === 'buddhist';
    const culture: DateCulture = Object.freeze({
        dateSeparator: literalBetween(numeric, firstNumber === undefined ? 'year' : firstNumber.type, 'date separator'),
        timeSeparator: literalBetween(time, 'hour', 'time separator'),
        months: Object.freeze(monthNames('long')),
        abbreviatedMonths: Object.freeze(monthNames('short')),
        days: dayNames('long'),
        abbreviatedDays: dayNames('short'),
        am: period(4),
        pm: period(16),
        yearOffset: buddhist ? 543 : 0,
        twoDigitYearMax: buddhist ? 2572 : 2049,
        locale,
    });
    dateCultures.set(locale, culture);
    return culture;
}

/** The languages whose cultures .NET gives a semicolon as their list separator, and those it gives one that is
 * neither a semicolon nor a comma (Arabic script's). .NET on ICU keeps Windows' list separators in a table of
 * its own, not in CLDR, so `Intl` cannot say them; these are .NET 10's, by language, with the cultures that
 * differ from their language's below. */
const semicolonLanguages = new Set((
    'af agq ak am ar asa ast az bas be bem bez bg bm br brx bs ca ce cgg cs cy da dav de dje dsb dua dyo dz ebu ' +
    'ee el eo es et eu ewo ff fi fil fo fr fur fy ga gd gl gsw guz gv ha haw hr hsb hu ia id ig ii is it jgo ' +
    'jmc jv ka kab kam kde kea khq ki kk kkj kl kln ks ksb ksf ksh kw ky lag lb lg lkt ln lo lrc lt lu luo luy ' +
    'lv mas mer mfe mg mgh mgo mk ml mn ms mt mua my mzn naq nb nd nds nl nmg nn nnh no nso nus nyn oc om os pl ' +
    'prg ps pt rm rn ro rof ru rw rwk sah saq sbp sd se seh ses sg shi si sk sl smn sn so sq sr st sv sw te teo ' +
    'tg ti tk tn to tr tt twq tzm uk ur uz vai vun wae wo xh xog yav yi yo zgh zu').split(' '));

const otherListLanguages = new Set(['fa', 'ku', 'nqo']);

/** The cultures whose list separator is not their language's: a comma, or a semicolon. */
const listSeparatorExceptions = new Map<string, ',' | ';'>([
    ...(
        'ar-EH bm-ML es-BZ es-EA es-IC es-MX es-US fa-AF ff-Adlm ff-Adlm-BF ff-Adlm-CM ff-Adlm-GH ff-Adlm-GM ' +
        'ff-Adlm-GN ff-Adlm-GW ff-Adlm-LR ff-Adlm-MR ff-Adlm-NE ff-Adlm-NG ff-Adlm-SL ff-Adlm-SN ff-Latn-BF ' +
        'ff-Latn-CM ff-Latn-GH ff-Latn-GM ff-Latn-GN ff-Latn-GW ff-Latn-LR ff-Latn-MR ff-Latn-NE ff-Latn-NG ' +
        'ff-Latn-SL ga-GB ha-GH ha-NE ha-NG it-VA jv-ID kk-Cyrl kk-Cyrl-KZ ks-Deva ks-Deva-IN ku-TR ms-ID oc-ES ' +
        'prg-PL ps-PK sd-Deva sd-Deva-IN sn-ZW tg-TJ tzm-MA yi-UA zgh-MA').split(' ').map(name => [name.toLowerCase(), ','] as const),
    ...(
        'bo-IN en-ID ko-KP ne-IN pa-Arab pa-Arab-PK ta-LK ta-MY ta-SG zh-Hans-HK zh-Hans-MO').split(' ').map(name => [name.toLowerCase(), ';'] as const),
]);

/** The character `locale` separates the items of a list with, as .NET's `TextInfo.ListSeparator` gives it, when
 * it is a comma or a semicolon; `undefined` when it is another (Arabic script's). The invariant culture's is a
 * comma. A tag .NET holds no culture for is taken by its language, where .NET would give a comma. */
export function listSeparatorOf(locale: string | undefined): ',' | ';' | undefined {
    if (locale === undefined) return ',';
    // A culture is named by its language, script and region; extensions and variants name none .NET holds.
    const { language, script, region } = new Intl.Locale(locale);
    const names = [[language, script, region], [language, region], [language, script]]
        .map(parts => parts.filter(each => each !== undefined).join('-').toLowerCase());
    for (const name of names) {
        const exception = listSeparatorExceptions.get(name);
        if (exception !== undefined) return exception;
    }
    if (otherListLanguages.has(language)) return undefined;
    return semicolonLanguages.has(language) ? ';' : ',';
}

/** Whether `locale` writes a short date with its day before its month, as .NET's `ShortDatePattern` does, which
 * .NET on ICU takes from the same CLDR pattern `Intl`'s short date style writes. The invariant culture's
 * (`MM/dd/yyyy`) has the month first. */
export function dayFirst(locale: string | undefined): boolean {
    if (locale === undefined) return false;
    const known = daysFirst.get(locale);
    if (known !== undefined) return known;
    const parts = new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeZone: 'UTC' }).formatToParts(0);
    const first = parts.find(each => each.type === 'day' || each.type === 'month' || each.type === 'year');
    const answer = first?.type === 'day';
    daysFirst.set(locale, answer);
    return answer;
}

const daysFirst = new Map<string, boolean>();
