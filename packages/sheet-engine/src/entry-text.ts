// Writes a constant back as the text a user would type for it under a culture (C#'s EntryText.cs), so that the
// Cell Editor opens on something that, typed again, gives the same Entry: a date as the culture's short date, a
// percentage with `%`, any other number in General form.
import { parseConstant } from './constant-parser';
import { dateCode, type Culture } from './culture';
import { maximumSerial } from './date-serial';
import { Entry } from './entry';
import { NumberFormat } from './number-format';
import { writtenText } from './number-text';
import { Value } from './value';

/** The text that, typed under `culture`, enters `constant` again, shown in `format`. */
export function entryText(constant: Value, format: NumberFormat, culture: Culture): string {
    switch (constant.kind) {
        case 'number': {
            const number = constant.number;
            if (format.isDate && number >= 0 && number < maximumSerial + 1) return dateTimeText(number, culture);
            if (format.isPercent) return `${writtenText(number * 100, culture)}%`;
            return writtenText(number, culture);
        }
        case 'boolean':
        case 'error':
            return constant.toString();
        case 'text': {
            const text = constant.text;
            // Text that would be read back as something else keeps Excel's leading apostrophe.
            if (text.length === 0 || text.startsWith('=') || text.startsWith("'") || parseConstant(text, culture).kind !== 'text' || Entry.isSignedFormula(text)) {
                return `'${text}`;
            }
            return text;
        }
        default:
            throw new Error(`Internal: ${String(constant.kind)} is no kind of Value.`);
    }
}

function dateTimeText(serial: number, culture: Culture): string {
    const whole = Math.floor(serial);
    const hasTime = serial !== whole;
    const twelveHour = culture.data.longTimePattern.includes('t');
    const time = NumberFormat.parse(twelveHour ? 'h:mm:ss AM/PM' : 'h:mm:ss');
    if (whole === 0 && hasTime) return time.format(Value.fromNumber(serial), culture).text;
    const date = NumberFormat.parse(dateCode(culture)).format(Value.fromNumber(whole), culture).text;
    return hasTime ? `${date} ${time.format(Value.fromNumber(serial), culture).text}` : date;
}
