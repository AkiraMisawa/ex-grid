namespace ExSheet;

/// <summary>
/// Why a press on a grid of a Pointing Scope wrote nothing into the Formula being edited (ADR-0058).
/// A Formula reads a Linked Table's cell by its row's key, and its column by name; a press that
/// stands for anything else is refused, rather than written as something close to it.
/// </summary>
public enum PointingRefusalReason
{
    /// <summary>More than one cell: a Shift+press, a press on a Row Heading or on the corner, or a
    /// drag that reached another cell. A drag takes back what its press wrote.</summary>
    SeveralCells,

    /// <summary>More than one column: a Shift+press on a header, or a drag across headers that
    /// reached another column. A drag takes back what its press wrote.</summary>
    SeveralColumns,

    /// <summary>A Header Group's rectangle, which stands over several columns (ADR-0032).</summary>
    HeaderGroup,

    /// <summary>A column the Linked Table does not have: one the grid shows of its own, such as an
    /// Action Column, or one the Scope was not told is the table's.</summary>
    ColumnNotInTable,

    /// <summary>A cell of a table declared without a key: no Formula can name its row.</summary>
    NoKey,

    /// <summary>A cell whose row has a blank key: no Formula can name that row.</summary>
    BlankKey,

    /// <summary>A cell whose row holds an Error Value as its key, which no lookup finds.</summary>
    KeyIsAnError,

    /// <summary>A cell whose row has not arrived yet: a Placeholder has no key to write.</summary>
    RowNotArrived,

    /// <summary>The Sheet that points has no Linked Table declared by the name the grid was
    /// registered with.</summary>
    TableNotDeclared,

    /// <summary>The press arrived after the Sheet stopped pointing — a key typed, the caret moved or
    /// the edit ended within the round trip before the grid heard of it.</summary>
    NotPointing,
}

/// <summary>A press on a grid of a Pointing Scope that wrote nothing, and why (ADR-0058). The text of
/// the Formula being edited is as it was before the press.</summary>
/// <param name="Reason">Which refusal.</param>
/// <param name="Message">The refusal in words, for the user, naming the table and the column where
/// they are known.</param>
public sealed record PointingRefusal(PointingRefusalReason Reason, string Message);
