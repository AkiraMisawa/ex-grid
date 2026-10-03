using System.Text;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// LEFT, RIGHT, MID, LEN, TRIM and CONCAT, each to Microsoft's documentation of it, and TEXT as
/// ADR-0120 reads it. A number or a
/// boolean is read as the text <c>&amp;</c> makes of it. Characters are counted as Excel counts
/// them, in UTF-16 code units, so a character outside the Basic Multilingual Plane counts as two.
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>The most characters a cell holds; CONCAT is <c>#VALUE!</c> past it.</summary>
    private const int TextLimit = 32_767;

    private static Operand Left(FunctionCall call) => Edge(call, fromStart: true);

    private static Operand Right(FunctionCall call) => Edge(call, fromStart: false);

    /// <summary>
    /// LEFT and RIGHT: <c>num_chars</c> is 1 when left out, 0 when given empty, truncated to a whole
    /// number, and <c>#VALUE!</c> below 0; more than the text holds gives the whole text.
    /// </summary>
    private static Operand Edge(FunctionCall call, bool fromStart)
    {
        if (!TryText(call, 0, out var text, out var failure)) return failure;
        var count = 1.0;
        if (call.Count > 1)
        {
            count = 0;
            if (call.Has(1) && !TryNumber(call, 1, out count, out failure)) return failure;
        }
        count = Math.Truncate(count);
        if (count < 0) return Operand.Of(ErrorValue.Value);
        var length = (int)Math.Min(count, text.Length);
        return Operand.Of(Value.FromText(fromStart ? text[..length] : text[^length..]));
    }

    /// <summary>
    /// MID: <c>start_num</c> below 1 or <c>num_chars</c> below 0 is <c>#VALUE!</c>; a start past
    /// the end gives empty text, and the characters run at most to the end.
    /// </summary>
    private static Operand Mid(FunctionCall call)
    {
        if (!TryText(call, 0, out var text, out var failure)) return failure;
        if (!TryNumber(call, 1, out var start, out failure)) return failure;
        if (!TryNumber(call, 2, out var count, out failure)) return failure;
        start = Math.Truncate(start);
        count = Math.Truncate(count);
        if (start < 1 || count < 0) return Operand.Of(ErrorValue.Value);
        if (start > text.Length) return Operand.Of(Value.FromText(""));
        var from = (int)start - 1;
        var length = (int)Math.Min(count, text.Length - from);
        return Operand.Of(Value.FromText(text.Substring(from, length)));
    }

    private static Operand Len(FunctionCall call) =>
        TryText(call, 0, out var text, out var failure) ? Operand.Of(Value.FromNumber(text.Length)) : failure;

    /// <summary>
    /// TRIM removes the space character, U+0020, from both ends and leaves one between words. Other
    /// white space, the non-breaking space among it, is kept, as Excel keeps it.
    /// </summary>
    private static Operand Trim(FunctionCall call) =>
        TryText(call, 0, out var text, out var failure)
            ? Operand.Of(Value.FromText(string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries))))
            : failure;

    /// <summary>
    /// CONCAT joins its arguments in order, and a range's cells row by row; blanks add nothing. An
    /// Error Value anywhere is the result, the first one found. A result longer than a cell holds is
    /// <c>#VALUE!</c>.
    /// </summary>
    private static Operand Concat(FunctionCall call)
    {
        var evaluator = call.Evaluator;
        var joined = new StringBuilder();
        for (var i = 0; i < call.Count; i++)
        {
            var operand = call.Operand(i);
            switch (operand.Kind)
            {
                case OperandKind.Area or OperandKind.Column:
                    foreach (var value in evaluator.RangeValues(operand))
                    {
                        if (value.IsError) return Operand.Of(value);
                        joined.Append(evaluator.ToText(value));
                    }
                    break;
                case OperandKind.Missing:
                    break;
                default:
                    if (operand.Scalar is { IsError: true }) return operand;
                    joined.Append(evaluator.ToText(operand.Scalar));
                    break;
            }
            if (joined.Length > TextLimit) return Operand.Of(ErrorValue.Value);
        }
        return Operand.Of(Value.FromText(joined.ToString()));
    }

    /// <summary>
    /// The longest General text <c>TEXT</c> answers. Excel is reported to fit TEXT's General to a
    /// width, as it fits a cell's; at 11 characters or fewer every reading of that width agrees, and
    /// past it the answer waits for a Windows run (ADR-0120).
    /// </summary>
    private const int TextGeneralLimit = 11;

    /// <summary>
    /// TEXT (ADR-0120): the Value as a cell in the format shows it under the Sheet's culture, the
    /// code read in the invariant spelling and as written. A blank is 0, and text that reads as a
    /// number is that number. A code the cell formats refuse, an empty code, and a number the
    /// format cannot show are <c>#VALUE!</c>; the code's colour is dropped.
    /// </summary>
    private static Operand FormatAsText(FunctionCall call)
    {
        if (!TryScalar(call, 0, out var value, out var failure)) return failure;
        if (!TryText(call, 1, out var code, out failure)) return failure;
        if (code.Length == 0 || !NumberFormat.TryParseAsWritten(code, out var format)) return Operand.Of(ErrorValue.Value);

        var culture = call.Evaluator.Culture;
        var shown = value switch
        {
            null => Value.FromNumber(0),
            { Kind: ValueKind.Text } text when ConstantParser.TryParseNumber(text.Text, culture, out var number) => Value.FromNumber(number),
            { } other => other,
        };
        if (shown.Kind == ValueKind.Number && format.ShowsNumbersAsGeneral)
        {
            var general = NumberText.General(shown.Number, culture);
            return general.Length > TextGeneralLimit ? Operand.Of(ErrorValue.Value) : Operand.Of(Value.FromText(general));
        }
        var (formatted, cannotShow, _) = format.Format(shown, culture);
        return cannotShow ? Operand.Of(ErrorValue.Value) : Operand.Of(Value.FromText(formatted));
    }
}
