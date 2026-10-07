using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// The pivot engine (ADR-0060/0066). A Pivot Source answers a question with the Leaf Aggregates;
/// <see cref="Cube"/> builds from them the row and column trees and every total; and
/// <see cref="Report"/> lays a cube out into a <see cref="PivotReport"/>. Pure, with no UI: a
/// server computes the same report the screen shows, and a server-side answer is held to the
/// bundled source's.
///
/// <para><see cref="Aggregate{TRecord}"/> and <see cref="Compute{TRecord}"/> run the whole path over
/// records in memory on the calling thread, through the bundled source
/// (<see cref="PivotSource.From{TRecord}(IReadOnlyList{TRecord}, IReadOnlyList{PivotField{TRecord}}, PivotSlicing?)"/>).</para>
/// </summary>
public static class PivotEngine
{
    /// <summary>Aggregates and lays out in one step — <see cref="Aggregate"/> then
    /// <see cref="Report"/>.</summary>
    public static PivotReport Compute<TRecord>(
        IReadOnlyList<TRecord> records,
        IReadOnlyList<PivotField<TRecord>> fields,
        PivotLayout layout,
        PivotOptions? options = null)
        => Report(Aggregate(records, fields, layout), layout, options);

    /// <summary>
    /// The cube of <paramref name="layout"/> over records in memory, on the calling thread: the
    /// bundled source answers the layout's question (<see cref="PivotQuery.For"/>), with no cap
    /// on leaves, and <see cref="Cube"/> builds the trees and the totals from its leaves. Refuses by
    /// name a layout that names an undeclared field, stands a field in two of Filters, Rows and
    /// Columns, or orders by a Value Field it does not have; and a field list that declares a name
    /// twice.
    /// </summary>
    public static PivotCube Aggregate<TRecord>(
        IReadOnlyList<TRecord> records,
        IReadOnlyList<PivotField<TRecord>> fields,
        PivotLayout layout)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(layout);
        var source = new RecordPivotSource<TRecord>(records, fields, PivotSlicing.Default);
        var meta = FieldMeta.Of(fields);
        Validate(layout, meta);
        var query = PivotQuery.For(layout, int.MaxValue);
        var answer = source.Aggregate(query);
        if (answer.IsRefused)
            throw new InvalidOperationException(answer.Refusal!.Message);
        return PivotCube.Build(query, answer, meta, records, fields, source.AllItems);
    }

    /// <summary>
    /// The cube of a Pivot Source's answer (ADR-0066): the row and column trees its leaves form,
    /// and every subtotal and grand total merged from the leaves' parts — each total from its
    /// records, never from the totals below it (ADR-0060). Refuses by name an answer to another
    /// question than <paramref name="query"/>, a refusal, and a field neither declared in
    /// <paramref name="fields"/> nor named by the question.
    /// </summary>
    /// <param name="query">The question the answer was given to.</param>
    /// <param name="answer">The source's answer.</param>
    /// <param name="fields">The source's fields: how Items are labelled and ordered.</param>
    public static PivotCube Cube(PivotQuery query, PivotAnswer answer, IReadOnlyList<PivotField> fields)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(fields);
        return PivotCube.Build(query, answer, FieldMeta.Of(fields), fields: fields);
    }

    /// <summary>
    /// <see cref="Cube"/> in slices (ADR-0066, PV-40): the same cube, made a piece at a time, the
    /// thread yielded whenever a slice of <see cref="PivotSlicing.Budget"/> is spent — so a browser
    /// keeps painting while the cube of a large answer is made. Cancelled, it throws at the next
    /// yield. A small answer is made without reading the clock, and the task is complete when it
    /// returns.
    /// </summary>
    /// <param name="query">The question the answer was given to.</param>
    /// <param name="answer">The source's answer.</param>
    /// <param name="fields">The source's fields: how Items are labelled and ordered.</param>
    /// <param name="slicing">How the work shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <param name="cancellationToken">Stops the work at the next yield.</param>
    public static ValueTask<PivotCube> CubeAsync(
        PivotQuery query,
        PivotAnswer answer,
        IReadOnlyList<PivotField> fields,
        PivotSlicing? slicing = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(fields);
        var meta = FieldMeta.Of(fields);
        return PivotCube.BuildAsync(query, answer, meta, Slicer.Of(slicing ?? PivotSlicing.Default, cancellationToken), fields: fields);
    }

    /// <summary>
    /// The cube of a live redraw's answer, made from the cube on screen (ADR-0161): under the same
    /// question, when the answer's leaves are those of <paramref name="previous"/>'s answer and only
    /// their values changed, it shares <paramref name="previous"/>'s axis trees and cells and computes
    /// again only the cells on the changed leaves' paths — each changed leaf's cell, every subtotal
    /// above it on both axes, and the grand totals — each from its leaves, as a cube built afresh
    /// computes it. Which leaves changed is the answer's to say
    /// (<see cref="PivotAnswer.ChangedLeaves"/>, since <paramref name="previous"/>'s Source Version);
    /// when it does not, the leaves are compared with <paramref name="previous"/>'s. Otherwise — another
    /// question, other fields, leaves that came or went, Items spelled anew, a batch the source could
    /// not fold, or so many changed leaves that building afresh is cheaper — it is built afresh
    /// (<see cref="Cube"/>). Either way it equals the cube built afresh, and
    /// <paramref name="previous"/> is left exactly as it was.
    /// </summary>
    /// <param name="previous">The cube the report on screen was laid out from.</param>
    /// <param name="query">The question the answer was given to.</param>
    /// <param name="answer">The source's answer.</param>
    /// <param name="fields">The source's fields: how Items are labelled and ordered.</param>
    public static PivotCube NextCube(PivotCube previous, PivotQuery query, PivotAnswer answer, IReadOnlyList<PivotField> fields)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(fields);
        return Slicer.Run(PivotCube.NextAsync(previous, query, answer, fields, Slicer.Unsliced));
    }

    /// <summary><see cref="NextCube"/> in slices (PV-40): yielding whenever a slice of
    /// <see cref="PivotSlicing.Budget"/> is spent, and throwing at the next yield once cancelled.</summary>
    /// <param name="previous">The cube the report on screen was laid out from.</param>
    /// <param name="query">The question the answer was given to.</param>
    /// <param name="answer">The source's answer.</param>
    /// <param name="fields">The source's fields: how Items are labelled and ordered.</param>
    /// <param name="slicing">How the work shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <param name="cancellationToken">Stops the work at the next yield.</param>
    public static ValueTask<PivotCube> NextCubeAsync(
        PivotCube previous,
        PivotQuery query,
        PivotAnswer answer,
        IReadOnlyList<PivotField> fields,
        PivotSlicing? slicing = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(fields);
        return PivotCube.NextAsync(previous, query, answer, fields, Slicer.Of(slicing ?? PivotSlicing.Default, cancellationToken));
    }

    /// <summary>
    /// The report of a live redraw, made from the report on screen (ADR-0161): when
    /// <paramref name="cube"/> was made from <paramref name="previous"/>'s cube by
    /// <see cref="NextCube"/>, under the same layout and words, it shares <paramref name="previous"/>'s
    /// columns and every row whose painted text did not change, and makes anew each row whose painted
    /// text did. Otherwise it is laid out afresh (<see cref="Report"/>): a change that is not data, a
    /// cube built afresh, an order that follows values (a sort by a Value Field), and a Show Values As,
    /// whose shown values on other rows a change can move. Either way it equals the report laid out
    /// afresh, and <paramref name="previous"/> is left exactly as it was.
    /// </summary>
    /// <param name="previous">The report on screen.</param>
    /// <param name="cube">The cube to lay out, which must hold the layout.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="options">The culture and the words; <see cref="PivotOptions.Default"/> when left out.</param>
    public static PivotReport NextReport(PivotReport previous, PivotCube cube, PivotLayout layout, PivotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(previous);
        return Slicer.Run(NextReportAsync(previous, cube, layout, options, Slicer.Unsliced));
    }

    /// <summary><see cref="NextReport"/> in slices (PV-40): yielding whenever a slice of
    /// <see cref="PivotSlicing.Budget"/> is spent, and throwing at the next yield once cancelled.</summary>
    /// <param name="previous">The report on screen.</param>
    /// <param name="cube">The cube to lay out, which must hold the layout.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="options">The culture and the words; <see cref="PivotOptions.Default"/> when left out.</param>
    /// <param name="slicing">How the work shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <param name="cancellationToken">Stops the work at the next yield.</param>
    public static ValueTask<PivotReport> NextReportAsync(
        PivotReport previous,
        PivotCube cube,
        PivotLayout layout,
        PivotOptions? options = null,
        PivotSlicing? slicing = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previous);
        return NextReportAsync(previous, cube, layout, options, Slicer.Of(slicing ?? PivotSlicing.Default, cancellationToken));
    }

    private static async ValueTask<PivotReport> NextReportAsync(PivotReport previous, PivotCube cube, PivotLayout layout, PivotOptions? options, Slicer slicer)
    {
        var builder = Builder(cube, layout, options);
        return await PivotReport.NextAsync(previous, cube, layout, options ?? PivotOptions.Default, slicer).ConfigureAwait(false)
            ?? await builder.BuildAsync(slicer).ConfigureAwait(false);
    }

    /// <summary>
    /// Lays a cube out under <paramref name="layout"/> (ADR-0060): the rows, the label columns,
    /// the value columns and their Header Group spans. Cheap — nothing is asked of the source —
    /// and what a collapse, a sort, a form or a Value Field's Aggregation changing costs. Refuses a
    /// layout the cube does not hold (<see cref="PivotCube.Holds"/>).
    /// </summary>
    public static PivotReport Report(PivotCube cube, PivotLayout layout, PivotOptions? options = null)
        => Builder(cube, layout, options).Build();

    private static ReportBuilder Builder(PivotCube cube, PivotLayout layout, PivotOptions? options)
    {
        ArgumentNullException.ThrowIfNull(cube);
        ArgumentNullException.ThrowIfNull(layout);
        Validate(layout, cube.Meta);
        if (!cube.Holds(layout))
        {
            throw new InvalidOperationException(
                "The cube was aggregated under other placed fields, Hidden Items or Value Fields than this layout " +
                "has, or without the parts one of its Aggregations reads; ask again (PivotEngine.Aggregate, or the " +
                "source with PivotQuery.For) before laying it out (ADR-0060/0066).");
        }
        return new ReportBuilder(cube, layout, options ?? PivotOptions.Default);
    }

    /// <summary>
    /// <see cref="Report"/> in slices (ADR-0066, PV-40): the same report, laid out a piece at a
    /// time — the value columns and their spans, then the rows, each axis walked in its Items'
    /// order — the thread yielded whenever a slice of <see cref="PivotSlicing.Budget"/> is spent, so
    /// a layout that grows long never holds a browser. Cancelled, it throws at the next yield. A
    /// small report is laid out without reading the clock, and the task is complete when it
    /// returns. Refuses, at once, what <see cref="Report"/> refuses.
    /// </summary>
    /// <param name="cube">The cube to lay out.</param>
    /// <param name="layout">The layout, which the cube must hold.</param>
    /// <param name="options">The culture and the words; <see cref="PivotOptions.Default"/> when left out.</param>
    /// <param name="slicing">How the work shares the thread; <see cref="PivotSlicing.Default"/> when left out.</param>
    /// <param name="cancellationToken">Stops the work at the next yield.</param>
    public static ValueTask<PivotReport> ReportAsync(
        PivotCube cube,
        PivotLayout layout,
        PivotOptions? options = null,
        PivotSlicing? slicing = null,
        CancellationToken cancellationToken = default)
    {
        var builder = Builder(cube, layout, options);
        return builder.BuildAsync(Slicer.Of(slicing ?? PivotSlicing.Default, cancellationToken));
    }

    /// <summary>Whether <paramref name="cube"/> was aggregated from exactly these records and
    /// field declarations and holds <paramref name="layout"/>, so that a new report needs no pass
    /// over the records. The records and the fields are compared by instance: a new list is a
    /// refresh (ADR-0059).</summary>
    public static bool CanReuse<TRecord>(
        PivotCube? cube, IReadOnlyList<TRecord> records, IReadOnlyList<PivotField<TRecord>> fields, PivotLayout layout)
        => cube is not null && ReferenceEquals(cube.RecordsIdentity, records)
            && ReferenceEquals(cube.FieldsIdentity, fields) && cube.Holds(layout);

    /// <summary>
    /// Every Item of a placed field over all the records, in the field's order — what Filter…
    /// lists (ADR-0061) — each with its label and whether it is hidden now. For a cube built by
    /// <see cref="Aggregate{TRecord}"/>; a cube built from a source's answer asks the source
    /// (<see cref="PivotSource.ItemsAsync"/>) and lays the page out with
    /// <see cref="ItemsOf(PivotItemPage, PivotLayout, PivotField, PivotOptions?)"/>.
    /// </summary>
    public static IReadOnlyList<PivotItemInfo> ItemsOf(
        PivotCube cube, PivotLayout layout, string field, PivotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cube);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(field);
        // The layout's placement, not the question's: a field in Filters that hides nothing is
        // placed, and is not in the question (ADR-0066, refined).
        if (layout.PlacementOf(field) is null && !cube.Query.Places(field))
            throw new ArgumentException($"'{field}' stands in none of Filters, Rows and Columns of the layout.", nameof(field));
        if (cube.AllItems is not { } allItems)
        {
            throw new InvalidOperationException(
                "This cube was built from a Pivot Source's answer, which carries only the Items its leaves have; ask the " +
                "source for the field's Items (PivotSource.ItemsAsync) under the cube's Source Version (ADR-0066).");
        }
        return ItemInfos(allItems(field).Select(key => ItemRef.Of(key)), cube.Meta[field], layout, field, options);
    }

    /// <summary>
    /// A page of a field's Items (<see cref="PivotSource.ItemsAsync"/>) as Filter… and the report
    /// filter band list them (ADR-0061): in the field's order under <paramref name="layout"/> —
    /// its declared Items first, then by kind and label under the report's culture, descending when
    /// the field is — each with its label and whether the layout hides it now.
    /// </summary>
    public static IReadOnlyList<PivotItemInfo> ItemsOf(
        PivotItemPage page, PivotLayout layout, PivotField field, PivotOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(field);
        return ItemInfos(page.Items.Select(ItemRef.Of), new FieldMeta(field), layout, field.Name, options);
    }

    private static IReadOnlyList<PivotItemInfo> ItemInfos(
        IEnumerable<ItemRef> items, FieldMeta meta, PivotLayout layout, string field, PivotOptions? options)
    {
        var placement = layout.PlacementOf(field) is { } at ? layout.PlacementsIn(at.Area)[at.Index] : new PivotFieldPlacement(field);
        var resolved = options ?? PivotOptions.Default;
        var labels = new ItemLabels(resolved);
        var hidden = placement.HiddenItems.Select(ItemKey.FromPublic).ToHashSet();
        var order = new ItemOrder(meta, labels, resolved.Culture,
            placement.Sort.Direction == PivotSortDirection.Descending && placement.Sort.ByValue is null);
        var listed = items.ToArray();
        order.Prepare(listed);
        return listed
            .OrderBy(item => item, order)
            .Select(item => new PivotItemInfo(item.PublicKey, labels.Of(item, meta), hidden.Contains(item.Key)))
            .ToArray();
    }

    /// <summary>
    /// The Source Records behind one cell of a report — Show Details (ADR-0063) — in their order
    /// in the snapshot: those no Hidden Item leaves out, carrying the row's Items and the
    /// column's. A label cell is <paramref name="valueColumn"/> −1, and is every record of its
    /// row. A row that stands for no records — a Value Field's row — is its Item's. The question is
    /// <see cref="PivotReport.DetailsQuery"/>'s, answered by the bundled source over
    /// <paramref name="records"/>.
    /// </summary>
    public static IReadOnlyList<TRecord> RecordsBehind<TRecord>(
        IReadOnlyList<TRecord> records,
        IReadOnlyList<PivotField<TRecord>> fields,
        PivotReport report,
        PivotReportRow row,
        int valueColumn)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(row);
        var query = report.DetailsQuery(row, valueColumn);
        return new RecordPivotSource<TRecord>(records, fields, PivotSlicing.Default).RecordsBehind(query);
    }

    private static void Validate(PivotLayout layout, IReadOnlyDictionary<string, FieldMeta> declared)
    {
        var standing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (area, placements) in new[]
                 {
                     (PivotArea.Filters, layout.Filters), (PivotArea.Columns, layout.Columns), (PivotArea.Rows, layout.Rows),
                 })
        {
            foreach (var placement in placements)
            {
                ArgumentNullException.ThrowIfNull(placement, nameof(layout));
                if (!declared.ContainsKey(placement.Field))
                    throw new InvalidOperationException($"The layout places '{placement.Field}' in {area}, and no Pivot Field of that name is declared (ADR-0060).");
                if (!standing.Add(placement.Field))
                    throw new InvalidOperationException($"The layout places '{placement.Field}' twice among Filters, Rows and Columns; a field stands in one of them at most (ADR-0061).");
                if (placement.Sort.ByValue is { } byValue && (byValue < 0 || byValue >= layout.Values.Count))
                    throw new InvalidOperationException($"'{placement.Field}' is ordered by Value Field {byValue}, and the layout has {layout.Values.Count} (ADR-0060).");
                if (!Enum.IsDefined(placement.Sort.Direction))
                    throw new InvalidOperationException($"'{placement.Field}' has an unknown sort direction ({placement.Sort.Direction}).");
            }
        }
        foreach (var value in layout.Values)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(layout));
            if (!declared.ContainsKey(value.Field))
                throw new InvalidOperationException($"The layout places '{value.Field}' in Values, and no Pivot Field of that name is declared (ADR-0060).");
            if (!Enum.IsDefined(value.Aggregation))
                throw new InvalidOperationException($"The Value Field of '{value.Field}' has an unknown Aggregation ({value.Aggregation}).");
            if (!Enum.IsDefined(value.ShowValuesAs))
                throw new InvalidOperationException($"The Value Field of '{value.Field}' has an unknown Show Values As ({value.ShowValuesAs}).");
            if (value.NumberFormat is { } format && PivotNumberFormat.Check(format) is { } problem)
                throw new InvalidOperationException($"The Value Field of '{value.Field}' has the number format '{format}', which {problem} (ADR-0060).");
        }
        if (!Enum.IsDefined(layout.ValuesAxis))
            throw new InvalidOperationException($"Unknown ValuesAxis ({layout.ValuesAxis}).");
        if (!Enum.IsDefined(layout.Form))
            throw new InvalidOperationException($"Unknown report form ({layout.Form}).");
    }
}

/// <summary>What a report is laid out in: the culture its labels and values are written in and
/// its text is ordered by, and the Consumer's words (ADR-0060).</summary>
public sealed record PivotOptions
{
    /// <summary>The invariant culture's report, in English.</summary>
    public static PivotOptions Default { get; } = new();

    /// <summary>The culture labels and values are formatted in, and text Items ordered by.</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>The Consumer's word for an id of <see cref="PivotWords"/>, or null to keep the
    /// English.</summary>
    public Func<string, string?>? Label { get; init; }

    internal string Word(string id) => PivotWords.Resolve(id, Label);
}

/// <summary>One Item as Filter… lists it (ADR-0061).</summary>
/// <param name="Key">The Item's key, as a layout writes it.</param>
/// <param name="Label">What it is painted as.</param>
/// <param name="IsHidden">Whether the layout hides it now.</param>
public sealed record PivotItemInfo(PivotItemKey Key, string Label, bool IsHidden);
