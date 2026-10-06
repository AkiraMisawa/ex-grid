using System.Globalization;
using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Components;

// The Field List half: the pane, a placed field's menu and the three panels, and the Items they
// list (ADR-0061/0066). ExPivot holds every piece of state and applies every rule; the Chrome draws
// what it is handed, or the built-in views do.
public partial class ExPivot
{
    /// <summary>Filter… lists at most this many Items at once; the search narrows the rest.
    /// Excel's own filter lists stop at the same number.</summary>
    public const int ItemListCap = 10_000;

    private static readonly PivotArea[] AreaOrder = [PivotArea.Filters, PivotArea.Columns, PivotArea.Rows, PivotArea.Values];

    private readonly Dictionary<PivotEntry, int> _entryFocus = [];
    private readonly Dictionary<string, int> _bandFocus = new(StringComparer.Ordinal);
    private readonly EventCallback _escape;
    private OpenSurface? _open;
    private sealed record FieldDrag(PivotDragSubject Subject, PivotLayout Layout, PivotReportSource Source);

    private FieldDrag? _drag;
    private (PivotArea Area, int Index)? _dropAt;
    private string _search = "";
    private int _focusSequence;

    private enum Surface
    {
        Menu,
        ItemFilter,
        FieldSettings,
        ValueFieldSettings,
        LayoutMenu,
    }

    /// <summary>The one menu or panel open in this ExPivot, with its draft (ADR-0061).</summary>
    private sealed class OpenSurface
    {
        public required Surface Kind { get; init; }

        /// <summary>The Field List entry that opened it, or null.</summary>
        public PivotEntry? Entry { get; init; }

        /// <summary>The report filter that opened it from the Pivot Toolbar's band,
        /// or null.</summary>
        public string? BandField { get; init; }

        /// <summary>Whether it opened from the Pivot Toolbar, over the report: the band's Filter…
        /// or the Layout menu.</summary>
        public bool OnToolbar => BandField is not null || Kind == Surface.LayoutMenu;

        public required int FocusRequest { get; init; }

        public bool InnerPopup { get; set; }

        public string? Refusal { get; set; }

        // Filter…
        public string Field { get; init; } = "";
        public HashSet<PivotItemKey> Hidden { get; init; } = [];
        public string ItemSearch { get; set; } = "";

        // Filter…'s search, asked of the source when the field has more Items than are listed.
        public int SearchGeneration { get; set; }
        public PivotReportItemsResult? SearchPage { get; set; }
        public SourceProblem? SearchProblem { get; set; }

        // Field Settings…
        public bool Subtotals { get; set; }
        public int Sort { get; set; }
        public IReadOnlyList<PivotChoice<PivotSort>> Sorts { get; init; } = [];

        // Value Field Settings…
        public int ValueIndex { get; init; }
        public string Caption { get; set; } = "";
        public bool CaptionTouched { get; set; }
        public PivotAggregation Aggregation { get; set; }
        public PivotShowValuesAs ShowValuesAs { get; set; }
        public string NumberFormat { get; set; } = "";
    }

    /// <summary>A field's Items as the source listed them under one Source Version, or why it could
    /// not; neither while the answer is on its way. The sequence orders listings by when they were
    /// asked for, so an older one landing late never replaces a newer one.</summary>
    private sealed class ItemsLoad
    {
        public required long Sequence { get; init; }

        public PivotReportItemsResult? Page { get; set; }

        public SourceProblem? Problem { get; set; }

        public bool Pending => Page is null && Problem is null;
    }

    /// <summary>A field's Items as Filter… and the report filter band see them: listed under the
    /// report's Source Version, or why they cannot be — or, while those are on their way, the
    /// newest listed under an earlier version of the same source (<see cref="Updating"/>); none of
    /// these while a first listing is on its way.</summary>
    private readonly record struct ItemsView(PivotReportItemsResult? Page, SourceProblem? Problem, bool Updating)
    {
        public bool Pending => Page is null && Problem is null;
    }

    // Items over all the data depend only on the data (ADR-0060): one listing per field, asked for
    // under the Source Version the report was computed from. When the report moves to a new
    // version of the same source, each field's newest listing stays in view until the new
    // version's lands (ADR-0066 refined): Hidden Items are keys, which name the same Items under
    // any version, so ticking and applying against them is safe, and a live report does not blank
    // the band and Filter… for a round trip after every redraw. A new source lists from nothing.
    private readonly Dictionary<string, ItemsLoad> _itemLoads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemsLoad> _earlierItems = new(StringComparer.Ordinal);
    private long _itemsSequence;
    private PivotReportSource? _itemsSource;
    private string? _itemsVersion;

    private IReadOnlyDictionary<string, PivotFieldInfo> Infos => _source!.Fields.ToDictionary(f => f.Name, f => f.Info, StringComparer.Ordinal);

    private PivotFieldInfo InfoOf(string field)
        => FieldOf(field)?.Info ?? throw new InvalidOperationException($"No Pivot Field named '{field}' is offered by the source.");

    // ---- Items, from the source (ADR-0066) ----------------------------------------------------

