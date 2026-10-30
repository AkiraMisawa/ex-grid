using System.Text;

namespace ExGrid.Clipboard;

/// <summary>Where one pasted field came from, which says how it is read (ADR-0050, item 10).</summary>
public enum PasteFieldOrigin
{
    /// <summary>Text as it was shown where it was copied: the plain-text flavour, or an HTML
    /// cell's text content. Its numbers are written in the source's culture — <c>1.234,5</c>
    /// under <c>de-DE</c> — so a Consumer reads it as typed under its own culture.</summary>
    ShownText = 0,

    /// <summary>An invariant number: Excel's <c>x:num</c> attribute, the raw value at full
    /// precision in invariant notation, or a field of ExGrid's own unformatted HTML, which is
    /// locale-free throughout (ADR-0005). A Consumer reads it under the invariant culture, never
    /// its own — otherwise <c>1234.5</c> from Excel would be misread under <c>de-DE</c>.</summary>
    Invariant,
}