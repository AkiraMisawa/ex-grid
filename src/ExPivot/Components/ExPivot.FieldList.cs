using System.Globalization;
using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExPivot.Components;

// The Field List half: the pane, the report filter band, a placed field's menu and the three
// panels (ADR-0060). ExPivot holds every piece of state and applies every rule; the Chrome draws
// what it is handed, or the built-in views do.
public partial class ExPivot<TRecord>
{
    /// <summary>Filter… lists at most this many Items at once; the search narrows the rest.
    /// Excel's own filter lists stop at the same number.</summary>
    public const int ItemListCap = 10_000;

    private static readonly object Emitted = new();
    private static readonly PivotArea[] AreaOrder = [PivotArea.Filters, PivotArea.Columns, PivotArea.Rows, PivotArea.Values];

    private readonly Dictionary<string, IReadOnlyList<PivotItemInfo>> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<PivotEntry, int> _entryFocus = [];
    private readonly Dictionary<string, int> _bandFocus = new(StringComparer.Ordinal);
    private readonly EventCallback _escape;
    private OpenSurface? _open;
    private PivotDragSubject? _drag;
    private (PivotArea Area, int Index)? _dropAt;
    private string _search = "";
    private int _focusSequence;

    private enum Surface
    {
        Menu,
        ItemFilter,
        FieldSettings,
        ValueFieldSettings,
    }

    /// <summary>The one menu or panel open in this ExPivot, with its draft (ADR-0060).</summary>
    private sealed class OpenSurface
    {
        public required Surface Kind { get; init; }

        /// <summary>The Field List entry that opened it, or null.</summary>
        public PivotEntry? Entry { get; init; }

        /// <summary>The report filter that opened it from the band, or null.</summary>
        public string? BandField { get; init; }

        public bool OnBand => BandField is not null;

        public required int FocusRequest { get; init; }

        public bool InnerPopup { get; set; }

        public string? Refusal { get; set; }

        // Filter…
        public string Field { get; init; } = "";
        public IReadOnlyList<PivotItemInfo> Items { get; init; } = [];
        public HashSet<PivotItemKey> Hidden { get; init; } = [];
        public string ItemSearch { get; set; } = "";

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

    private PivotOptions Options => new() { Culture = _culture, Label = Label };

    private IReadOnlyDictionary<string, PivotFieldInfo> Infos => Fields.ToDictionary(f => f.Name, f => f.Info, StringComparer.Ordinal);

    private PivotFieldInfo InfoOf(string field)
        => Fields.FirstOrDefault(f => f.Name == field)?.Info
            ?? throw new InvalidOperationException($"No Pivot Field named '{field}' is declared.");

    /// <summary>Applies a layout the user's gesture produced (ADR-0060): at once, and then told.</summary>
    private async Task ApplyAsync(PivotLayout layout)
    {
        _notice = null;
        if (ReferenceEquals(layout, _layout))
        {
            StateHasChanged();
            return;
        }
        _layout = layout;
        _emitted.AddOrUpdate(layout, Emitted);
        Recompute();
        StateHasChanged();
        await LayoutChanged.InvokeAsync(layout);
    }

    private IReadOnlyList<PivotItemInfo> ItemsOf(string field)
    {
        if (_cube is null)
            return [];
        if (!_items.TryGetValue(field, out var items))
        {
            items = PivotEngine.ItemsOf(_cube, _layout, field, Options);
            _items[field] = items;
        }
        return items;
    }

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
        // The keyboard goes back to what opened it (ADR-0060).
        if (open.Entry is { } entry)
            _entryFocus[entry] = ++_focusSequence;
        else if (open.BandField is { } field)
            _bandFocus[field] = ++_focusSequence;
        StateHasChanged();
    }