    /// <summary>A field's Items as they stand for the report on screen: listed under its Source
    /// Version, or why they cannot be; while those are on their way, or not asked for yet, the
    /// newest an earlier version of the same source listed (ADR-0066 refined); and nothing — a
    /// first listing on its way — while there is neither, or no report.</summary>
    private ItemsView ItemsOf(string field)
    {
        if (_report is null)
            return default;
        FollowReportVersion();
        if (_itemLoads.TryGetValue(field, out var load) && !load.Pending)
            return new ItemsView(load.Page, load.Problem, Updating: false);
        return _earlierItems.TryGetValue(field, out var earlier) ? new ItemsView(earlier.Page, null, Updating: true) : default;
    }

    /// <summary>
    /// Brings the listings to the report's Source Version. Under a new version of the same source,
    /// each field's Items listed so far become its earlier listing, which stays in view until the
    /// new version's land — unless the source could not list them, when nothing was in view to
    /// keep (ADR-0066 refined). Under a new source, nothing listed before is kept.
    /// </summary>
    private void FollowReportVersion()
    {
        if (_report is not { } report || _reportSource is not { } source)
            return;
        var version = report.SourceVersion + "|" + PivotReportJson.Write(report.Settings);
        if (ReferenceEquals(source, _itemsSource) && version == _itemsVersion)
            return;
        if (ReferenceEquals(source, _itemsSource))
        {
            foreach (var (field, load) in _itemLoads)
            {
                if (load.Page is not null)
                    KeepEarlier(field, load);
                else if (load.Problem is not null)
                    _earlierItems.Remove(field);
            }
        }
        else
        {
            _earlierItems.Clear();
        }
        _itemLoads.Clear();
        _itemsSource = source;
        _itemsVersion = version;
    }

    /// <summary>Keeps <paramref name="load"/>'s Items in view for <paramref name="field"/> until the
    /// report's version's land, unless a listing asked for after it is kept already.</summary>
    private void KeepEarlier(string field, ItemsLoad load)
    {
        if (!_earlierItems.TryGetValue(field, out var kept) || kept.Sequence < load.Sequence)
            _earlierItems[field] = load;
    }

    /// <summary>Asks the source for a field's Items under the report's Source Version, unless they
    /// are held or on their way. Filter… lists them, and the report filter band says from them what
    /// it shows.</summary>
    private void LoadItems(string field)
    {
        if (_report is not { } report || _reportSource is not { } source)
            return;
        FollowReportVersion();
        if (_itemLoads.ContainsKey(field))
            return;
        var load = new ItemsLoad { Sequence = ++_itemsSequence };
        _itemLoads[field] = load;
        _ = ListItemsAsync(source, new PivotReportItemsQuery(report.Version, field, Max: ItemListCap),
            page =>
            {
                load.Page = page;
                if (!ReferenceEquals(_itemLoads.GetValueOrDefault(field), load))
                {
                    // Listed under a version the report has since moved past: until the report's
                    // own land, these are the newest Items of the source to keep in view.
                    if (ReferenceEquals(source, _itemsSource))
                        KeepEarlier(field, load);
                    return;
                }
                // The report's version's Items replace whatever an earlier one listed.
                _earlierItems.Remove(field);
                // A search already typed into this field's open Filter…, beyond the Items listed,
                // is the source's to answer, under the report's version (ADR-0066 refined).
                if (_open is { Kind: Surface.ItemFilter } open && open.Field == field && open.ItemSearch.Length > 0
                    && page.Total > page.Items.Count)
                    SearchItems(open, open.ItemSearch);
            },
            problem => load.Problem = problem);
    }

    /// <summary>A new report — another Source Version — lists again what an open Filter… and the
    /// report filter band show; the Items they show meanwhile are the earlier version's.</summary>
    private void LoadShownItems()
    {
        FollowReportVersion();
        LoadBandItems();
        if (_open is { Kind: Surface.ItemFilter } open)
            LoadItems(open.Field);
    }

    /// <summary>The Items of every report filter that hides some, which the band summarises.</summary>
    private void LoadBandItems()
    {
        foreach (var placement in _layout.Filters)
        {
            if (placement.HiddenItems.Count > 0)
                LoadItems(placement.Field);
        }
    }

    private async Task ListItemsAsync(PivotReportSource source, PivotReportItemsQuery query, Action<PivotReportItemsResult> listed, Action<SourceProblem> failed)
    {
        try
        {
            PivotReportItemsResult page;
            try
            {
                page = await source.ItemsAsync(query);
            }
            catch (Exception error)
            {
                failed(new SourceProblem(null, error));
                Repaint();
                return;
            }
            if (page.Refusal is { } refusal)
                failed(new SourceProblem(refusal.SourceRefusal ?? (refusal.Kind == PivotReportRefusalKind.ReportVersionNotHeld
                    ? new(PivotSourceRefusalKind.SourceVersionNotHeld, refusal.Message) : null),
                    refusal.Kind == PivotReportRefusalKind.ReportVersionNotHeld ? null : new InvalidOperationException(refusal.Message)));
            else
                listed(page);
            Repaint();
        }
        catch (Exception error) when (!_disposed)
        {
            await DispatchExceptionAsync(error);
        }
    }

    private void Repaint()
    {
        if (!_disposed)
            StateHasChanged();
    }

