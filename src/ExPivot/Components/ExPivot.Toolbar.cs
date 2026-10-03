using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Components;

// The Pivot Toolbar above the report (ADR-0061): the report filter band on its left; Layout ▾,
// Refresh and the Field List's toggle on its right. Its popups open under it, over the report, with
// a backdrop that closes them.
public partial class ExPivot
{
    // The Layout menu's choices in Excel's order, each group headed where it starts.
    private static readonly (PivotLayoutChoice Choice, string? Heading)[] LayoutChoices =
    [
        (PivotLayoutChoice.DoNotShowSubtotals, "subtotals"),
        (PivotLayoutChoice.ShowSubtotalsAtBottom, null),
        (PivotLayoutChoice.ShowSubtotalsAtTop, null),
        (PivotLayoutChoice.GrandTotalsOff, "grand-totals"),
        (PivotLayoutChoice.GrandTotalsOn, null),
        (PivotLayoutChoice.GrandTotalsOnRowsOnly, null),
        (PivotLayoutChoice.GrandTotalsOnColumnsOnly, null),
        (PivotLayoutChoice.CompactForm, "report-layout"),
        (PivotLayoutChoice.OutlineForm, null),
        (PivotLayoutChoice.TabularForm, null),
        (PivotLayoutChoice.RepeatItemLabels, null),
        (PivotLayoutChoice.DoNotRepeatItemLabels, null),
    ];

    private int _layoutMenuFocus;

    private RenderFragment Toolbar() => builder =>
    {
        var context = ToolbarContext();
        if (PivotChrome?.Toolbar(context) is { } custom)
        {
            builder.AddContent(0, custom);
            return;
        }
        builder.OpenComponent<PivotToolbarView>(1);
        builder.AddComponentParameter(2, nameof(PivotToolbarView.Context), context);
        builder.CloseComponent();
    };

    private PivotToolbarContext ToolbarContext()
    {
        var layoutOpen = _open is { Kind: Surface.LayoutMenu };
        var layoutMenu = new PivotToolbarMenuView(
            PivotCommandIds.LayoutMenu, Word(PivotCommandIds.LayoutMenu), layoutOpen, ToggleLayoutMenu,
            layoutOpen ? PopupFragment(_open!) : null, _layoutMenuFocus);
        // Refresh is offered only by a source that can be asked again (ADR-0066).
        var refresh = Source.Features.CanRefresh
            ? new PivotCommand(PivotCommandIds.Refresh, Word(PivotCommandIds.Refresh), true, RefreshAsync)
            : null;
        var fieldList = new PivotCommand(PivotCommandIds.FieldListToggle, Word(PivotCommandIds.FieldListToggle), true,
            () => SetFieldListShownAsync(!_fieldListShown)) { Checked = _fieldListShown };
        return new PivotToolbarContext(_layout.Filters.Count > 0 ? ReportFilters() : null, layoutMenu, refresh, fieldList, _refusal, Word);
    }

    private void ToggleLayoutMenu()
    {
        if (_open is { Kind: Surface.LayoutMenu })
        {
            CloseOpen();
            return;
        }
        Open(new OpenSurface { Kind = Surface.LayoutMenu, FocusRequest = ++_focusSequence });
    }

    /// <summary>The Layout menu's commands (ADR-0061): Excel's Design tab choices, the current one
    /// of each group marked, and one that would change nothing disabled. A choice is the report's:
    /// it reaches a pending layout too.</summary>
    private IReadOnlyList<PivotCommand> LayoutMenuCommands()
    {
        var layout = _layout;
        var commands = new List<PivotCommand>(LayoutChoices.Length);
        foreach (var (choice, heading) in LayoutChoices)
        {
            var id = PivotCommandIds.LayoutChoice(choice);
            commands.Add(new PivotCommand(id, Word(id), PivotLayoutEdits.Changes(layout, choice), () =>
            {
                CloseOpen();
                return ReportEditAsync(l => PivotLayoutEdits.Choose(l, choice));
            })
            {
                Checked = PivotLayoutEdits.IsChosen(layout, choice),
                GroupHeading = heading is null ? null : Word(heading),
            });
        }
        return commands;
    }

    // ---- The report filter band, on the Pivot Toolbar's left (ADR-0061) -------------------------

    private RenderFragment ReportFilters() => builder =>
    {
        var filters = _layout.Filters.Select(placement =>
        {
            var field = placement.Field;
            var open = _open is { BandField: not null } surface && surface.BandField == field;
            return new PivotReportFilterView(field, InfoOf(field).Caption, BandSummary(placement), placement.HiddenItems.Count > 0, open,
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

    /// <summary>What a report filter shows, as Excel shows a page field: <c>(All)</c> while it hides
    /// nothing, the one Item shown, or <c>(Multiple Items)</c> — read from the field's Items, asked
    /// of the source; "Loading…" until a first listing arrives, rather than a guess, and why when
    /// they cannot be listed. While a new Source Version's Items are on their way, the summary is
    /// read from the Items in view, the earlier version's (ADR-0066 refined).</summary>
    private string BandSummary(PivotFieldPlacement placement)
    {
        if (placement.HiddenItems.Count == 0)
            return Word(PivotWords.All);
        var items = ItemsOf(placement.Field);
        if (items.Problem is { } problem)
            return ProblemText(problem);
        if (items.Page is not { } page || FieldOf(placement.Field) is not { } field)
            return Word("loading");
        if (page.Total > page.Items.Count)
        {
            // More Items than are held: provably several shown, or not known.
            return page.Total - placement.HiddenItems.Count > 1 ? Word(PivotWords.MultipleItems) : Word("loading");
        }
        var shown = PivotEngine.ItemsOf(page, _layout, field, _options).Where(i => !i.IsHidden).Take(2).ToArray();
        return shown.Length == 1 ? shown[0].Label : Word(PivotWords.MultipleItems);
    }
}
