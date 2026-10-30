namespace ExGrid.Cells;

/// <summary>
/// How the grid asks for Cell Metadata: by (row, column), never by reading something
/// stored on the cell (ADR-0006). An as-of stamp is a function of (book × metric), so
/// two metrics in the same row can answer differently and the Consumer is the only one
/// who can say.
///
/// <para><b>Keep it light.</b> This is called once per painted cell on every render —
/// roughly one dictionary probe, with no string building and no allocation. The measured
/// cost of a per-cell metadata lookup is +14–18% against plain markup and is included in
/// the 1.90 ms (800 cells) budget row memoisation buys (ADR-0003 / ADR-0006).</para>
///
/// <para><b>The delegate's identity is the change signal.</b> Metadata changes without
/// any row instance changing, so the grid cannot see it by comparing rows. Hold the
/// lookup in a field and hand over a <em>new</em> one when the metadata behind it
/// changes — the same discipline as handing over a new row instance rather than
/// rewriting one in place (ADR-0003). Rewriting the dictionary behind a lookup that
/// stays reference-identical leaves the old states on screen, deliberately: the
/// alternative is re-reading every visible cell on every render.</para>
/// </summary>
public delegate CellState CellStateOf<TRow>(TRow row, GridColumn<TRow> column);