    /// <summary>Why a listing cannot be shown, in words: "The data has changed — refresh." when
    /// the source can no longer answer under the report's Source Version (ADR-0066).</summary>
    private string ProblemText(SourceProblem problem)
        => problem.Refusal is { } refusal
            ? RefusalOf(refusal)
            : PivotWords.Fill(Word("source-failed"), problem.Error?.Message ?? "");

    // ---- Opening and closing ---------------------------------------------------------------

    private void Open(OpenSurface surface)
    {
        _open = surface;
        StateHasChanged();
    }

    private void CloseOpen()
    {
        if (_open is not { } open)
            return;
        _open = null;
        // The keyboard goes back to what opened it (ADR-0061).
        if (open.Entry is { } entry)
            _entryFocus[entry] = ++_focusSequence;
        else if (open.BandField is { } field)
            _bandFocus[field] = ++_focusSequence;
        else if (open.Kind == Surface.LayoutMenu)
            _layoutMenuFocus = ++_focusSequence;
        StateHasChanged();
    }

    private void CloseOpenQuietly() => _open = null;

    private void DismissFieldMenu(OpenSurface? menu)
    {
        // The gesture names the menu it was painted against. A replacement surface must not be
        // closed by an older press or focus event arriving over the circuit (ADR-0061).
        if (menu is not null && ReferenceEquals(_open, menu))
        {
            CloseOpenQuietly();
            // The outside operation owns the keyboard: do not ask the entry for it back.
            StateHasChanged();
        }
    }

    private Task OnEscapeAsync()
    {
        // While a popup of the content's own is open, Escape is that popup's (ADR-0039).
        if (_open is { InnerPopup: false })
            CloseOpen();
        return Task.CompletedTask;
    }

    private void ToggleMenu(PivotEntry entry)
    {
        if (_open is { Kind: Surface.Menu } open && open.Entry == entry)
        {
            CloseOpen();
            return;
        }
        Open(new OpenSurface { Kind = Surface.Menu, Entry = entry, FocusRequest = ++_focusSequence });
    }

    private Task OpenPanelAsync(PivotEntry entry, Surface kind)
    {
        Open(NewPanel(kind, entry, bandField: null));
        return Task.CompletedTask;
    }

    private Task OpenFromReportAsync(PivotEntry entry, Surface kind)
    {
        if (!_fieldListShown)
        {
            _fieldListShown = true;
            _ = ShowFieldListChanged.InvokeAsync(true);
        }
        Open(NewPanel(kind, entry, bandField: null));
        return Task.CompletedTask;
    }

    private void ToggleBand(string field)
    {
        if (_open is { BandField: not null } open && open.BandField == field)
        {
            CloseOpen();
            return;
        }
        if (_layout.PlacementOf(field) is not { } at)
            return;
        Open(NewPanel(Surface.ItemFilter, new PivotEntry(at.Area, at.Index), field));
    }

    private OpenSurface NewPanel(Surface kind, PivotEntry entry, string? bandField)
    {
        var focus = ++_focusSequence;
        var layout = bandField is null ? PaneLayout : _layout;
        switch (kind)
        {
            case Surface.ItemFilter:
            {
                var placement = layout.PlacementsIn(entry.Area)[entry.Index];
                LoadItems(placement.Field);
                return new OpenSurface
                {
                    Kind = kind,
                    Entry = bandField is null ? entry : null,
                    BandField = bandField,
                    FocusRequest = focus,
                    Field = placement.Field,
                    Hidden = placement.HiddenItems.ToHashSet(),
                };
            }
            case Surface.FieldSettings:
            {
                var placement = layout.PlacementsIn(entry.Area)[entry.Index];
                var sorts = SortChoices(layout);
                var current = sorts.Select((choice, i) => (choice, i)).FirstOrDefault(c => c.choice.Value == placement.Sort).i;
                return new OpenSurface
                {
                    Kind = kind,
                    Entry = entry,
                    FocusRequest = focus,
                    Field = placement.Field,
                    Subtotals = placement.Subtotals,
                    Sorts = sorts,
                    Sort = current,
                };
            }
            default:
            {
                var value = layout.Values[entry.Index];
                return new OpenSurface
                {
                    Kind = Surface.ValueFieldSettings,
                    Entry = entry,
                    FocusRequest = focus,
                    Field = value.Field,
                    ValueIndex = entry.Index,
                    Caption = CaptionsOf(layout).ElementAtOrDefault(entry.Index) ?? DefaultCaption(value.Aggregation, value.Field),
                    Aggregation = value.Aggregation,
                    ShowValuesAs = value.ShowValuesAs,
                    NumberFormat = value.NumberFormat ?? "",
                };
            }
        }
    }

    private IReadOnlyList<PivotChoice<PivotSort>> SortChoices(PivotLayout layout)
    {
        var sorts = new List<PivotChoice<PivotSort>>
        {
            new(PivotSort.Ascending, Word("sort-label-ascending")),
            new(PivotSort.Descending, Word("sort-label-descending")),
        };
        var captions = CaptionsOf(layout);
        for (var i = 0; i < captions.Count; i++)
        {
            sorts.Add(new(new PivotSort(PivotSortDirection.Ascending, i), PivotWords.Fill(Word("sort-value-ascending"), captions[i])));
            sorts.Add(new(new PivotSort(PivotSortDirection.Descending, i), PivotWords.Fill(Word("sort-value-descending"), captions[i])));
        }
        return sorts;
    }