    private void CloseOpenQuietly() => _open = null;

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
        _fieldListShown = true;
        Open(NewPanel(kind, entry, bandField: null));
        return Task.CompletedTask;
    }

    private void ToggleBand(string field)
    {
        if (_open is { OnBand: true } open && open.BandField == field)
        {
            CloseOpen();
            return;
        }
        var at = _layout.PlacementOf(field)!.Value;
        Open(NewPanel(Surface.ItemFilter, new PivotEntry(at.Area, at.Index), field));
    }

    private OpenSurface NewPanel(Surface kind, PivotEntry entry, string? bandField)
    {
        var focus = ++_focusSequence;
        switch (kind)
        {
            case Surface.ItemFilter:
            {
                var placement = _layout.PlacementsIn(entry.Area)[entry.Index];
                return new OpenSurface
                {
                    Kind = kind,
                    Entry = bandField is null ? entry : null,
                    BandField = bandField,
                    FocusRequest = focus,
                    Field = placement.Field,
                    Items = ItemsOf(placement.Field),
                    Hidden = placement.HiddenItems.ToHashSet(),
                };
            }
            case Surface.FieldSettings:
            {
                var placement = _layout.PlacementsIn(entry.Area)[entry.Index];
                var sorts = SortChoices();
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
                var value = _layout.Values[entry.Index];
                return new OpenSurface
                {
                    Kind = Surface.ValueFieldSettings,
                    Entry = entry,
                    FocusRequest = focus,
                    Field = value.Field,
                    ValueIndex = entry.Index,
                    Caption = _report?.ValueCaptions.ElementAtOrDefault(entry.Index) ?? DefaultCaption(value.Aggregation, value.Field),
                    Aggregation = value.Aggregation,
                    ShowValuesAs = value.ShowValuesAs,
                    NumberFormat = value.NumberFormat ?? "",
                };
            }
        }
    }

    private IReadOnlyList<PivotChoice<PivotSort>> SortChoices()
    {
        var sorts = new List<PivotChoice<PivotSort>>
        {
            new(PivotSort.Ascending, Word("sort-label-ascending")),
            new(PivotSort.Descending, Word("sort-label-descending")),
        };
        var captions = _report?.ValueCaptions ?? [];
        for (var i = 0; i < captions.Count; i++)
        {
            sorts.Add(new(new PivotSort(PivotSortDirection.Ascending, i), PivotWords.Fill(Word("sort-value-ascending"), captions[i])));
            sorts.Add(new(new PivotSort(PivotSortDirection.Descending, i), PivotWords.Fill(Word("sort-value-descending"), captions[i])));
        }
        return sorts;
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
        var fields = Fields
            .Where(f => _search.Length == 0 || f.Caption.Contains(_search, StringComparison.CurrentCultureIgnoreCase))
            .Select(f =>
            {
                var placement = _layout.PlacementOf(f.Name) is { } at ? _layout.PlacementsIn(at.Area)[at.Index] : null;
                var name = f.Name;
                return new PivotFieldEntry(name, f.Caption, f.Type, _layout.Places(name), placement?.HiddenItems.Count > 0,
                    () => ApplyAsync(_layout.Places(name)
                        ? PivotLayoutEdits.Untick(_layout, name)
                        : PivotLayoutEdits.Tick(_layout, InfoOf(name))));
            })
            .ToArray();

        var areas = AreaOrder.Select(area =>
        {
            var entries = new List<PivotAreaEntryView>();
            if (area == PivotArea.Values)
            {
                for (var i = 0; i < _layout.Values.Count; i++)
                    entries.Add(EntryView(new PivotEntry(area, i), _report?.ValueCaptions.ElementAtOrDefault(i) ?? _layout.Values[i].Field, false));
            }
            else
            {
                var placements = _layout.PlacementsIn(area);
                for (var i = 0; i < placements.Count; i++)
                    entries.Add(EntryView(new PivotEntry(area, i), InfoOf(placements[i].Field).Caption, placements[i].HiddenItems.Count > 0));
                if (_layout.Values.Count >= 2 && area == (_layout.ValuesAxis == PivotAxis.Rows ? PivotArea.Rows : PivotArea.Columns))
                    entries.Add(EntryView(PivotEntry.ValuesPseudoField(_layout.ValuesAxis), Word(PivotWords.ValuesPseudoField), false));
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
            _drag,
            StartDrag,
            DragOver,
            DropAsync,
            _drag?.Entry is not null,
            DropOnListAsync,
            EndDrag,
            Word);
    }

    private PivotAreaEntryView EntryView(PivotEntry entry, string caption, bool filtered)
    {
        var open = _open is { OnBand: false } surface && surface.Entry == entry;
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

    // ---- Drag and drop (ADR-0060): Blazor's own events, what is dragged held here -----------

    /// <summary>Whether a drop of what is dragged on <paramref name="area"/> would change anything:
    /// Σ Values goes only to Rows and Columns.</summary>
    private bool Accepts(PivotArea area)
        => _drag is { } drag && (drag.Entry is not { IsValuesPseudoField: true } || area is PivotArea.Rows or PivotArea.Columns);

    private void StartDrag(PivotDragSubject subject)
    {
        _drag = subject;
        _dropAt = null;
        CloseOpenQuietly();
        StateHasChanged();
    }

    private void DragOver(PivotArea area, int index)
    {
        if (_drag is null || _dropAt == (area, index))
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

    private Task DropAsync(PivotArea area, int index)
    {
        var drag = _drag;
        _drag = null;
        _dropAt = null;
        if (drag is null)
        {
            StateHasChanged();
            return Task.CompletedTask;
        }
        if (drag.Field is { } field)
            return ApplyAsync(PivotLayoutEdits.Place(_layout, InfoOf(field), area, index));
        var entry = drag.Entry!.Value;
        return ApplyAsync(PivotLayoutEdits.Move(_layout, entry, area, index, entry.IsValuesPseudoField ? null : InfoOf(FieldOf(entry))));
    }

    private Task DropOnListAsync()
    {
        var drag = _drag;
        _drag = null;
        _dropAt = null;
        if (drag?.Entry is { } entry)
            return ApplyAsync(PivotLayoutEdits.Remove(_layout, entry));
        StateHasChanged();
        return Task.CompletedTask;
    }

    private string FieldOf(PivotEntry entry)
        => entry.Area == PivotArea.Values ? _layout.Values[entry.Index].Field : _layout.PlacementsIn(entry.Area)[entry.Index].Field;

    // ---- A placed field's menu (ADR-0060) ---------------------------------------------------

    private IReadOnlyList<PivotCommand> MenuFor(PivotEntry entry)
    {
        var commands = new List<PivotCommand>();
        void Add(string id, bool enabled, Func<Task> run)
            => commands.Add(new PivotCommand(id, Word(id), enabled, () =>
            {
                CloseOpen();
                return run();
            }));
        Task Move(PivotArea area, int index)
            => ApplyAsync(PivotLayoutEdits.Move(_layout, entry, area, index, entry.IsValuesPseudoField ? null : InfoOf(FieldOf(entry))));

        if (entry.IsValuesPseudoField)
        {
            var onRows = _layout.ValuesAxis == PivotAxis.Rows;
            Add(PivotCommandIds.MoveToRows, !onRows, () => Move(PivotArea.Rows, int.MaxValue));
            Add(PivotCommandIds.MoveToColumns, onRows, () => Move(PivotArea.Columns, int.MaxValue));
            return commands;
        }

        var count = entry.Area == PivotArea.Values ? _layout.Values.Count : _layout.PlacementsIn(entry.Area).Count;
        Add(PivotCommandIds.MoveUp, entry.Index > 0, () => Move(entry.Area, entry.Index - 1));
        Add(PivotCommandIds.MoveDown, entry.Index < count - 1, () => Move(entry.Area, entry.Index + 2));
        Add(PivotCommandIds.MoveToBeginning, entry.Index > 0, () => Move(entry.Area, 0));
        Add(PivotCommandIds.MoveToEnd, entry.Index < count - 1, () => Move(entry.Area, count));
        Add(PivotCommandIds.MoveToFilters, entry.Area != PivotArea.Filters, () => Move(PivotArea.Filters, int.MaxValue));
        Add(PivotCommandIds.MoveToRows, entry.Area != PivotArea.Rows, () => Move(PivotArea.Rows, int.MaxValue));
        Add(PivotCommandIds.MoveToColumns, entry.Area != PivotArea.Columns, () => Move(PivotArea.Columns, int.MaxValue));
        Add(PivotCommandIds.MoveToValues, entry.Area != PivotArea.Values, () => Move(PivotArea.Values, int.MaxValue));
        Add(PivotCommandIds.RemoveField, true, () => ApplyAsync(PivotLayoutEdits.Remove(_layout, entry)));

        if (entry.Area == PivotArea.Values)
        {
            Add(PivotCommandIds.ValueFieldSettings, true, () => OpenPanelAsync(entry, Surface.ValueFieldSettings));
            return commands;
        }
        var placement = _layout.PlacementsIn(entry.Area)[entry.Index];
        if (entry.Area is PivotArea.Rows or PivotArea.Columns)
        {
            var field = placement.Field;
            Add(PivotCommandIds.SortAscending, placement.Sort != PivotSort.Ascending,
                () => ApplyAsync(PivotLayoutEdits.SetSort(_layout, field, PivotSort.Ascending)));
            Add(PivotCommandIds.SortDescending, placement.Sort != PivotSort.Descending,
                () => ApplyAsync(PivotLayoutEdits.SetSort(_layout, field, PivotSort.Descending)));
            Add(PivotCommandIds.FilterItems, true, () => OpenPanelAsync(entry, Surface.ItemFilter));
            // The innermost field has nothing under its Items to collapse (ADR-0059).
            var outer = entry.Index < count - 1;
            Add(PivotCommandIds.ExpandField, outer && (placement.Collapsed || placement.ToggledItems.Count > 0),
                () => ApplyAsync(PivotLayoutEdits.SetFieldCollapsed(_layout, field, false)));
            Add(PivotCommandIds.CollapseField, outer && (!placement.Collapsed || placement.ToggledItems.Count > 0),
                () => ApplyAsync(PivotLayoutEdits.SetFieldCollapsed(_layout, field, true)));
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
        builder.AddComponentParameter(1, nameof(PivotPopupFrame.Role), open.Kind == Surface.Menu ? "menu" : "dialog");
        builder.AddComponentParameter(2, nameof(PivotPopupFrame.Label), TitleOf(open));
        builder.AddComponentParameter(3, nameof(PivotPopupFrame.Overlay), open.OnBand);
        builder.AddComponentParameter(4, nameof(PivotPopupFrame.OnEscape), _escape);
        builder.AddComponentParameter(5, nameof(PivotPopupFrame.ChildContent), Content(open));
        builder.CloseComponent();
    };

    private string TitleOf(OpenSurface open) => open.Kind switch
    {
        Surface.Menu => PivotWords.Fill(Word("field-menu"), EntryCaption(open.Entry!.Value)),
        Surface.ItemFilter => PivotWords.Fill(Word("filter-of"), InfoOf(open.Field).Caption),
        Surface.FieldSettings => Word(PivotCommandIds.FieldSettings),
        _ => Word(PivotCommandIds.ValueFieldSettings),
    };

    private string EntryCaption(PivotEntry entry)
        => entry.IsValuesPseudoField ? Word(PivotWords.ValuesPseudoField)
            : entry.Area == PivotArea.Values ? _report?.ValueCaptions.ElementAtOrDefault(entry.Index) ?? FieldOf(entry)
            : InfoOf(FieldOf(entry)).Caption;

    private RenderFragment Content(OpenSurface open)
    {
        switch (open.Kind)
        {
            case Surface.Menu:
            {
                var context = new PivotMenuContext(TitleOf(open), MenuFor(open.Entry!.Value), CloseOpen, open.FocusRequest);
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

    // Filter… (ADR-0060): the draft of ticks, the search that narrows the list, and OK.
    private PivotItemFilterContext ItemFilterContext(OpenSurface open)
    {
        var search = open.ItemSearch;
        var matches = search.Length == 0
            ? open.Items
            : open.Items.Where(i => i.Label.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        var listed = matches.Take(ItemListCap).Select(i => new PivotItemChoice(i.Key, i.Label, !open.Hidden.Contains(i.Key))).ToArray();
        var ticked = matches.Count(i => !open.Hidden.Contains(i.Key));
        bool? all = ticked == matches.Count ? true : ticked == 0 ? false : null;
        var canApply = open.Items.Count == 0 || open.Items.Any(i => !open.Hidden.Contains(i.Key));
        return new PivotItemFilterContext(
            InfoOf(open.Field).Caption,
            listed,
            open.Items.Count,
            matches.Count,
            ItemListCap,
            search,
            text =>
            {
                open.ItemSearch = text ?? "";
                StateHasChanged();
            },
            all,
            tick =>
            {
                foreach (var item in matches)
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
            open.Refusal ?? (canApply ? null : Word("refused-hides-every-item")),
            () =>
            {
                var result = PivotLayoutEdits.SetHiddenItems(_layout, open.Field, open.Hidden, open.Items.Select(i => i.Key).ToArray());
                if (result.IsRefused)
                {
                    open.Refusal = Word(result.RefusalWord!);
                    StateHasChanged();
                    return Task.CompletedTask;
                }
                CloseOpen();
                return ApplyAsync(result.Layout);
            },
            CloseOpen,
            open.FocusRequest,
            Word);
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
                var layout = PivotLayoutEdits.SetSubtotals(_layout, open.Field, open.Subtotals);
                layout = PivotLayoutEdits.SetSort(layout, open.Field, open.Sorts[open.Sort].Value);
                CloseOpen();
                return ApplyAsync(layout);
            },
            CloseOpen,
            open.FocusRequest,
            SetInnerPopup,
            Word);

    private PivotValueFieldSettingsContext ValueFieldSettingsContext(OpenSurface open)
    {
        var percent = open.ShowValuesAs != PivotShowValuesAs.NoCalculation;
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
                // A caption the user has not written follows the Aggregation, as Excel's Custom
                // Name box does.
                if (!open.CaptionTouched && _layout.Values[open.ValueIndex].Caption is null)
                    open.Caption = DefaultCaption(aggregation, open.Field);
                open.Aggregation = aggregation;
                open.Refusal = null;
                StateHasChanged();
            },
            Enum.GetValues<PivotAggregation>().Select(a => new PivotChoice<PivotAggregation>(a, Word(PivotWords.AggregationName(a)))).ToArray(),
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
                var current = _layout.Values[open.ValueIndex];
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
                var result = PivotLayoutEdits.SetValueField(_layout, open.ValueIndex, value, Infos);
                if (result.IsRefused)
                {
                    open.Refusal = Word(result.RefusalWord!);
                    StateHasChanged();
                    return Task.CompletedTask;
                }
                CloseOpen();
                return ApplyAsync(result.Layout);
            },
            CloseOpen,
            open.FocusRequest,
            SetInnerPopup,
            Word);
    }

    /// <summary>A number shown in the draft's format — or the reason it cannot be — so a format is
    /// seen before it is applied (ADR-0060).</summary>
    private string Sample(string format, bool percent)
    {
        var trimmed = format.Trim();
        if (trimmed.Length > 0 && PivotNumberFormat.Check(trimmed) is not null)
            return Word("refused-number-format");
        var sample = percent ? 0.1234 : -1234.5678;
        var text = sample.ToString(trimmed.Length == 0 ? (percent ? "0.00%" : "G15") : trimmed, _culture);
        return PivotWords.Fill(Word("sample"), text);
    }

    // ---- The report filter band (ADR-0060) ----------------------------------------------------

    private RenderFragment ReportFilters() => builder =>
    {
        var filters = _layout.Filters.Select(placement =>
        {
            var field = placement.Field;
            var items = ItemsOf(field);
            var shown = items.Where(i => !i.IsHidden).ToArray();
            var summary = shown.Length == items.Count ? Word(PivotWords.All)
                : shown.Length == 1 ? shown[0].Label
                : Word(PivotWords.MultipleItems);
            var open = _open is { OnBand: true } surface && surface.BandField == field;
            return new PivotReportFilterView(field, InfoOf(field).Caption, summary, shown.Length < items.Count, open,
                () => ToggleBand(field), open ? PopupFragment(_open!) : null, _bandFocus.GetValueOrDefault(field));
        }).ToArray();
        var context = new PivotReportFiltersContext(Word("report-filters"), filters, Word);
        if (PivotChrome?.ReportFilters(context) is { } custom)
        {
            builder.AddContent(0, custom);
            return;
        }
        builder.OpenComponent<PivotReportFiltersView>(1);
        builder.AddComponentParameter(2, nameof(PivotReportFiltersView.Context), context);
        builder.CloseComponent();
    };
}
