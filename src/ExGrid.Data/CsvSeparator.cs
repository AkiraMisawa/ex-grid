namespace ExGrid.Data;

/// <summary>
/// The character that separates the fields of a CSV's record, as its Schema declares it (ADR-0063).
/// Only these three are read: the separators Excel writes, in every locale it writes them.
/// </summary>
public enum CsvSeparator
{
    /// <summary>A comma, <c>,</c>.</summary>
    Comma,

    /// <summary>A tab.</summary>
    Tab,

    /// <summary>A semicolon, <c>;</c>, which Excel writes where the comma is the decimal point.</summary>
    Semicolon,
}