    /// <summary>The Value Fields' captions of a layout, by the engine's rule (ADR-0060): what the
    /// pane calls each one, whichever layout it shows.</summary>
    private IReadOnlyList<string> CaptionsOf(PivotLayout layout)
    {
        try
        {
            return ValueCaptions.Resolve(layout.Values, Infos, _options);
        }
        catch (InvalidOperationException)
        {
            return layout.Values.Select(v => v.Caption ?? DefaultCaption(v.Aggregation, v.Field)).ToArray();
        }
    }

    private string DefaultCaption(PivotAggregation aggregation, string field)
        => PivotWords.Fill(Word(PivotWords.CaptionOf(aggregation)), InfoOf(field).Caption);

    // ---- The pane --------------------------------------------------------------------------

    private RenderFragment FieldList() => builder =>
    {
        var context = FieldListContext();
        if (PivotChrome?.FieldList(context) is { } custom)
        {
            builder.AddContent(0, custom);
            return;
        }
        builder.OpenComponent<PivotFieldListView>(1);
        builder.AddComponentParameter(2, nameof(PivotFieldListView.Context), context);
        builder.CloseComponent();
    };

    private PivotFieldListContext FieldListContext()
    {
        var layout = PaneLayout;
        var source = _source!;
        var fields = _source!.Fields
            .Where(f => _search.Length == 0 || f.Caption.Contains(_search, StringComparison.CurrentCultureIgnoreCase))
            .Select(f =>
            {
                var placement = layout.PlacementOf(f.Name) is { } at ? layout.PlacementsIn(at.Area)[at.Index] : null;
                var name = f.Name;
                return new PivotFieldEntry(name, f.Caption, f.Type, layout.Places(name), placement?.HiddenItems.Count > 0,
                    () => PaneApplyAsync(PaneLayout.Places(name)
                        ? PivotLayoutEdits.Untick(PaneLayout, name)
                        : PivotLayoutEdits.Tick(PaneLayout, InfoOf(name))));
            })
            .ToArray();

        var captions = CaptionsOf(layout);
        var areas = AreaOrder.Select(area =>
        {
            var entries = new List<PivotAreaEntryView>();
            if (area == PivotArea.Values)
            {
                for (var i = 0; i < layout.Values.Count; i++)
                    entries.Add(EntryView(new PivotEntry(area, i), captions[i], false));
            }
            else
            {
                var placements = layout.PlacementsIn(area);
                for (var i = 0; i < placements.Count; i++)
                    entries.Add(EntryView(new PivotEntry(area, i), InfoOf(placements[i].Field).Caption, placements[i].HiddenItems.Count > 0));
                if (layout.Values.Count >= 2 && area == (layout.ValuesAxis == PivotAxis.Rows ? PivotArea.Rows : PivotArea.Columns))
                    entries.Add(EntryView(PivotEntry.ValuesPseudoField(layout.ValuesAxis), Word(PivotWords.ValuesPseudoField), false));
            }
            return new PivotAreaView(area, Word(AreaWord(area)), entries, Accepts(area),
                _dropAt is { } at && at.Area == area ? at.Index : null);
        }).ToArray();

        return new PivotFieldListContext(
            Word("field-list"),
            fields,
            _search,
            SearchChanged,
            areas,
            CurrentDrag,
            subject => StartDrag(subject, layout, source),
            DragOver,
            (area, index) => DropAsync(layout, area, index),
            CanRemoveDrag,
            () => DropOnListAsync(layout),
            EndDrag,
            Word)
        {
            Close = () => SetFieldListShownAsync(false),
            DeferLayoutUpdate = _pending is not null,
            DeferLayoutUpdateChanged = SetDeferAsync,
            CanUpdate = _pending is { } pending && !ReferenceEquals(pending, _layout),
            Update = UpdateAsync,
        };
    }

    private PivotAreaEntryView EntryView(PivotEntry entry, string caption, bool filtered)
    {
        var open = _open is { OnToolbar: false } surface && surface.Entry == entry;
        return new PivotAreaEntryView(entry, caption, filtered, open, () => ToggleMenu(entry),
            open ? PopupFragment(_open!) : null, _entryFocus.GetValueOrDefault(entry));
    }

    private static string AreaWord(PivotArea area) => area switch
    {
        PivotArea.Filters => "area-filters",
        PivotArea.Columns => "area-columns",
        PivotArea.Rows => "area-rows",
        _ => "area-values",
    };

    private void SearchChanged(string? search)
    {
        _search = search ?? "";
        StateHasChanged();
    }

    // ---- Drag and drop (ADR-0061): Blazor's own events, what is dragged held here -----------

    /// <summary>Whether a drop of what is dragged on <paramref name="area"/> would change anything:
    /// Σ Values goes only to Rows and Columns.</summary>
    private bool Accepts(PivotArea area)
        => CurrentDrag is { } drag && (drag.Entry is not { IsValuesPseudoField: true } || area is PivotArea.Rows or PivotArea.Columns);

