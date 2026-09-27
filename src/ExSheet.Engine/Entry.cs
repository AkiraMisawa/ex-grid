using System.Globalization;
using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// What a user put into a cell: a constant or a Formula (CONTEXT.md). An Entry is what a Sheet
/// Document records, and what the user sees again when editing the cell. A constant is held
/// already parsed, never as the text that was typed (ADR-0048); a Formula is held in Excel's
/// invariant syntax (ADR-0047). Immutable.
/// </summary>
public sealed class Entry : IEquatable<Entry>
{
    private Entry(Value constant)
    {
        Constant = constant;
    }

    private Entry(string formula, Node parsed)
    {
        Formula = formula;
        Parsed = parsed;
    }

    /// <summary>The constant, or <see langword="null"/> when this Entry is a Formula.</summary>
    public Value? Constant { get; }

    /// <summary>
    /// The Formula in invariant syntax, beginning with <c>=</c>: the whitespace as it was typed
    /// where Excel keeps it (not at the end, nor before a <c>,</c>), and each token as the engine writes it (function names and References in upper case,
    /// <c>,</c> between arguments, <c>.</c> as the decimal separator) (ADR-0047);
    /// <see langword="null"/> when this Entry is a constant.
    /// </summary>
    public string? Formula { get; }

    /// <summary>Whether this Entry is a Formula.</summary>
    public bool IsFormula => Formula is not null;

    internal Node? Parsed { get; }

    /// <summary>A constant Entry. <c>#GETTING_DATA</c> and <c>#CIRC!</c> are states of a computation and cannot be one.</summary>
    public static Entry FromValue(Value constant) =>
        constant.IsError && constant.Error is ErrorValue.GettingData or ErrorValue.Circ
            ? throw new ArgumentException($"{constant.Error.ToText()} is not a constant a cell can hold.", nameof(constant))
            : new(constant);

    /// <summary>A Formula Entry from its invariant text, which begins with <c>=</c>.</summary>
    /// <exception cref="FormulaSyntaxException">The Formula cannot be read.</exception>
    public static Entry FromFormula(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var parsed = Parser.Parse(formula, out var tokens);
        return new Entry(FormulaText.Normalize(formula, tokens), parsed);
    }

    /// <summary>
    /// Reads text as a user typed it into a cell: text beginning with <c>=</c> is a Formula in
    /// invariant syntax; anything else is a constant read under <paramref name="culture"/> and
    /// recorded parsed (ADR-0048). Empty text is no Entry, and gives <see langword="null"/>.
    /// </summary>
    /// <exception cref="FormulaSyntaxException">The text is a Formula that cannot be read.</exception>
    public static Entry? Parse(string typed, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(culture);
        if (typed.Length == 0) return null;
        if (typed[0] == '=') return FromFormula(typed);
        var constant = ConstantParser.Parse(typed, culture);
        if (constant.Kind == ValueKind.Text && SignedFormula(typed) is { } formula) return formula;
        return new Entry(constant);
    }

    /// <summary>
    /// Text typed with a leading <c>+</c> or <c>-</c> that is not a constant is the Formula it
    /// spells after an <c>=</c>, as Excel's cell editor reads it: <c>+A1</c> is <c>=+A1</c> and
    /// <c>-A1</c> is <c>=-A1</c> (observed with real keys; Excel's <c>Range.FormulaLocal</c> keeps
    /// them as text, which is not what a user typing gets). Text that does not read as a Formula
    /// that way stays text. <see langword="null"/> when the text is not such a Formula.
    /// </summary>
    internal static Entry? SignedFormula(string typed)
    {
        if (typed.Length < 2 || typed[0] is not ('+' or '-')) return null;
        try
        {
            return FromFormula("=" + typed);
        }
        catch (FormulaSyntaxException)
        {
            return null;
        }
    }

    /// <summary>Two Entries are equal when they hold the same constant (exactly) or the same Formula text.</summary>
    public bool Equals(Entry? other) =>
        other is not null && Nullable.Equals(Constant, other.Constant) && string.Equals(Formula, other.Formula, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Entry);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Constant, Formula);

    /// <summary>The Formula text, or the constant's invariant diagnostic form.</summary>
    public override string ToString() => Formula ?? Constant!.Value.ToString();
}
