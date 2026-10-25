// The declared set of functions (ADR-0047; C#'s FunctionLibrary.cs and the declarations of Functions.cs): ADR-0047's
// first list, and the functions admitted after it (docs/specs/exsheet-functions/spec.md). Each function is
// written to Microsoft's documented behaviour for it: how it treats blanks, text, booleans and Error Values typed
// as arguments and found inside ranges, and its rounding. Where ExSheet cannot give Excel's answer — a result
// that would spill an array, XLOOKUP's binary search over data it cannot show to be sorted — it gives an Error
// Value instead of another answer. A name outside the set is `#NAME?`.
import { compareOrdinal, ignoreCaseKey } from '../dotnet';
import { type FunctionCall, FunctionDefinition, type ThreeDArgument } from './function-definition';
import type { Operand } from './operand';
import * as arrays from './functions/arrays';
import * as core from './functions/core';
import * as criteria from './functions/criteria';
import * as dates from './functions/dates';
import * as financial from './functions/financial';
import * as logic from './functions/logic';
import * as lookup from './functions/lookup';
import * as math from './functions/math';
import * as subtotal from './functions/subtotal';
import * as text from './functions/text';
import * as trigonometry from './functions/trigonometry';

/** The most arguments an open-ended function takes: Excel's limit. */
const open = 255;

// threeD: what a 3D Reference given as each argument is to the function, as Excel answered for every
// function ExSheet declares (ADR-0331 §7; verification/2026-10-24-windows-workbook-12). One left out
// reads it as one Value at every argument: #REF!.
const across = (): ThreeDArgument => 'range';

const acrossFirst = (index: number): ThreeDArgument => index === 0 ? 'range' : 'value';

const acrossSecond = (index: number): ThreeDArgument => index === 1 ? 'range' : 'value';

// TEXTJOIN reads its delimiters across a span, and its texts; ignore_empty is one Value.
const acrossTexts = (index: number): ThreeDArgument => index === 1 ? 'value' : 'range';

// IF's and CHOOSE's values, and IFERROR's and IFNA's fallback.
const handedOnAfterFirst = (index: number): ThreeDArgument => index === 0 ? 'value' : 'handOn';

const ifsValues = (index: number): ThreeDArgument => index % 2 === 1 ? 'valueError' : 'value';

// SWITCH's results stand after each value, and its default last, after the expression and its pairs.
const switchResults = (index: number, count: number): ThreeDArgument =>
    (index >= 2 && index % 2 === 0) || (count % 2 === 0 && index === count - 1) ? 'valueError' : 'value';

const oneSheetFirst = (index: number): ThreeDArgument => index === 0 ? 'oneSheet' : 'value';

const oneSheetSecond = (index: number): ThreeDArgument => index === 1 ? 'oneSheet' : 'value';

const oneSheetFirstTwo = (index: number): ThreeDArgument => index <= 1 ? 'oneSheet' : 'value';

// SUMIF's and AVERAGEIF's range and result range; their criterion is one Value.
const oneSheetCriteria = (index: number): ThreeDArgument => index !== 1 ? 'oneSheet' : 'value';

// SUMIFS and its partners: the result range, then each criteria range before its criterion.
const oneSheetResultAndCriteria = (index: number): ThreeDArgument => index === 0 || index % 2 === 1 ? 'oneSheet' : 'value';

const oneSheetCountCriteria = (index: number): ThreeDArgument => index % 2 === 0 ? 'oneSheet' : 'value';

// VLOOKUP's and HLOOKUP's table and the index after it.
const oneSheetTable = (index: number): ThreeDArgument => index === 1 || index === 2 ? 'oneSheet' : 'value';

// MATCH's array, and its match_type, which is #VALUE! where XMATCH's modes are #REF!.
const matchArguments = (index: number): ThreeDArgument => index === 1 ? 'oneSheet' : index === 2 ? 'valueError' : 'value';

// XLOOKUP's arrays, and its if_not_found, #VALUE! only where it is read.
const xLookupArguments = (index: number): ThreeDArgument => index === 1 || index === 2 ? 'oneSheet' : index === 3 ? 'valueError' : 'value';

const oneSheetAfterFirst = (index: number): ThreeDArgument => index >= 1 ? 'oneSheet' : 'value';

const oneSheetAfterSecond = (index: number): ThreeDArgument => index >= 2 ? 'oneSheet' : 'value';