    private PivotDragSubject? CurrentDrag
        => _fieldListShown && _drag is { } drag && ReferenceEquals(drag.Layout, PaneLayout)
            && ReferenceEquals(drag.Source, _source!) ? drag.Subject : null;

    private bool CanRemoveDrag => CurrentDrag?.Entry is { IsValuesPseudoField: false };

    private bool ReportAcceptsDrop => CanRemoveDrag && _selectedSheet is null && _dialog is null;

    private void StartDrag(PivotDragSubject subject, PivotLayout layout, PivotReportSource source)
    {
        _drag = new FieldDrag(subject, layout, source);
        _dropAt = null;
        CloseOpenQuietly();
        StateHasChanged();
    }

    private void DragOver(PivotArea area, int index)
    {
        if (CurrentDrag is null || _dropAt == (area, index))
            return;
        _dropAt = (area, index);
        StateHasChanged();
    }

    private void EndDrag()
    {
        if (_drag is null && _dropAt is null)
            return;
        _drag = null;
        _dropAt = null;
        StateHasChanged();
    }

    private PivotDragSubject? TakeDrag(PivotLayout targetLayout)
    {
        // Both ends name the layout the browser saw. A late gesture cannot reuse an index
        // against a replacement layout, even when the dragged field still exists (ADR-0061).
        var drag = ReferenceEquals(targetLayout, PaneLayout) ? CurrentDrag : null;
        _drag = null;
        _dropAt = null;
        return drag;
    }

    private Task DropAsync(PivotLayout layout, PivotArea area, int index)
    {
        var drag = TakeDrag(layout);
        if (drag is null)
        {
            StateHasChanged();
            return Task.CompletedTask;
        }
        if (drag.Field is { } field)
            return PaneApplyAsync(PivotLayoutEdits.Place(layout, InfoOf(field), area, index));
        var entry = drag.Entry!.Value;
        return PaneApplyAsync(PivotLayoutEdits.Move(layout, entry, area, index, entry.IsValuesPseudoField ? null : InfoOf(FieldOf(layout, entry))));
    }

    private Task DropOnReportAsync(PivotLayout layout, bool reportShown)
    {
        if (reportShown && _selectedSheet is null && _dialog is null)
            return DropOnListAsync(layout);
        EndDrag();
        return Task.CompletedTask;
    }

    private Task DropOnListAsync(PivotLayout layout)
    {
        var drag = TakeDrag(layout);
        if (drag?.Entry is { IsValuesPseudoField: false } entry)
            return PaneApplyAsync(PivotLayoutEdits.Remove(layout, entry));
        StateHasChanged();
        return Task.CompletedTask;
    }

    private static string FieldOf(PivotLayout layout, PivotEntry entry)
        => entry.Area == PivotArea.Values ? layout.Values[entry.Index].Field : layout.PlacementsIn(entry.Area)[entry.Index].Field;

    // ---- A placed field's menu (ADR-0061) ---------------------------------------------------

    private IReadOnlyList<PivotCommand> MenuFor(PivotEntry entry)
    {
        var layout = PaneLayout;
        var commands = new List<PivotCommand>();
        void Add(string id, bool enabled, Func<Task> run)
            => commands.Add(new PivotCommand(id, Word(id), enabled, () =>
            {
                CloseOpen();
                return run();
            }));
        Task Move(PivotArea area, int index)
            => PaneApplyAsync(PivotLayoutEdits.Move(layout, entry, area, index, entry.IsValuesPseudoField ? null : InfoOf(FieldOf(layout, entry))));

        if (entry.IsValuesPseudoField)
        {
            var onRows = layout.ValuesAxis == PivotAxis.Rows;
            Add(PivotCommandIds.MoveToRows, !onRows, () => Move(PivotArea.Rows, int.MaxValue));
            Add(PivotCommandIds.MoveToColumns, onRows, () => Move(PivotArea.Columns, int.MaxValue));
            return commands;
        }

        var count = entry.Area == PivotArea.Values ? layout.Values.Count : layout.PlacementsIn(entry.Area).Count;
        Add(PivotCommandIds.MoveUp, entry.Index > 0, () => Move(entry.Area, entry.Index - 1));
        Add(PivotCommandIds.MoveDown, entry.Index < count - 1, () => Move(entry.Area, entry.Index + 2));
        Add(PivotCommandIds.MoveToBeginning, entry.Index > 0, () => Move(entry.Area, 0));
        Add(PivotCommandIds.MoveToEnd, entry.Index < count - 1, () => Move(entry.Area, count));
        Add(PivotCommandIds.MoveToFilters, entry.Area != PivotArea.Filters, () => Move(PivotArea.Filters, int.MaxValue));
        Add(PivotCommandIds.MoveToRows, entry.Area != PivotArea.Rows, () => Move(PivotArea.Rows, int.MaxValue));
        Add(PivotCommandIds.MoveToColumns, entry.Area != PivotArea.Columns, () => Move(PivotArea.Columns, int.MaxValue));
        Add(PivotCommandIds.MoveToValues, entry.Area != PivotArea.Values, () => Move(PivotArea.Values, int.MaxValue));
        Add(PivotCommandIds.RemoveField, true, () => PaneApplyAsync(PivotLayoutEdits.Remove(layout, entry)));

        if (entry.Area == PivotArea.Values)
        {
            Add(PivotCommandIds.ValueFieldSettings, true, () => OpenPanelAsync(entry, Surface.ValueFieldSettings));
            return commands;
        }
        var placement = layout.PlacementsIn(entry.Area)[entry.Index];
        if (entry.Area is PivotArea.Rows or PivotArea.Columns)
        {
            var field = placement.Field;
            Add(PivotCommandIds.SortAscending, placement.Sort != PivotSort.Ascending,
                () => PaneApplyAsync(PivotLayoutEdits.SetSort(layout, field, PivotSort.Ascending)));
            Add(PivotCommandIds.SortDescending, placement.Sort != PivotSort.Descending,
                () => PaneApplyAsync(PivotLayoutEdits.SetSort(layout, field, PivotSort.Descending)));
            Add(PivotCommandIds.FilterItems, true, () => OpenPanelAsync(entry, Surface.ItemFilter));
            // The innermost field has nothing under its Items to collapse (ADR-0060).
            var outer = entry.Index < count - 1;
            Add(PivotCommandIds.ExpandField, outer && (placement.Collapsed || placement.ToggledItems.Count > 0),
                () => PaneApplyAsync(PivotLayoutEdits.SetFieldCollapsed(layout, field, false)));
            Add(PivotCommandIds.CollapseField, outer && (!placement.Collapsed || placement.ToggledItems.Count > 0),
                () => PaneApplyAsync(PivotLayoutEdits.SetFieldCollapsed(layout, field, true)));
            Add(PivotCommandIds.FieldSettings, true, () => OpenPanelAsync(entry, Surface.FieldSettings));
        }
        else
        {
            Add(PivotCommandIds.FilterItems, true, () => OpenPanelAsync(entry, Surface.ItemFilter));
        }
        return commands;
    }

