namespace ExGrid.Chrome;

/// <summary>
/// A stylesheet a Chrome brings, as the stylesheet's notice names it (ADR-0123). A Wrapper has no
/// root element of its own (ADR-0062), so its Chrome declares the stylesheets it brings, and the
/// root of the product it dresses renders a notice for each, beside its own: hidden until a
/// stylesheet's rules decide, shown by another version's stylesheet, hidden by the one the Wrapper
/// was built with. The notice's markup is a contract between versions: the root writes
/// <paramref name="Name"/> into <c>data-ex-stylesheet</c> and <paramref name="Stamp"/> into
/// <c>data-ex-stamp</c>, and the stylesheet ends with the rule that shows a notice of its name and
/// the rule that hides it for its own stamp.
/// </summary>
/// <param name="Name">The stylesheet's source file name, as the rules at the end of the shipped
/// stylesheet name it: <c>mud-ex-grid.css</c>.</param>
/// <param name="Stamp">The SHA-256 of that source, as lower-case hex: the stamp the stylesheet the
/// Chrome was built with hides its notice for.</param>
public sealed record ChromeStylesheet(string Name, string Stamp);
