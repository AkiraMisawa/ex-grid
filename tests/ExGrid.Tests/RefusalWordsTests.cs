using ExGrid.Chrome;
using ExGrid.Clipboard;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The built-in Chrome's words for every refusal of a paste or a copy (ADR-0203, ERR-2): each
/// reason has an id of its own, each id is worded, and where a next step exists the words say it.
/// What reaches the live region, and how often, is layer 2's.
/// </summary>
public class RefusalWordsTests
{
    public static TheoryData<PasteRefusalReason> PasteReasons() => new(Enum.GetValues<PasteRefusalReason>());

    public static TheoryData<CopyRefusalReason> CopyReasons() => new(Enum.GetValues<CopyRefusalReason>());

    private static IEnumerable<string> EveryId()
        => Enum.GetValues<PasteRefusalReason>().Select(RefusalLabelIds.For)
            .Concat(Enum.GetValues<CopyRefusalReason>().Select(RefusalLabelIds.For));

    [Fact] // ADR-0203 / ERR-2: one id per reason, paste and copy, none shared and none a command's
    public void Every_refusal_reason_has_an_id_of_its_own()
    {
        var ids = EveryId().ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(ids, id => typeof(GridCommandIds).GetFields().Any(f => Equals(f.GetValue(null), id)));
    }

    [Theory] // ADR-0203 / ERR-2: every paste refusal reason is worded by the built-in Chrome, as a sentence and not its id
    [MemberData(nameof(PasteReasons))]
    public void Every_paste_refusal_reason_is_worded(PasteRefusalReason reason)
    {
        var id = RefusalLabelIds.For(reason);
        var words = BuiltInCommandLabels.For(id);

        Assert.NotEqual(id, words);
        Assert.False(string.IsNullOrWhiteSpace(words));
        Assert.EndsWith(".", words);
    }

    [Theory] // ADR-0203 / ERR-2: every copy refusal reason is worded by the built-in Chrome, as a sentence and not its id
    [MemberData(nameof(CopyReasons))]
    public void Every_copy_refusal_reason_is_worded(CopyRefusalReason reason)
    {
        var id = RefusalLabelIds.For(reason);
        var words = BuiltInCommandLabels.For(id);

        Assert.NotEqual(id, words);
        Assert.False(string.IsNullOrWhiteSpace(words));
        Assert.EndsWith(".", words);
        Assert.StartsWith("Nothing was copied", words);
    }

    [Theory] // ADR-0203 / ERR-2, ADR-0014: a reason with a next step says it — the target to select instead
    [InlineData(PasteRefusalReason.EmptySelection, "Select the cells")]
    [InlineData(PasteRefusalReason.SingleCellTarget, "Select a range of the copied block's shape.")]
    [InlineData(PasteRefusalReason.ShapeMismatch, "Select a range of the copied block's shape, or one it repeats over exactly.")]
    [InlineData(PasteRefusalReason.DisjointTarget, "Select one range.")]
    [InlineData(PasteRefusalReason.SpillPastExtent, "Select a cell further from the edge.")]
    [InlineData(PasteRefusalReason.NothingToFillFrom, "Select the cells to fill from together with the cells to fill.")]
    [InlineData(PasteRefusalReason.MultipleRanges, "Select one range.")]
    [InlineData(PasteRefusalReason.TooLarge, "Use a smaller block.")]
    [InlineData(PasteRefusalReason.OrderMoved, "Select the cells again.")]
    [InlineData(PasteRefusalReason.SourceChanged, "Select the cells again.")]
    public void A_paste_refusal_with_a_next_step_says_it(PasteRefusalReason reason, string nextStep)
        => Assert.Contains(nextStep, BuiltInCommandLabels.For(RefusalLabelIds.For(reason)));

    [Theory] // ADR-0203 / ERR-2: a copy refusal with a next step says it
    [InlineData(CopyRefusalReason.EmptySelection, "Select the cells to copy.")]
    [InlineData(CopyRefusalReason.MisalignedShape, "Select ranges that share their rows or their columns.")]
    [InlineData(CopyRefusalReason.TooLarge, "Select fewer cells.")]
    public void A_copy_refusal_with_a_next_step_says_it(CopyRefusalReason reason, string nextStep)
        => Assert.Contains(nextStep, BuiltInCommandLabels.For(RefusalLabelIds.For(reason)));

    [Fact] // ADR-0203 / ADR-0035: a target that is not editable names the column that refused, and advises no reselection — none would be accepted
    public void A_target_that_is_not_editable_names_the_column_and_advises_no_reselection()
    {
        var words = BuiltInCommandLabels.For(RefusalLabelIds.PasteTargetNotEditable);

        Assert.Contains(RefusalLabelIds.ColumnPlaceholder, words);
        Assert.DoesNotContain("Select", words);
        Assert.DoesNotContain("shape", words);
    }

    [Fact] // ADR-0203: only the words for a target that is not editable name a column
    public void Only_the_editability_refusal_holds_the_column_placeholder()
    {
        var naming = EveryId().Where(id => BuiltInCommandLabels.For(id).Contains(RefusalLabelIds.ColumnPlaceholder)).ToList();

        Assert.Equal([RefusalLabelIds.PasteTargetNotEditable], naming);
    }

    [Theory] // ADR-0203: the words a refusal is said in are keyed by its reason, the same id from either way of asking
    [InlineData(PasteRefusalReason.ShapeMismatch, RefusalLabelIds.PasteShapeMismatch)]
    [InlineData(PasteRefusalReason.TargetNotEditable, RefusalLabelIds.PasteTargetNotEditable)]
    [InlineData(PasteRefusalReason.OrderMoved, RefusalLabelIds.PasteOrderMoved)]
    public void A_paste_reason_names_its_id(PasteRefusalReason reason, string id)
        => Assert.Equal(id, RefusalLabelIds.For(reason));
}