    // ---- The frame and its contents -----------------------------------------------------------

    private RenderFragment PopupFragment(OpenSurface open) => builder =>
    {
        builder.OpenComponent<PivotPopupFrame>(0);
        builder.AddComponentParameter(1, nameof(PivotPopupFrame.Role), open.Kind is Surface.Menu or Surface.LayoutMenu ? "menu" : "dialog");
        builder.AddComponentParameter(2, nameof(PivotPopupFrame.Label), TitleOf(open));
        builder.AddComponentParameter(3, nameof(PivotPopupFrame.Overlay), open.OnToolbar);
        builder.AddComponentParameter(4, nameof(PivotPopupFrame.AlignEnd), open.Kind == Surface.LayoutMenu);
        builder.AddComponentParameter(5, nameof(PivotPopupFrame.OnEscape), _escape);
        builder.AddComponentParameter(6, nameof(PivotPopupFrame.ChildContent), Content(open));
        builder.CloseComponent();
    };

    private string TitleOf(OpenSurface open) => open.Kind switch
    {
        Surface.Menu => PivotWords.Fill(Word("field-menu"), EntryCaption(open.Entry!.Value)),
        Surface.LayoutMenu => Word(PivotCommandIds.LayoutMenu),
        Surface.ItemFilter => PivotWords.Fill(Word("filter-of"), InfoOf(open.Field).Caption),
        Surface.FieldSettings => Word(PivotCommandIds.FieldSettings),
        _ => Word(PivotCommandIds.ValueFieldSettings),
    };

    private string EntryCaption(PivotEntry entry)
    {
        var layout = PaneLayout;
        return entry.IsValuesPseudoField ? Word(PivotWords.ValuesPseudoField)
            : entry.Area == PivotArea.Values ? CaptionsOf(layout).ElementAtOrDefault(entry.Index) ?? FieldOf(layout, entry)
            : InfoOf(FieldOf(layout, entry)).Caption;
    }

    private RenderFragment Content(OpenSurface open)
    {
        switch (open.Kind)
        {
            case Surface.Menu:
            {
                var context = new PivotMenuContext(TitleOf(open), MenuFor(open.Entry!.Value), CloseOpen, open.FocusRequest);
                return PivotChrome?.Menu(context) ?? View<PivotMenuView, PivotMenuContext>(context);
            }
            case Surface.LayoutMenu:
            {
                var context = new PivotMenuContext(TitleOf(open), LayoutMenuCommands(), CloseOpen, open.FocusRequest);
                return PivotChrome?.Menu(context) ?? View<PivotMenuView, PivotMenuContext>(context);
            }
            case Surface.ItemFilter:
            {
                var context = ItemFilterContext(open);
                return PivotChrome?.ItemFilter(context) ?? View<PivotItemFilterView, PivotItemFilterContext>(context);
            }
            case Surface.FieldSettings:
            {
                var context = FieldSettingsContext(open);
                return PivotChrome?.FieldSettings(context) ?? View<PivotFieldSettingsView, PivotFieldSettingsContext>(context);
            }
            default:
            {
                var context = ValueFieldSettingsContext(open);
                return PivotChrome?.ValueFieldSettings(context) ?? View<PivotValueFieldSettingsView, PivotValueFieldSettingsContext>(context);
            }
        }
    }

    private static RenderFragment View<TView, TContext>(TContext context) where TView : IComponent => builder =>
    {
        builder.OpenComponent<TView>(0);
        builder.AddComponentParameter(1, "Context", context);
        builder.CloseComponent();
    };