const valueErrorEverywhere = (): ThreeDArgument => 'valueError';

/** ROUNDUP: away from zero. */
function roundUp(call: FunctionCall): Operand {
    return core.roundBy(call, 'awayFromZero');
}

/** ROUNDDOWN: toward zero. */
function roundDown(call: FunctionCall): Operand {
    return core.roundBy(call, 'towardZero');
}

/**
 * The declarations, made when first asked for: the function modules import the evaluator, which imports the
 * parser, which reads this table, so building it as the module loads would read a module not yet evaluated.
 */
function declare(): FunctionDefinition[] {
    return [
        new FunctionDefinition('SUM', 1, open, 'number1, [number2], ...', 'Adds its arguments.', core.sum, { threeD: across }),
        new FunctionDefinition('AVERAGE', 1, open, 'number1, [number2], ...', 'Returns the average (arithmetic mean) of its arguments.', core.average, { threeD: across }),
        new FunctionDefinition('MIN', 1, open, 'number1, [number2], ...', 'Returns the smallest number in a set of values.', core.min, { threeD: across }),
        new FunctionDefinition('MAX', 1, open, 'number1, [number2], ...', 'Returns the largest number in a set of values.', core.max, { threeD: across }),
        new FunctionDefinition('COUNT', 1, open, 'value1, [value2], ...', 'Counts how many numbers are in the list of arguments.', core.count, { threeD: across }),
        new FunctionDefinition('COUNTA', 1, open, 'value1, [value2], ...', 'Counts how many values are in the list of arguments.', core.countA, { threeD: across }),
        new FunctionDefinition('SUMIF', 2, 3, 'range, criteria, [sum_range]', 'Adds numbers whose corresponding cells meet a condition.', criteria.sumIf, { threeD: oneSheetCriteria }),
        new FunctionDefinition('SUMIFS', 3, open, 'sum_range, criteria_range1, criteria1, ...', 'Adds numbers whose corresponding cells meet every condition.', criteria.sumIfs, { threeD: oneSheetResultAndCriteria, inPairs: true, pairOffset: 1 }),
        new FunctionDefinition('COUNTIF', 2, 2, 'range, criteria', 'Counts cells that meet a condition.', criteria.countIf, { threeD: oneSheetFirst }),
        new FunctionDefinition('COUNTIFS', 2, 254, 'criteria_range1, criteria1, ...', 'Counts cells that meet every condition.', criteria.countIfs, { threeD: oneSheetCountCriteria, inPairs: true }),
        new FunctionDefinition('AVERAGEIF', 2, 3, 'range, criteria, [average_range]', 'Averages numbers whose corresponding cells meet a condition.', criteria.averageIf, { threeD: oneSheetCriteria }),
        new FunctionDefinition('AVERAGEIFS', 3, open, 'average_range, criteria_range1, criteria1, ...', 'Averages numbers whose corresponding cells meet every condition.', criteria.averageIfs, { threeD: oneSheetResultAndCriteria, inPairs: true, pairOffset: 1 }),
        new FunctionDefinition('MAXIFS', 3, open, 'max_range, criteria_range1, criteria1, ...', 'Returns the greatest number whose corresponding cells meet every condition.', criteria.maxIfs, { threeD: oneSheetResultAndCriteria, inPairs: true, pairOffset: 1 }),
        new FunctionDefinition('MINIFS', 3, open, 'min_range, criteria_range1, criteria1, ...', 'Returns the least number whose corresponding cells meet every condition.', criteria.minIfs, { threeD: oneSheetResultAndCriteria, inPairs: true, pairOffset: 1 }),
        new FunctionDefinition('COUNTBLANK', 1, 1, 'range', 'Counts blank cells and cells whose formulas return empty text.', criteria.countBlank, { threeD: oneSheetFirst }),
        new FunctionDefinition('IF', 2, 3, 'logical_test, value_if_true, [value_if_false]', 'Returns one value if a condition is TRUE and another if it is FALSE.', core.ifFunction, { threeD: handedOnAfterFirst, returnsReference: true }),
        new FunctionDefinition('ROUND', 2, 2, 'number, num_digits', 'Rounds a number to a specified number of digits, half away from zero.', core.round),
        new FunctionDefinition('IFERROR', 2, 2, 'value, value_if_error', 'Returns value_if_error if value is an Error Value, and value otherwise. #GETTING_DATA is not an error to it.', core.ifError, { threeD: handedOnAfterFirst }),
        new FunctionDefinition('ISERROR', 1, 1, 'value', 'Returns TRUE if value is an Error Value. #GETTING_DATA is not an error to it.', core.isError),
        new FunctionDefinition('AND', 1, open, 'logical1, [logical2], ...', 'Returns TRUE if all of its arguments are TRUE.', logic.and, { threeD: across }),
        new FunctionDefinition('OR', 1, open, 'logical1, [logical2], ...', 'Returns TRUE if any argument is TRUE.', logic.or, { threeD: across }),
        new FunctionDefinition('NOT', 1, 1, 'logical', 'Reverses the logic of its argument.', logic.not),
        new FunctionDefinition('IFNA', 2, 2, 'value, value_if_na', 'Returns value_if_na if value is #N/A, and value otherwise.', logic.ifNa, { threeD: handedOnAfterFirst }),
        new FunctionDefinition('ISBLANK', 1, 1, 'value', 'Returns TRUE if value refers to an empty cell.', logic.isBlank),
        new FunctionDefinition('ISNUMBER', 1, 1, 'value', 'Returns TRUE if value is a number.', logic.isNumber),
        new FunctionDefinition('ROUNDUP', 2, 2, 'number, num_digits', 'Rounds a number up, away from zero.', roundUp),
        new FunctionDefinition('ROUNDDOWN', 2, 2, 'number, num_digits', 'Rounds a number down, toward zero.', roundDown),
        new FunctionDefinition('ABS', 1, 1, 'number', 'Returns the absolute value of a number.', math.abs),
        new FunctionDefinition('INT', 1, 1, 'number', 'Rounds a number down to the nearest integer.', math.int),
        new FunctionDefinition('CEILING.MATH', 1, 3, 'number, [significance], [mode]', 'Rounds a number up to a multiple of significance.', math.ceilingMath),
        new FunctionDefinition('FLOOR.MATH', 1, 3, 'number, [significance], [mode]', 'Rounds a number down to a multiple of significance.', math.floorMath),
        new FunctionDefinition('MROUND', 2, 2, 'number, multiple', 'Rounds a number to a whole-number multiple; fractional multiples are refused.', math.mRound, { threeD: valueErrorEverywhere }),
        new FunctionDefinition('YEARFRAC', 2, 3, 'start_date, end_date, [basis]',
            'Returns the year fraction represented by the whole days between two dates.',
            dates.yearFrac, {
            threeD: valueErrorEverywhere,
            values: {
                2: [
                    { value: '0', text: '0 - US (NASD) 30/360' },
                    { value: '1', text: '1 - Actual/actual' },
                    { value: '2', text: '2 - Actual/360' },
                    { value: '3', text: '3 - Actual/365' },
                    { value: '4', text: '4 - European 30/360' },
                ],
            },
        }),
        new FunctionDefinition('DATEDIF', 3, 3, 'start_date, end_date, unit', 'Returns the difference between two dates in whole years, months or days.', dates.dateDif),
        new FunctionDefinition('MOD', 2, 2, 'number, divisor', 'Returns the remainder from division, with the sign of the divisor.', math.mod),
        new FunctionDefinition('DATE', 3, 3, 'year, month, day', 'Returns the serial number of a particular date.', dates.dateFunction),
        new FunctionDefinition('TODAY', 0, 0, '', 'Returns the serial number of today\'s date: the Sheet Day.', dates.today),
        new FunctionDefinition('YEAR', 1, 1, 'serial_number', 'Converts a serial number to a year.', dates.yearFunction),
        new FunctionDefinition('MONTH', 1, 1, 'serial_number', 'Converts a serial number to a month.', dates.monthFunction),
        new FunctionDefinition('DAY', 1, 1, 'serial_number', 'Converts a serial number to a day of the month.', dates.dayFunction),
        new FunctionDefinition('EOMONTH', 2, 2, 'start_date, months', 'Returns the serial number of the last day of the month before or after a specified number of months.', dates.eoMonth, { threeD: valueErrorEverywhere }),
        new FunctionDefinition('EDATE', 2, 2, 'start_date, months', 'Returns the serial number of the date that is the indicated number of months before or after the start date.', dates.eDate, { threeD: valueErrorEverywhere }),
        new FunctionDefinition('LEFT', 1, 2, 'text, [num_chars]', 'Returns the leftmost characters from a text value.', text.left),
        new FunctionDefinition('RIGHT', 1, 2, 'text, [num_chars]', 'Returns the rightmost characters from a text value.', text.right),
        new FunctionDefinition('MID', 3, 3, 'text, start_num, num_chars', 'Returns a specific number of characters from a text string, starting at the position you specify.', text.mid),
        new FunctionDefinition('LEN', 1, 1, 'text', 'Returns the number of characters in a text string.', text.len),
        new FunctionDefinition('TRIM', 1, 1, 'text', 'Removes spaces from text, leaving single spaces between words.', text.trim),
        new FunctionDefinition('TEXT', 2, 2, 'value, format_text', 'Formats a number and converts it to text.', text.formatAsText),
        new FunctionDefinition('CONCAT', 1, 253, 'text1, [text2], ...', 'Combines the text from multiple ranges and strings.', text.concat, { threeD: across }),
        new FunctionDefinition('VLOOKUP', 3, 4, 'lookup_value, table_array, col_index_num, [range_lookup]',
            'Searches the first column of a table and returns the value in the specified column of the matching row.',
            lookup.vLookup, {
            threeD: oneSheetTable,
            values: {
                3: [
                    { value: 'TRUE', text: 'TRUE - Approximate match' },
                    { value: 'FALSE', text: 'FALSE - Exact match' },
                ],
            },
        }),
        new FunctionDefinition('HLOOKUP', 3, 4, 'lookup_value, table_array, row_index_num, [range_lookup]',
            'Searches the first row of a table and returns the value in the specified row of the matching column.',
            lookup.hLookup, {
            threeD: oneSheetTable,
            values: {
                3: [
                    { value: 'TRUE', text: 'TRUE - Approximate match' },
                    { value: 'FALSE', text: 'FALSE - Exact match' },
                ],
            },
        }),
        new FunctionDefinition('MATCH', 2, 3, 'lookup_value, lookup_array, [match_type]',
            'Returns the relative position of an item in an array or range of cells.',
            lookup.matchFunction, {
            threeD: matchArguments,
            values: {
                2: [
                    { value: '1', text: '1 - Less than' },
                    { value: '0', text: '0 - Exact match' },
                    { value: '-1', text: '-1 - Greater than' },
                ],
            },
        }),
        new FunctionDefinition('INDEX', 2, 3, 'array, row_num, [column_num]', 'Returns the value of the cell at the intersection of a row and a column of a range.', lookup.indexFunction, { threeD: oneSheetFirst, returnsReference: true }),
        new FunctionDefinition('PRODUCT', 1, open, 'number1, [number2], ...', 'Multiplies its arguments.', math.product, { threeD: across }),
        new FunctionDefinition('MEDIAN', 1, open, 'number1, [number2], ...', 'Returns the median of the given numbers.', math.median, { threeD: across }),
        new FunctionDefinition('LARGE', 2, 2, 'array, k', 'Returns the k-th largest value in a data set.', math.large, { threeD: acrossFirst }),
        new FunctionDefinition('SMALL', 2, 2, 'array, k', 'Returns the k-th smallest value in a data set.', math.small, { threeD: acrossFirst }),
        new FunctionDefinition('ISTEXT', 1, 1, 'value', 'Returns TRUE if value is text.', logic.isText),
        new FunctionDefinition('ISNA', 1, 1, 'value', 'Returns TRUE if value is the #N/A error value.', logic.isNa),
        new FunctionDefinition('IFS', 2, 254, 'logical_test1, value_if_true1, ...', 'Checks whether one or more conditions are met, and returns a value that corresponds to the first TRUE condition.', logic.ifs, { threeD: ifsValues, inPairs: true, returnsReference: true }),
        new FunctionDefinition('SWITCH', 3, 254, 'expression, value1, result1, [default_or_value2], [result2], ...', 'Evaluates an expression against a list of values, and returns the result corresponding to the first matching value.', logic.switchFunction, { threeD: switchResults, returnsReference: true }),
        new FunctionDefinition('NA', 0, 0, '', 'Returns the error value #N/A.', logic.na),
        new FunctionDefinition('ERROR.TYPE', 1, 1, 'error_val', 'Returns a number corresponding to an error type.', logic.errorType),
        new FunctionDefinition('XMATCH', 2, 4, 'lookup_value, lookup_array, [match_mode], [search_mode]', 'Returns the relative position of an item in an array or range of cells.', lookup.xMatch, { threeD: oneSheetSecond }),
        new FunctionDefinition('CHOOSE', 2, open, 'index_num, value1, [value2], ...', 'Chooses a value from a list of values.', lookup.choose, { threeD: handedOnAfterFirst, returnsReference: true }),
        new FunctionDefinition('ROW', 0, 1, '[reference]', 'Returns the row number of a reference.', lookup.rowFunction, { threeD: oneSheetFirst }),
        new FunctionDefinition('COLUMN', 0, 1, '[reference]', 'Returns the column number of a reference.', lookup.columnFunction, { threeD: oneSheetFirst }),
        new FunctionDefinition('TRUNC', 1, 2, 'number, [num_digits]', 'Truncates a number to an integer, or to num_digits.', math.trunc),
        new FunctionDefinition('POWER', 2, 2, 'number, power', 'Returns the result of a number raised to a power.', math.power),
        new FunctionDefinition('SQRT', 1, 1, 'number', 'Returns a positive square root.', math.sqrt),
        new FunctionDefinition('WEEKDAY', 1, 2, 'serial_number, [return_type]', 'Converts a serial number to a day of the week.', dates.weekday),
        new FunctionDefinition('DAYS', 2, 2, 'end_date, start_date', 'Returns the number of days between two dates.', dates.daysFunction),
        new FunctionDefinition('NETWORKDAYS', 2, 3, 'start_date, end_date, [holidays]', 'Returns the number of whole workdays between two dates.', dates.networkDays, { threeD: valueErrorEverywhere }),
        new FunctionDefinition('WORKDAY', 2, 3, 'start_date, days, [holidays]', 'Returns the serial number of the date before or after a specified number of workdays.', dates.workday, { threeD: valueErrorEverywhere }),
        new FunctionDefinition('TEXTJOIN', 3, 252, 'delimiter, ignore_empty, text1, [text2], ...', 'Combines the text from multiple ranges and strings, with a delimiter between each value.', text.textJoin, { threeD: acrossTexts }),
        new FunctionDefinition('CONCATENATE', 1, open, 'text1, [text2], ...', 'Joins several text items into one text item.', text.concatenate),
        new FunctionDefinition('SUBSTITUTE', 3, 4, 'text, old_text, new_text, [instance_num]', 'Substitutes new text for old text in a text string.', text.substitute),
        new FunctionDefinition('REPLACE', 4, 4, 'old_text, start_num, num_chars, new_text', 'Replaces characters within text.', text.replace),
        new FunctionDefinition('LOWER', 1, 1, 'text', 'Converts text to lowercase.', text.lower),
        new FunctionDefinition('PROPER', 1, 1, 'text', 'Capitalizes the first letter of each word.', text.proper),
        new FunctionDefinition('SEARCH', 2, 3, 'find_text, within_text, [start_num]', 'Finds one text value within another, ignoring case and accepting wildcards.', text.searchText),
        new FunctionDefinition('UPPER', 1, 1, 'text', 'Converts text to uppercase.', text.upper),
        new FunctionDefinition('FIND', 2, 3, 'find_text, within_text, [start_num]', 'Finds one text value within another, case-sensitive.', text.find),
        new FunctionDefinition('PMT', 3, 5, 'rate, nper, pv, [fv], [type]', 'Returns the periodic payment for an annuity.', financial.pmt),
        new FunctionDefinition('PV', 3, 5, 'rate, nper, pmt, [fv], [type]', 'Returns the present value of an investment.', financial.pv),
        new FunctionDefinition('FV', 3, 5, 'rate, nper, pmt, [pv], [type]', 'Returns the future value of an investment.', financial.fv),
        new FunctionDefinition('RANK.EQ', 2, 3, 'number, ref, [order]', 'Returns the rank of a number in a list of numbers.', math.rankEq, { threeD: acrossSecond, referenceOnly: { first: 1, last: 1 } }),
        new FunctionDefinition('STDEV.S', 1, open, 'number1, [number2], ...', 'Estimates standard deviation based on a sample.', math.stdevS, { threeD: across }),
        new FunctionDefinition('STDEV.P', 1, open, 'number1, [number2], ...', 'Calculates standard deviation based on the entire population.', math.stdevP, { threeD: across }),
        new FunctionDefinition('VAR.S', 1, open, 'number1, [number2], ...', 'Estimates variance based on a sample.', math.varS, { threeD: across }),
        new FunctionDefinition('VAR.P', 1, open, 'number1, [number2], ...', 'Calculates variance based on the entire population.', math.varP, { threeD: across }),
        new FunctionDefinition('XOR', 1, open, 'logical1, [logical2], ...', 'Returns TRUE if an odd number of its arguments are TRUE.', logic.xor, { threeD: across }),
        new FunctionDefinition('SUBTOTAL', 2, open, 'function_num, ref1, [ref2], ...',
            'Returns a subtotal in a list or database.',
            subtotal.subtotal, {
            threeD: oneSheetAfterFirst,
            referenceOnly: { first: 1, last: open - 1 },
            values: {
                0: [
                    { value: '1', text: '1 - AVERAGE' },
                    { value: '2', text: '2 - COUNT' },
                    { value: '3', text: '3 - COUNTA' },
                    { value: '4', text: '4 - MAX' },
                    { value: '5', text: '5 - MIN' },
                    { value: '6', text: '6 - PRODUCT' },
                    { value: '7', text: '7 - STDEV.S' },
                    { value: '8', text: '8 - STDEV.P' },
                    { value: '9', text: '9 - SUM' },
                    { value: '10', text: '10 - VAR.S' },
                    { value: '11', text: '11 - VAR.P' },
                    { value: '101', text: '101 - AVERAGE' },
                    { value: '102', text: '102 - COUNT' },
                    { value: '103', text: '103 - COUNTA' },
                    { value: '104', text: '104 - MAX' },
                    { value: '105', text: '105 - MIN' },
                    { value: '106', text: '106 - PRODUCT' },
                    { value: '107', text: '107 - STDEV.S' },
                    { value: '108', text: '108 - STDEV.P' },
                    { value: '109', text: '109 - SUM' },
                    { value: '110', text: '110 - VAR.S' },
                    { value: '111', text: '111 - VAR.P' },
                ],
            },
        }),
        new FunctionDefinition('AGGREGATE', 3, open, 'function_num, options, ref1, [ref2], ...',
            'Returns an aggregate in a list or database.',
            subtotal.aggregate, {
            threeD: oneSheetAfterSecond,
            referenceOnly: { first: 4, last: open - 1 },
            values: {
                0: [
                    { value: '1', text: '1 - AVERAGE' },
                    { value: '2', text: '2 - COUNT' },
                    { value: '3', text: '3 - COUNTA' },
                    { value: '4', text: '4 - MAX' },
                    { value: '5', text: '5 - MIN' },
                    { value: '6', text: '6 - PRODUCT' },
                    { value: '7', text: '7 - STDEV.S' },
                    { value: '8', text: '8 - STDEV.P' },
                    { value: '9', text: '9 - SUM' },
                    { value: '10', text: '10 - VAR.S' },
                    { value: '11', text: '11 - VAR.P' },
                    { value: '12', text: '12 - MEDIAN' },
                    { value: '14', text: '14 - LARGE' },
                    { value: '15', text: '15 - SMALL' },
                ],
                1: [
                    { value: '0', text: '0 - Ignore nested SUBTOTAL and AGGREGATE functions' },
                    { value: '1', text: '1 - Ignore hidden rows, nested SUBTOTAL and AGGREGATE functions' },
                    { value: '2', text: '2 - Ignore error values, nested SUBTOTAL and AGGREGATE functions' },
                    { value: '3', text: '3 - Ignore hidden rows, error values, nested SUBTOTAL and AGGREGATE functions' },
                    { value: '4', text: '4 - Ignore nothing' },
                    { value: '5', text: '5 - Ignore hidden rows' },
                    { value: '6', text: '6 - Ignore error values' },
                    { value: '7', text: '7 - Ignore hidden rows and error values' },
                ],
            },
        }),
        new FunctionDefinition('ROWS', 1, 1, 'array', 'Returns the number of rows in a reference.', lookup.rowsFunction, { threeD: oneSheetFirst }),
        new FunctionDefinition('COLUMNS', 1, 1, 'array', 'Returns the number of columns in a reference.', lookup.columnsFunction, { threeD: oneSheetFirst }),
        new FunctionDefinition('SIGN', 1, 1, 'number', 'Returns the sign of a number: 1, 0 or -1.', math.signFunction),
        new FunctionDefinition('EXP', 1, 1, 'number', 'Returns e raised to the power of a given number.', math.exp),
        new FunctionDefinition('LN', 1, 1, 'number', 'Returns the natural logarithm of a number.', math.ln),
        new FunctionDefinition('LOG10', 1, 1, 'number', 'Returns the base-10 logarithm of a number.', math.log10),
        new FunctionDefinition('PI', 0, 0, '', 'Returns the value of pi.', math.pi),
        new FunctionDefinition('TIME', 3, 3, 'hour, minute, second', 'Returns the serial number of a particular time.', dates.time),
        new FunctionDefinition('HOUR', 1, 1, 'serial_number', 'Converts a serial number to an hour.', dates.hourFunction),
        new FunctionDefinition('MINUTE', 1, 1, 'serial_number', 'Converts a serial number to a minute.', dates.minuteFunction),
        new FunctionDefinition('SECOND', 1, 1, 'serial_number', 'Converts a serial number to a second.', dates.secondFunction),
        new FunctionDefinition('REPT', 2, 2, 'text, number_times', 'Repeats text a given number of times.', text.rept),
        new FunctionDefinition('EXACT', 2, 2, 'text1, text2', 'Checks to see if two text values are identical, case-sensitive.', text.exact),
        new FunctionDefinition('NUMBERVALUE', 1, 3, 'text, [decimal_separator], [group_separator]', 'Converts text to a number with the separators given.', text.numberValue),
        new FunctionDefinition('XNPV', 3, 3, 'rate, values, dates', 'Returns the net present value for a schedule of cash flows that is not necessarily periodic.', financial.xNpv, { threeD: valueErrorEverywhere }),
        new FunctionDefinition('VALUE', 1, 1, 'text', 'Converts a text argument to a number, read under the Sheet\'s culture.', text.valueOf),
        new FunctionDefinition('NOW', 0, 0, '', 'Returns the serial number of the current date and time.', dates.now),
        new FunctionDefinition('RAND', 0, 0, '', 'Returns a random number from 0 up to 1, drawn from the Sheet Seed.', math.rand),
        new FunctionDefinition('RANDBETWEEN', 2, 2, 'bottom, top', 'Returns a random whole number between the numbers you specify, drawn from the Sheet Seed.', math.randBetween, { threeD: valueErrorEverywhere }),
        new FunctionDefinition('OFFSET', 3, 5, 'reference, rows, cols, [height], [width]', 'Returns a reference offset from a given reference.', lookup.offset, { threeD: oneSheetFirst, returnsReference: true }),
        new FunctionDefinition('INDIRECT', 1, 2, 'ref_text, [a1]', 'Returns a reference indicated by a text value.', lookup.indirect, { returnsReference: true }),
        new FunctionDefinition('FILTER', 2, 3, 'array, include, [if_empty]', 'Filters a range of data based on criteria you define.', arrays.filter, { threeD: oneSheetFirstTwo }),
        new FunctionDefinition('UNIQUE', 1, 3, 'array, [by_col], [exactly_once]', 'Returns a list of unique values in a list or range.', arrays.unique),
        new FunctionDefinition('SORT', 1, 4, 'array, [sort_index], [sort_order], [by_col]', 'Sorts the contents of a range or array.', arrays.sort),
        new FunctionDefinition('SORTBY', 2, open, 'array, by_array1, [sort_order1], ...', 'Sorts the contents of a range or array based on the values in a corresponding range or array.', arrays.sortBy),
        new FunctionDefinition('SEQUENCE', 1, 4, 'rows, [columns], [start], [step]', 'Generates a list of sequential numbers in an array.', arrays.sequence),
        new FunctionDefinition('TRANSPOSE', 1, 1, 'array', 'Returns the transpose of an array.', arrays.transpose),
        new FunctionDefinition('LET', 3, 253, 'name1, name_value1, calculation_or_name2, [name_value2, calculation_or_name3...]', 'Assigns names to calculation results, to be used inside the Formula.', logic.letFunction, { returnsReference: true }),
        new FunctionDefinition('SUMPRODUCT', 1, open, 'array1, [array2], ...', 'Returns the sum of the products of corresponding array components.', arrays.sumProduct),
        new FunctionDefinition('LOG', 1, 2, 'number, [base]', 'Returns the logarithm of a number to the base you specify.', trigonometry.log),
        new FunctionDefinition('SIN', 1, 1, 'number', 'Returns the sine of an angle.', trigonometry.sin),
        new FunctionDefinition('COS', 1, 1, 'number', 'Returns the cosine of an angle.', trigonometry.cos),
        new FunctionDefinition('TAN', 1, 1, 'number', 'Returns the tangent of an angle.', trigonometry.tan),
        new FunctionDefinition('COT', 1, 1, 'number', 'Returns the cotangent of an angle.', trigonometry.cot),
        new FunctionDefinition('CSC', 1, 1, 'number', 'Returns the cosecant of an angle.', trigonometry.csc),
        new FunctionDefinition('SEC', 1, 1, 'number', 'Returns the secant of an angle.', trigonometry.sec),
        new FunctionDefinition('ASIN', 1, 1, 'number', 'Returns the arcsine of a number.', trigonometry.asin),
        new FunctionDefinition('ACOS', 1, 1, 'number', 'Returns the arccosine of a number.', trigonometry.acos),
        new FunctionDefinition('ATAN', 1, 1, 'number', 'Returns the arctangent of a number.', trigonometry.atan),
        new FunctionDefinition('ATAN2', 2, 2, 'x_num, y_num', 'Returns the arctangent from x- and y-coordinates.', trigonometry.atan2),
        new FunctionDefinition('ACOT', 1, 1, 'number', 'Returns the arccotangent of a number.', trigonometry.acot),
        new FunctionDefinition('SINH', 1, 1, 'number', 'Returns the hyperbolic sine of a number.', trigonometry.sinh),
        new FunctionDefinition('COSH', 1, 1, 'number', 'Returns the hyperbolic cosine of a number.', trigonometry.cosh),
        new FunctionDefinition('TANH', 1, 1, 'number', 'Returns the hyperbolic tangent of a number.', trigonometry.tanh),
        new FunctionDefinition('COTH', 1, 1, 'number', 'Returns the hyperbolic cotangent of a number.', trigonometry.coth),
        new FunctionDefinition('CSCH', 1, 1, 'number', 'Returns the hyperbolic cosecant of an angle.', trigonometry.csch),
        new FunctionDefinition('SECH', 1, 1, 'number', 'Returns the hyperbolic secant of an angle.', trigonometry.sech),
        new FunctionDefinition('ASINH', 1, 1, 'number', 'Returns the inverse hyperbolic sine of a number.', trigonometry.asinh),
        new FunctionDefinition('ACOSH', 1, 1, 'number', 'Returns the inverse hyperbolic cosine of a number.', trigonometry.acosh),
        new FunctionDefinition('ATANH', 1, 1, 'number', 'Returns the inverse hyperbolic tangent of a number.', trigonometry.atanh),
        new FunctionDefinition('ACOTH', 1, 1, 'number', 'Returns the hyperbolic arccotangent of a number.', trigonometry.acoth),
        new FunctionDefinition('DEGREES', 1, 1, 'angle', 'Converts radians to degrees.', trigonometry.degrees),
        new FunctionDefinition('RADIANS', 1, 1, 'angle', 'Converts degrees to radians.', trigonometry.radians),
        new FunctionDefinition('XLOOKUP', 3, 6, 'lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode]',
            'Searches a range for a match and returns the corresponding item of a second range.',
            lookup.xLookup, {
            threeD: xLookupArguments,
            returnsReference: true,
            values: {
                4: [
                    { value: '0', text: '0 - Exact match' },
                    { value: '-1', text: '-1 - Exact match or next smaller item' },
                    { value: '1', text: '1 - Exact match or next larger item' },
                    { value: '2', text: '2 - Wildcard character match' },
                    { value: '3', text: '3 - Regex match' },
                ],
                5: [
                    { value: '1', text: '1 - Search first-to-last' },
                    { value: '-1', text: '-1 - Search last-to-first' },
                    { value: '2', text: '2 - Binary search (sorted ascending order)' },
                    { value: '-2', text: '-2 - Binary search (sorted descending order)' },
                ],
            },
        }),
    ];
}

let byName: ReadonlyMap<string, FunctionDefinition> | undefined;

function table(): ReadonlyMap<string, FunctionDefinition> {
    byName ??= new Map(declare().map(f => [ignoreCaseKey(f.name), f]));
    return byName;
}

/** The declared function of a name, read without regard to case; `undefined` for a name outside the set. */
export function findFunction(name: string): FunctionDefinition | undefined {
    return table().get(ignoreCaseKey(name));
}

/** Every declared function, in the ordinal order of its name. */
export function allFunctions(): readonly FunctionDefinition[] {
    return [...table().values()].toSorted((a, b) => compareOrdinal(a.name, b.name));
}
