// What the Sheet reads of its culture beside what the engine formats with it: the currency symbol, where it
// stands and how many decimals it takes, and whether the short time is twelve-hour. The engine keeps these in
// Culture's data, which it marks internal, and does not export its currencySymbolOf; this module reads them in
// one place until the engine makes them public.

import type { Culture } from '@ex-grid/sheet-engine';

/**
 * The currency symbol Excel shows under a culture: the culture's own, with Windows' ja-JP yen sign U+00A5 where
 * ICU gives the full-width U+FFE5 (ADR-0071), as the engine's currencySymbolOf reads it.
 */
export function currencySymbolOf(culture: Culture): string {
    return culture.data.currencySymbol.replaceAll('￥', '¥');
}

/** `NumberFormatInfo.CurrencyPositivePattern`: 0 is `$n`, 1 `n$`, 2 `$ n`, 3 `n $`. */
export function currencyPositivePatternOf(culture: Culture): number {
    return culture.data.currencyPositivePattern;
}

/** How many decimal places the culture's currency shows. */
export function currencyDecimalDigitsOf(culture: Culture): number {
    return culture.data.currencyDecimalDigits;
}

/** Whether the culture's own short time is twelve-hour: its pattern has an AM/PM designator. */
export function hasTwelveHourTime(culture: Culture): boolean {
    return culture.data.shortTimePattern.includes('t');
}
