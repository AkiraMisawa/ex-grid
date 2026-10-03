namespace ExSheet.Engine.Formulas;

/// <summary>
/// A rectangle of Values a Formula computes (ADR-0125): an operator applied to a range, a function
/// built for arrays, or a range read whole. Row by row; a blank is <see langword="null"/>.
/// </summary>
internal sealed class ValueArray
{
    private readonly Value?[] _values;

    public ValueArray(int rows, int columns)
    {
        if (rows < 1 || columns < 1) throw new ArgumentOutOfRangeException(nameof(rows), "An array has at least one row and one column.");
        Rows = rows;
        Columns = columns;
        _values = new Value?[checked(rows * columns)];
    }

    public int Rows { get; }

    public int Columns { get; }

    /// <summary>Whether the array is one Value, which is no array at all to whoever reads it.</summary>
    public bool IsSingle => Rows == 1 && Columns == 1;

    public Value? this[int row, int column]
    {
        get => _values[(row * Columns) + column];
        set => _values[(row * Columns) + column] = value;
    }

    /// <summary>Every Value row by row, blanks included as <see langword="null"/>.</summary>
    public IEnumerable<Value?> All() => _values;

    /// <summary>The Values row by row, blanks left out, as a range's are read.</summary>
    public IEnumerable<Value> NonBlank() => _values.Where(v => v is not null).Select(v => v!.Value);

    /// <summary>
    /// The element at <paramref name="row"/>, <paramref name="column"/> as Excel's broadcasting reads
    /// it: a single row or column is repeated along the other array, and past its size an array of
    /// more than one row or column is <c>#N/A</c>.
    /// </summary>
    public Value? Broadcast(int row, int column)
    {
        var r = Rows == 1 ? 0 : row;
        var c = Columns == 1 ? 0 : column;
        return r < Rows && c < Columns ? this[r, c] : Value.FromError(ErrorValue.NA);
    }

    public static ValueArray Single(Value? value)
    {
        var array = new ValueArray(1, 1);
        array[0, 0] = value;
        return array;
    }
}
