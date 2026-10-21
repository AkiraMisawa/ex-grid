using ExGrid.Chrome;
using ExGrid.Finding;

namespace ExSheet;

/// <summary>Where Find and Replace search on a Sheet (ADR-0331 §8, ADR-0332 §5), as Excel's dialog's
/// Within offers it.</summary>
public enum SheetFindWithin
{
    /// <summary>The Sheet shown: the default, as in Excel. With more than one cell selected, the Selection
    /// alone, as ExGrid's Find searches it (ADR-0055).</summary>
    Sheet,

    /// <summary>Every Sheet of the Workbook, in tab order: a step past the last match of the Sheet shown goes
    /// on to the next Sheet, and a match on another Sheet shows that Sheet with the cell selected. The
    /// Selection is not a scope here: every cell of every Sheet is searched.</summary>
    Workbook,
}

/// <summary>
/// The find panel ExSheet draws in place of the grid's (ADR-0332 §5), for a Chrome that draws it
/// (<see cref="ISheetChrome.SheetFindPanel"/>). It is the grid's panel with Within added: <see cref="Find"/>
/// is the grid's own contract (ADR-0055, ADR-0332 §2) — the field, the two options, the steps, the Replace
/// part, the focus requests and the close — and the Chrome draws it as it draws the grid's, adding a control
/// for <see cref="Within"/>. What a step finds, what Replace writes and what the panel says are ExSheet's,
/// the same under every Chrome.
/// </summary>
/// <param name="Find">The grid's find panel contract. Its <see cref="FindContext.ReplaceAll"/> is ExSheet's:
/// over the Workbook ExSheet performs it itself, as one step on its undo stack. Draw its outcome with
/// <see cref="OutcomeText"/>, not from <see cref="FindContext.Outcome"/>, which knows nothing of a Replace
/// All over the Workbook.</param>
/// <param name="Within">Where the panel searches as it opens: the Sheet shown, or the Workbook. ExSheet keeps
/// it across a close and reopen, as the grid keeps the find text.</param>
/// <param name="WithinChanged">The Chrome reports the user's choice of Within; the next step searches there.
/// The Chrome shows the choice itself from then on: the context is not made again for it.</param>
/// <param name="OutcomeText">What the last step or Replace came to, as one sentence, or the empty string for
/// nothing to say: asked when the Chrome draws, so it is current after a Replace All the Chrome awaited.
/// Written into a live region, as the grid holds no sentence (ADR-0033).</param>
/// <param name="Outcome">What the last step or Replace came to, as <see cref="OutcomeText"/> says it: for a Chrome
/// that colours the sentence by its kind. Asked when the Chrome draws.</param>
/// <param name="Words">The words for an id of <see cref="FindPanelLabelIds"/> or <see cref="SheetFindLabelIds"/>:
/// the Chrome's own (<see cref="IGridChrome.Words"/>), else <c>ExSheet.CommandLabel</c>, else ExSheet's and the
/// grid's English.</param>
public sealed record SheetFindContext(
    FindContext Find,
    SheetFindWithin Within,
    Action<SheetFindWithin> WithinChanged,
    Func<string> OutcomeText,
    Func<FindOutcome> Outcome,
    Func<string, string> Words);

/// <summary>
/// The words ExSheet's find panel adds to the grid's (<see cref="FindPanelLabelIds"/>), by id: Within and its
/// two choices (ADR-0332 §5). A Consumer translates them through <c>ExSheet.CommandLabel</c>, or a Chrome
/// through its own words, as it translates the grid's.
/// </summary>
public static class SheetFindLabelIds
{
    /// <summary>The label of the Within choice.</summary>
    public const string Within = "exsheet.find-within";

    /// <summary>Within: the Sheet shown.</summary>
    public const string WithinSheet = "exsheet.find-within-sheet";

    /// <summary>Within: the Workbook.</summary>
    public const string WithinWorkbook = "exsheet.find-within-workbook";

    /// <summary>ExSheet's English for one of these ids, as Excel's dialog words them; null for any other id.</summary>
    /// <param name="id">The id.</param>
    public static string? EnglishFor(string id) => id switch
    {
        Within => "Within",
        WithinSheet => "Sheet",
        WithinWorkbook => "Workbook",
        _ => null,
    };
}
