namespace ExGrid.Selection;

/// <summary>An arrow-key direction (ADR-0012).</summary>
public enum GridDirection
{
    /// <summary>Toward the first row.</summary>
    Up,

    /// <summary>Toward the last row.</summary>
    Down,

    /// <summary>Toward the first visible column.</summary>
    Left,

    /// <summary>Toward the last visible column.</summary>
    Right,
}
