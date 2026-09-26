using ExGrid.Chrome;
using MudBlazor;

namespace ExGrid.MudBlazor;

/// <summary>
/// The words the Wrapper's menus and filter panel show (ADR-0030, WR-3): MudBlazor's own
/// localised text wherever MudBlazor has a key — one registered <c>MudLocalizer</c>
/// translates the grid's words with the rest of the app's — and the Chrome's
/// <see cref="MudGridChrome.Label"/> for the rest, English where that says nothing. The
/// ids below are what <see cref="MudGridChrome.Label"/> is asked with, beside the core's
/// command ids.
/// </summary>
public static class MudExGridWords
{
    /// <summary>The value list's search field.</summary>
    public const string Search = "search";

    /// <summary>The value list's entry for a Blank cell — never "empty" (CONTEXT.md
    /// <b>Blank</b>): an empty string is a value, and Blank is its absence.</summary>
    public const string BlankValue = "blank-value";

    /// <summary>The <c>In</c> operator in the condition form, for which MudBlazor has no word.</summary>
    public const string IsOneOf = "operator-in";

    /// <summary>The <c>IsBlank</c> operator — MudBlazor's own word for it says "empty",
    /// which this grid never does.</summary>
    public const string IsBlank = "operator-is-blank";

    /// <summary>The <c>IsNotBlank</c> operator, for the same reason.</summary>
    public const string IsNotBlank = "operator-is-not-blank";

    /// <summary>The value list's "(Select All)" (ADR-0009).</summary>
    public const string SelectAll = "select-all";

    /// <summary>"(Select All)" while a search stands: it acts on the matching values only.</summary>
    public const string SelectAllSearchResults = "select-all-search-results";

    /// <summary>Excel's "Add current selection to filter" (ADR-0009).</summary>
    public const string AddToFilter = "add-to-filter";

    /// <summary>The AND joining a second condition to the first (ADR-0009).</summary>
    public const string JoinAnd = "join-and";

    /// <summary>The OR joining a second condition to the first.</summary>
    public const string JoinOr = "join-or";

    /// <summary>The second condition's operator left unset: no second condition.</summary>
    public const string NoSecondCondition = "no-second-condition";

    /// <summary>The second condition's operator select — named apart from the first's
    /// "Operator", so the two are not one name twice to a screen reader.</summary>
    public const string SecondCondition = "second-condition";

    private static string English(string id) => id switch
    {
        Search => "Search",
        BlankValue => "(Blanks)",
        IsOneOf => "is one of",
        IsBlank => "is blank",
        IsNotBlank => "is not blank",
        SelectAll => "(Select All)",
        SelectAllSearchResults => "(Select All Search Results)",
        AddToFilter => "Add current selection to filter",
        JoinAnd => "And",
        JoinOr => "Or",
        NoSecondCondition => "(none)",
        SecondCondition => "Second condition",
        _ => BuiltInCommandLabels.For(id),
    };

    /// <summary>MudBlazor's key for a command's word, where it has one.</summary>
    internal static string? MudKeyForCommand(string id) => id switch
    {
        GridCommandIds.ClearFilter => "MudDataGrid_ClearFilter",
        GridCommandIds.Hide => "MudDataGrid_Hide",
        _ => null,
    };

    /// <summary>MudBlazor's key for an operator's word on a column of this type, where it
    /// has one: its data grid's own words — symbols for numbers, before and after for
    /// dates. Blank and In have none the grid may use.</summary>
    internal static string? MudKeyForOperator(FilterOperator op, ColumnType type) => (type, op) switch
    {
        (_, FilterOperator.In or FilterOperator.IsBlank or FilterOperator.IsNotBlank) => null,
        (ColumnType.Number, FilterOperator.Equals) => "MudDataGrid_EqualSign",
        (ColumnType.Number, FilterOperator.NotEquals) => "MudDataGrid_NotEqualSign",
        (ColumnType.Number, FilterOperator.GreaterThan) => "MudDataGrid_GreaterThanSign",
        (ColumnType.Number, FilterOperator.GreaterThanOrEqual) => "MudDataGrid_GreaterThanOrEqualSign",
        (ColumnType.Number, FilterOperator.LessThan) => "MudDataGrid_LessThanSign",
        (ColumnType.Number, FilterOperator.LessThanOrEqual) => "MudDataGrid_LessThanOrEqualSign",
        (ColumnType.Date or ColumnType.Boolean, FilterOperator.Equals) => "MudDataGrid_Is",
        (ColumnType.Date, FilterOperator.NotEquals) => "MudDataGrid_IsNot",
        (ColumnType.Date, FilterOperator.GreaterThan) => "MudDataGrid_IsAfter",
        (ColumnType.Date, FilterOperator.GreaterThanOrEqual) => "MudDataGrid_IsOnOrAfter",
        (ColumnType.Date, FilterOperator.LessThan) => "MudDataGrid_IsBefore",
        (ColumnType.Date, FilterOperator.LessThanOrEqual) => "MudDataGrid_IsOnOrBefore",
        (_, FilterOperator.Equals) => "MudDataGrid_Equals",
        (_, FilterOperator.NotEquals) => "MudDataGrid_NotEquals",
        (_, FilterOperator.Contains) => "MudDataGrid_Contains",
        (_, FilterOperator.DoesNotContain) => "MudDataGrid_NotContains",
        (_, FilterOperator.StartsWith) => "MudDataGrid_StartsWith",
        (_, FilterOperator.EndsWith) => "MudDataGrid_EndsWith",
        _ => null,
    };

    /// <summary>The Chrome's own word for an operator MudBlazor has none for.</summary>
    internal static string OwnIdForOperator(FilterOperator op) => op switch
    {
        FilterOperator.In => IsOneOf,
        FilterOperator.IsBlank => IsBlank,
        FilterOperator.IsNotBlank => IsNotBlank,
        _ => op.ToString(),
    };

    /// <summary>MudBlazor's word for a key, as its localiser answers it.</summary>
    internal static string Mud(ILocalizationInterceptor localizer, string key) => localizer.Handle(key).Value;

    /// <summary>The Chrome's own word: its <see cref="MudGridChrome.Label"/>, English
    /// where that says nothing.</summary>
    internal static string Own(MudGridChrome chrome, string id) => chrome.Label?.Invoke(id) ?? English(id);
}
