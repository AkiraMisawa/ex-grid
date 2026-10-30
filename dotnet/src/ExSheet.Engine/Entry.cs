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

    /// <summary>
    /// The functions the Formula calls that are outside ExSheet's declared set
    /// (<see cref="DeclaredFunction"/>), in the text's order, each once without regard to case: each
    /// gives <c>#NAME?</c> (ADR-0047). Empty for a constant, and for a Formula that calls only
    /// declared functions. A reader of another program's workbook lists them before the user meets
    /// them (ADR-0330 §4).
    /// </summary>
    public IReadOnlyList<string> UndeclaredFunctions =>
        Parsed is null
            ? []
            : Parsed.Calls.Where(c => c.Function is null).Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>
    /// The names the Formula reads that nothing in it binds — not a function's, not a <c>LET</c>'s,
    /// not a <c>LAMBDA</c>'s parameter, not <c>TRUE</c> or <c>FALSE</c> — in the text's order, each once without regard to case. ExSheet
    /// defines no names, so each gives <c>#NAME?</c>; in Excel each is a defined name, or an Excel
    /// Table's, where the workbook has one (ADR-0047, ADR-0330 §4). Empty for a constant.
    /// </summary>
    public IReadOnlyList<string> UnboundNames => Parsed is null ? [] : UnboundNameWalk.Of(Parsed);

    /// <summary>
    /// The Entry as a copy writes it <paramref name="rows"/> down and <paramref name="columns"/>
    /// across from where it stands, as Excel writes a copied Formula and a shared formula's cells
    /// (ADR-0048, ADR-0330 §4): each relative part of a Reference moves by that distance, each
    /// absolute part (<c>$</c>) stays, a whole column or row spans every row or column still, and a
    /// Reference moved off the Sheet is written <c>#REF!</c>. A constant, or a move of nothing, is
    /// this Entry. Negative distances move up and left.
    /// </summary>
    public Entry MovedBy(int rows, int columns) => ReferenceShift.Shift(this, rows, columns);

    /// <summary>
    /// The constant Entry of an Error Value written exactly as Excel writes one in a file, case and all
    /// (<c>#N/A</c>, <c>#DIV/0!</c>, <c>#SPILL!</c>), or <see langword="null"/> when the text is no Error
    /// Value a cell can hold: <c>#GETTING_DATA</c> and <c>#CIRC!</c> are not one, and nothing else is read
    /// as a Formula or a number. A reader of another program's workbook takes an error cell through it
    /// (ADR-0330 §4).
    /// </summary>
    public static Entry? FromErrorText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ErrorValues.TryParseWritten(text, out var error) ? new Entry(Value.FromError(error)) : null;
    }

    /// <summary>
    /// A constant Entry. <c>#GETTING_DATA</c> and <c>#CIRC!</c> are states of a computation and cannot be one,
    /// and neither can the <c>#VALUE!</c> of a cell whose Formula ExSheet refuses, read from it: held as a
    /// constant it would be an Error Value a Formula can catch (ADR-0047; 2026-10-12, decided with the user:
    /// a refusal is never caught, anywhere).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="constant"/> is one of those.</exception>
    public static Entry FromValue(Value constant) =>
        constant.IsRefused
            ? throw new ArgumentException($"This {constant.Error.ToText()} is a Formula ExSheet refused, because it cannot answer it as Excel; held as a constant it would be an Error Value a Formula can catch.", nameof(constant))
            : constant.IsError && constant.Error is ErrorValue.GettingData or ErrorValue.Circ
            ? throw new ArgumentException($"{constant.Error.ToText()} is not a constant a cell can hold.", nameof(constant))
            : new(constant);

    /// <summary>A Formula Entry from its invariant text, which begins with <c>=</c>. Internal
    /// (ADR-0201): <see cref="Parse"/> reads the same text as the same Formula.</summary>
    /// <exception cref="FormulaSyntaxException">The Formula cannot be read.</exception>
    internal static Entry FromFormula(string formula)
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
    /// <exception cref="FormulaSyntaxException">
    /// The text is a Formula that cannot be read, or text with a leading <c>+</c> or <c>-</c> that
    /// Excel reads as a Formula using syntax the engine does not implement (<c>-B2 C2</c>) or nested
    /// past the limits the engine reads.
    /// </exception>
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
    /// <exception cref="FormulaSyntaxException">
    /// Excel reads the text as a Formula whose syntax the engine does not implement: <c>-B2 C2</c>,
    /// the intersection of two References, is refused by name, not kept as text, since Excel makes
    /// it the Formula <c>=-B2 C2</c> (TYPED-054; ADR-0047, third run). So is a Formula refused for its
    /// size alone, nested past the limits (<see cref="FormulaSyntaxException.IsBeyondLimit"/>): it is a
    /// Formula still, and kept as text it would quietly be another Entry than the one typed.
    /// </exception>
    internal static Entry? SignedFormula(string typed)
    {
        if (typed.Length < 2 || typed[0] is not ('+' or '-')) return null;
        try
        {
            return FromFormula("=" + typed);
        }
        catch (FormulaSyntaxException e) when (!e.IsUnimplemented && !e.IsBeyondLimit)
        {
            return null;
        }
    }

    /// <summary>Whether typed text would not be kept as text because of its leading sign: <see cref="SignedFormula"/> reads it as a Formula, or refuses it.</summary>
    internal static bool IsSignedFormula(string typed)
    {
        try
        {
            return SignedFormula(typed) is not null;
        }
        catch (FormulaSyntaxException)
        {
            return true;
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