    private void SetInnerPopup(bool open)
    {
        if (_open is { } surface)
            surface.InnerPopup = open;
    }

    // Filter… (ADR-0061/0066): the field's Items as the source lists them under the report's
    // Source Version, ordered as the field is; the draft of ticks; a search that narrows the
    // painted labels among the Items held, and asks the source only when the field has more Items
    // than are listed; and OK.
    private PivotItemFilterContext ItemFilterContext(OpenSurface open)
    {
        var layout = open.BandField is null ? PaneLayout : _layout;
        var items = ItemsOf(open.Field);
        var search = open.ItemSearch;
        var field = FieldOf(open.Field)!;
        IReadOnlyList<PivotItemInfo> matches = [];
        var loading = items.Pending;
        string? unavailable = items.Problem is { } problem ? ProblemText(problem) : null;
        var allHeld = true;
        var itemCount = 0;
        var matchCount = 0;
        if (items.Page is { } page)
        {
            itemCount = page.Total;
            allHeld = page.Total <= page.Items.Count;
            if (!allHeld && search.Length > 0)
            {
                // More Items than Filter… holds: the typed search is the source's to answer.
                if (open.SearchProblem is { } searchProblem)
                    unavailable = ProblemText(searchProblem);
                else if (open.SearchPage is { } found)
                {
                    matches = found.Items;
                    matchCount = found.Total;
                }
                else
                {
                    loading = true;
                }
            }
            else
            {
                var every = page.Items;
                var compare = _culture.CompareInfo;
                matches = search.Length == 0
                    ? every
                    : every.Where(i => compare.IndexOf(i.Label, search, CompareOptions.IgnoreCase) >= 0).ToArray();
                matchCount = search.Length == 0 ? page.Total : matches.Count;
            }
        }
        var listed = matches.Take(ItemListCap).Select(i => new PivotItemChoice(i.Key, i.Label, !open.Hidden.Contains(i.Key))).ToArray();
        var ticked = listed.Count(i => i.Ticked);
        bool? all = ticked == listed.Length ? true : ticked == 0 ? false : null;
        // The Items in view, an earlier version's while the report's are on their way: Hidden Items
        // are keys, so OK applies against them safely (ADR-0066 refined).
        IReadOnlyList<PivotItemKey> held = items.Page?.Items.Select(item => item.Key).ToArray() ?? [];
        var canApply = !loading && unavailable is null
            && (!allHeld || held.Count == 0 || held.Any(key => !open.Hidden.Contains(key)));
        return new PivotItemFilterContext(
            InfoOf(open.Field).Caption,
            listed,
            itemCount,
            matchCount,
            ItemListCap,
            search,
            text => SearchItems(open, text ?? ""),
            all,
            tick =>
            {
                foreach (var item in listed)
                {
                    if (tick)
                        open.Hidden.Remove(item.Key);
                    else
                        open.Hidden.Add(item.Key);
                }
                open.Refusal = null;
                StateHasChanged();
            },
            (key, tick) =>
            {
                if (tick)
                    open.Hidden.Remove(key);
                else
                    open.Hidden.Add(key);
                open.Refusal = null;
                StateHasChanged();
            },
            canApply,
            open.Refusal ?? (canApply || loading || unavailable is not null ? null : Word("refused-hides-every-item")),
            () => ApplyItemFilterAsync(open, held, allHeld),
            CloseOpen,
            open.FocusRequest,
            Word)
        {
            IsLoading = loading,
            IsUpdating = items.Updating,
            Unavailable = unavailable,
        };
    }

    private void SearchItems(OpenSurface open, string text)
    {
        open.ItemSearch = text;
        open.SearchPage = null;
        open.SearchProblem = null;
        var generation = ++open.SearchGeneration;
        if (text.Length > 0 && ItemsOf(open.Field).Page is { } page && page.Total > page.Items.Count
            && _reportSource is { } source && _report is { } report)
        {
            // Under the report's Source Version, whichever version listed the Items in view. An
            // answer to an older search is discarded, as an answer to a superseded question is.
            _ = ListItemsAsync(source, new PivotReportItemsQuery(report.Version, open.Field, text, ItemListCap),
                found =>
                {
                    if (open.SearchGeneration == generation)
                        open.SearchPage = found;
                },
                problem =>
                {
                    if (open.SearchGeneration == generation)
                        open.SearchProblem = problem;
                });
        }
        StateHasChanged();
    }

    private Task ApplyItemFilterAsync(OpenSurface open, IReadOnlyList<PivotItemKey> held, bool allHeld)
    {
        // With more Items than Filter… holds, that every one is unticked cannot be told, and
        // nothing is refused for it: the rule needs every Item (ADR-0060).
        IReadOnlyCollection<PivotItemKey> items = allHeld ? held.ToArray() : [];
        var hidden = open.Hidden.ToArray();
        var field = open.Field;
        var layout = open.BandField is null ? PaneLayout : _layout;
        var result = PivotLayoutEdits.SetHiddenItems(layout, field, hidden, items);
        if (result.IsRefused)
        {
            open.Refusal = Word(result.RefusalWord!);
            StateHasChanged();
            return Task.CompletedTask;
        }
        CloseOpen();
        if (open.BandField is null)
            return PaneApplyAsync(result.Layout);
        return ReportEditAsync(l => PivotLayoutEdits.SetHiddenItems(l, field, hidden, items) is { IsRefused: false } applied
            ? applied.Layout
            : throw new ArgumentException($"'{field}' would hide every Item."));
    }

