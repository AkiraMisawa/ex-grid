using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Data;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// What a refused Copy and a declined Selection Summary say (ADR-0060, ADR-0152): every sentence
/// ExPivot writes is a word of its table, replaceable by id, with the bundled Japanese beside the
/// English. A source's own sentence is said inside ExPivot's <c>source-refused</c>, as a refused
/// question's is. The source here answers a Copy and a Summary as the test says, as a Consumer's own
/// report source may: under another Report Version, a selected range left out or cut short, error
/// counts that disagree, or refused in its own words.
/// </summary>
public sealed class ReportOperationWordsTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    // A Latin letter in a sentence the Japanese words say is an English word left.
    private static readonly Regex Latin = new(@"[A-Za-z]+", RegexOptions.CultureInvariant);

    // What the source says in its own words: Japanese, so that a sentence with it inside is all
    // Japanese too.
    private const string OwnWords = "サーバーが混み合っています";

    /// <summary>How the source misanswers.</summary>
    public enum Misanswer
    {
        /// <summary>It answers under another Report Version than the one asked.</summary>
        AnotherVersion,

        /// <summary>It leaves a selected range out.</summary>
        RangeLeftOut,

        /// <summary>It cuts a selected range short.</summary>
        RangeCutShort,

        /// <summary>It says an error was selected and counts no value.</summary>
        ErrorCountsDisagree,

        /// <summary>It refuses, in its own words.</summary>
        RefusedInItsOwnWords,
    }

    /// <summary>
    /// A report source that answers as the bundled one does, except a Copy and a Selection Summary,
    /// which it answers as the test says.
    /// </summary>
    private sealed class MisansweringSource(PivotSource data, Misanswer misanswer) : PivotReportSource
    {
        private readonly LocalPivotReportSource _inner = From(data);

        public override IReadOnlyList<PivotField> Fields => _inner.Fields;

        public override PivotSourceFeatures Features => _inner.Features;

        public override PivotReportUpdateMode UpdateMode => _inner.UpdateMode;

        public override ValueTask<PivotReportUpdate> WindowAsync(PivotReportRequest request, CancellationToken cancellationToken = default)
            => _inner.WindowAsync(request, cancellationToken);

        public override ValueTask<PivotItemPage> RawItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
            => _inner.RawItemsAsync(query, cancellationToken);

        public override ValueTask<PivotReportItemsResult> ItemsAsync(PivotReportItemsQuery query, CancellationToken cancellationToken = default)
            => _inner.ItemsAsync(query, cancellationToken);

        public override async ValueTask<PivotReportCopyResult> CopyAsync(PivotReportCopyQuery query, CancellationToken cancellationToken = default)
        {
            var answer = await _inner.CopyAsync(query, cancellationToken);
            return misanswer switch
            {
                Misanswer.AnotherVersion => answer with { Version = new("another") },
                Misanswer.RangeLeftOut => answer with { Blocks = [] },
                Misanswer.RangeCutShort => answer with { Blocks = [answer.Blocks[0] with { Rows = [.. answer.Blocks[0].Rows.Take(1)] }] },
                Misanswer.RefusedInItsOwnWords => new(query.Version, [], new(PivotReportRefusalKind.SourceRefused, OwnWords)),
                _ => answer,
            };
        }

        public override async ValueTask<PivotReportSummaryResult> SummaryAsync(PivotReportSummaryQuery query, CancellationToken cancellationToken = default)
        {
            var answer = await _inner.SummaryAsync(query, cancellationToken);
            return misanswer switch
            {
                Misanswer.AnotherVersion => answer with { Version = new("another") },
                Misanswer.ErrorCountsDisagree => answer with { HasError = true, Counts = default(AggregateCounts) },
                Misanswer.RefusedInItsOwnWords => new(query.Version, default, default, default, false, null, "",
                    new(PivotReportRefusalKind.SourceRefused, OwnWords)),
                _ => answer,
            };
        }

        public override ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
            => _inner.DetailsAsync(query, cancellationToken);

        public override ValueTask RefreshAsync(CancellationToken cancellationToken = default) => _inner.RefreshAsync(cancellationToken);

        public override ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    /// <summary>The sentence a word says under the Japanese words: the bundled Japanese for
    /// <paramref name="id"/>, which must be there, with <paramref name="arguments"/> put in.</summary>
    private static string Japanese(string id, params string[] arguments)
    {
        var word = PivotWords.JapaneseFor(id);
        Assert.True(word is not null, $"'{id}' has no word of the Japanese edition");
        Assert.True(PivotWords.EnglishFor(id) is not null, $"'{id}' has no English word");
        return PivotWords.Fill(word!, arguments);
    }

    private IRenderedComponent<PivotComponent> RenderJapanese(MisansweringSource source)
        => RenderPivot(RegionAmount, ps => ps.Add(p => p.Label, PivotWords.Japanese), reportSource: source);

    /// <summary>Selects the first three values — East, North and West — as the user does.</summary>
    private static Task SelectThreeValuesAsync(IRenderedComponent<PivotComponent> cut)
    {
        var grid = Grid(cut).Instance;
        return cut.InvokeAsync(() => grid.PlaceSelectionAsync(new SelectionRange(0, 1, 3, 1), new CellPosition(0, 1), grid.RowSequenceVersion));
    }

    /// <summary>What the report grid last said in its live region.</summary>
    private static string Announced(IRenderedComponent<PivotComponent> cut)
        => cut.Find(".ex-pivot-sheet > .ex-grid .ex-announce").TextContent;

    [Theory] // ADR-0060/0152: a copy the source misanswers is refused in a word of ExPivot's table — under the Japanese words, its Japanese
    [InlineData(Misanswer.AnotherVersion, "copy-another-version")]
    [InlineData(Misanswer.RangeLeftOut, "copy-missing-range")]
    [InlineData(Misanswer.RangeCutShort, "copy-incomplete-range")]
    public async Task ADR0060_A_copy_the_source_misanswers_is_refused_in_a_word_of_the_table(Misanswer misanswer, string id)
    {
        await using var source = new MisansweringSource(Bundled(), misanswer);
        var cut = RenderJapanese(source);
        await SelectThreeValuesAsync(cut);

        var payload = await cut.InvokeAsync(() => Grid(cut).Instance.BuildCopyPayloadAsync());

        Assert.Null(payload);
        var expected = Japanese(id);
        cut.WaitForAssertion(() => Assert.Equal(expected, Announced(cut)));
        Assert.DoesNotMatch(Latin, Announced(cut));
    }

    [Fact] // ADR-0060/0152: a copy the source refuses in its own words is refused in them, inside ExPivot's source-refused, as a refused question is
    public async Task ADR0060_A_copy_the_source_refuses_says_its_words_inside_the_tables()
    {
        await using var source = new MisansweringSource(Bundled(), Misanswer.RefusedInItsOwnWords);
        var cut = RenderJapanese(source);
        await SelectThreeValuesAsync(cut);

        var payload = await cut.InvokeAsync(() => Grid(cut).Instance.BuildCopyPayloadAsync());

        Assert.Null(payload);
        var expected = Japanese("source-refused", OwnWords);
        cut.WaitForAssertion(() => Assert.Equal(expected, Announced(cut)));
    }

    [Theory] // ADR-0060/0152: a Selection Summary the source misanswers is declined in a word of ExPivot's table — under the Japanese words, its Japanese
    [InlineData(Misanswer.AnotherVersion, "summary-another-version")]
    [InlineData(Misanswer.ErrorCountsDisagree, "summary-inconsistent-errors")]
    public async Task ADR0060_A_summary_the_source_misanswers_is_declined_in_a_word_of_the_table(Misanswer misanswer, string id)
    {
        await using var source = new MisansweringSource(Bundled(), misanswer);
        var cut = RenderJapanese(source);

        await SelectThreeValuesAsync(cut);

        var expected = Japanese(id);
        cut.WaitForAssertion(() => Assert.Equal(expected, cut.Find(".ex-summary").TextContent));
        Assert.DoesNotMatch(Latin, cut.Find(".ex-summary").TextContent);
    }

    [Fact] // ADR-0060/0152: a Selection Summary the source refuses in its own words is declined in them, inside ExPivot's source-refused
    public async Task ADR0060_A_summary_the_source_refuses_says_its_words_inside_the_tables()
    {
        await using var source = new MisansweringSource(Bundled(), Misanswer.RefusedInItsOwnWords);
        var cut = RenderJapanese(source);

        await SelectThreeValuesAsync(cut);

        var expected = Japanese("source-refused", OwnWords);
        cut.WaitForAssertion(() => Assert.Equal(expected, cut.Find(".ex-summary").TextContent));
    }

    [Fact] // ADR-0060: without the Japanese words, the same refusal is the table's English, and a Consumer's Label replaces it by id
    public async Task ADR0060_The_refusal_is_the_tables_English_and_a_Label_replaces_it_by_id()
    {
        await using var english = new MisansweringSource(Bundled(), Misanswer.AnotherVersion);
        var cut = RenderPivot(RegionAmount, reportSource: english);
        await SelectThreeValuesAsync(cut);
        await cut.InvokeAsync(() => Grid(cut).Instance.BuildCopyPayloadAsync());
        var word = PivotWords.EnglishFor("copy-another-version");
        Assert.NotNull(word);
        cut.WaitForAssertion(() => Assert.Equal(word, Announced(cut)));

        await using var replaced = new MisansweringSource(Bundled(), Misanswer.AnotherVersion);
        var relabelled = RenderPivot(RegionAmount,
            ps => ps.Add(p => p.Label, id => id == "copy-another-version" ? "Copy refused: the report moved on." : null),
            reportSource: replaced);
        await SelectThreeValuesAsync(relabelled);
        await relabelled.InvokeAsync(() => Grid(relabelled).Instance.BuildCopyPayloadAsync());
        relabelled.WaitForAssertion(() => Assert.Equal("Copy refused: the report moved on.", Announced(relabelled)));
    }
}