    private PivotFieldSettingsContext FieldSettingsContext(OpenSurface open)
        => new(
            InfoOf(open.Field).Caption,
            open.Subtotals,
            subtotals =>
            {
                open.Subtotals = subtotals;
                StateHasChanged();
            },
            open.Sort,
            sort =>
            {
                open.Sort = Math.Clamp(sort, 0, open.Sorts.Count - 1);
                StateHasChanged();
            },
            open.Sorts,
            () =>
            {
                var layout = PivotLayoutEdits.SetSubtotals(PaneLayout, open.Field, open.Subtotals);
                layout = PivotLayoutEdits.SetSort(layout, open.Field, open.Sorts[open.Sort].Value);
                CloseOpen();
                return PaneApplyAsync(layout);
            },
            CloseOpen,
            open.FocusRequest,
            SetInnerPopup,
            Word);

    private PivotValueFieldSettingsContext ValueFieldSettingsContext(OpenSurface open)
    {
        var percent = open.ShowValuesAs != PivotShowValuesAs.NoCalculation;
        var features = _source!.Features;
        return new PivotValueFieldSettingsContext(
            InfoOf(open.Field).Caption,
            open.Caption,
            caption =>
            {
                open.Caption = caption ?? "";
                open.CaptionTouched = true;
                open.Refusal = null;
                StateHasChanged();
            },
            open.Aggregation,
            aggregation =>
            {
                // An Aggregation the source does not answer is offered disabled, and choosing it
                // anyway changes nothing: it is never asked for (ADR-0066).
                if (!features.Offers(aggregation))
                    return;
                // A caption the user has not written follows the Aggregation, as Excel's Custom
                // Name box does.
                if (!open.CaptionTouched && PaneLayout.Values[open.ValueIndex].Caption is null)
                    open.Caption = DefaultCaption(aggregation, open.Field);
                open.Aggregation = aggregation;
                open.Refusal = null;
                StateHasChanged();
            },
            Enum.GetValues<PivotAggregation>().Select(a =>
            {
                var name = Word(PivotWords.AggregationName(a));
                return features.Offers(a)
                    ? new PivotChoice<PivotAggregation>(a, name)
                    : new PivotChoice<PivotAggregation>(a, name) { Enabled = false, Reason = PivotWords.Fill(Word("aggregation-not-offered"), name) };
            }).ToArray(),
            open.ShowValuesAs,
            showAs =>
            {
                open.ShowValuesAs = showAs;
                open.Refusal = null;
                StateHasChanged();
            },
            Enum.GetValues<PivotShowValuesAs>().Select(s => new PivotChoice<PivotShowValuesAs>(s, Word(PivotWords.ShowValuesAsName(s)))).ToArray(),
            open.NumberFormat,
            format =>
            {
                open.NumberFormat = format ?? "";
                open.Refusal = null;
                StateHasChanged();
            },
            Sample(open.NumberFormat, percent),
            open.Refusal,
            () =>
            {
                var layout = PaneLayout;
                if (!features.Offers(open.Aggregation))
                {
                    open.Refusal = PivotWords.Fill(Word("aggregation-not-offered"), Word(PivotWords.AggregationName(open.Aggregation)));
                    StateHasChanged();
                    return Task.CompletedTask;
                }
                var current = layout.Values[open.ValueIndex];
                var caption = open.Caption.Trim();
                string? own = !open.CaptionTouched && current.Caption is null ? null
                    : string.Equals(caption, DefaultCaption(open.Aggregation, open.Field), StringComparison.Ordinal) ? null
                    : caption;
                var value = current with
                {
                    Aggregation = open.Aggregation,
                    ShowValuesAs = open.ShowValuesAs,
                    Caption = own,
                    NumberFormat = open.NumberFormat.Trim().Length == 0 ? null : open.NumberFormat.Trim(),
                };
                var result = PivotLayoutEdits.SetValueField(layout, open.ValueIndex, value, Infos);
                if (result.IsRefused)
                {
                    open.Refusal = Word(result.RefusalWord!);
                    StateHasChanged();
                    return Task.CompletedTask;
                }
                CloseOpen();
                return PaneApplyAsync(result.Layout);
            },
            CloseOpen,
            open.FocusRequest,
            SetInnerPopup,
            Word);
    }

    /// <summary>A number shown in the draft's format — or the reason it cannot be — so a format is
    /// seen before it is applied (ADR-0061).</summary>
    private string Sample(string format, bool percent)
    {
        var trimmed = format.Trim();
        if (trimmed.Length > 0 && PivotNumberFormat.Check(trimmed) is not null)
            return Word("refused-number-format");
        var sample = percent ? 0.1234 : -1234.5678;
        var text = sample.ToString(trimmed.Length == 0 ? (percent ? "0.00%" : "G15") : trimmed, _culture);
        return PivotWords.Fill(Word("sample"), text);
    }
}
