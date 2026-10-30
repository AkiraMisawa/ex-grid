using ExPivot.Engine;
using Xunit;
using static ExGrid.DemoApi.Tests.PivotSources;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// ADR-0370 section 3: past the cap, the demo's SQL source answers a question with levels with the
/// groups it shows, and a set ordered by a value past the window's limit as a window of it, as
/// <c>PivotSource.From</c> does over the same trades, cell for cell, through <c>PivotJson</c> both
/// ways. The bundled sources' level answer is the TypeScript engine's to the last bit (the shared
/// cases under <c>tests/cases/pivot/</c>), so an answer equal to it is one the TypeScript report lays
/// out; <c>PivotSource.Fetch</c> refuses one that does not answer the levels asked.
/// </summary>
public sealed class PivotLevelTests(PivotApiServer server) : IClassFixture<PivotApiServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PivotQueryField F(string field, params PivotItemKey[] hidden) => new(field, hidden);

    private static PivotQueryValue V(string field, PivotParts parts = PivotParts.Sum | PivotParts.Extremes) => new(field, parts);

    private static PivotItemKey T(string text) => PivotItemKey.Text(text);

    private static PivotLevelRule Collapsed(params PivotItemKey[] toggled) => new(collapsed: true, toggled);

    private static PivotLevelRule Expanded(params PivotItemKey[] toggled) => new(collapsed: false, toggled);

    private static readonly PivotLevelRule Open = PivotLevelRule.Expanded;

    /// <summary>Questions whose leaves pass their cap and whose shown groups do not: each field's
    /// rule collapsed or expanded, with Items toggled spelled in another case, a Blank toggled, an
    /// Item no stored value can be, and Hidden Items beside them.</summary>
    public static TheoryData<string, PivotQuery> Questions => new()
    {
        {
            "Region collapsed but EMEA, spelled in another case, over Book, by Month",
            new PivotQuery(rows: [F("Region"), F("Book")], columns: [F("Month")], values: [V("Pnl"), V("Quantity", PivotParts.Counts)], maxLeaves: 800)
            {
                Levels = new([Collapsed(T("emea")), Open], [Open]),
            }
        },
        {
            "Region expanded but APAC over Desk and Book, with a Hidden Item",
            new PivotQuery(rows: [F("Region"), F("Desk", T("fx")), F("Book")], values: [V("Notional", PivotParts.Sum)], maxLeaves: 100)
            {
                Levels = new([Expanded(T("APAC")), Collapsed(T("Rates")), Open], []),
            }
        },
        {
            "Currency collapsed but the Blank, over Book, in Columns",
            new PivotQuery(rows: [F("Desk")], columns: [F("Currency"), F("Book")], values: [V("Pnl")], maxLeaves: 400)
            {
                Levels = new([Open], [Collapsed(PivotItemKey.Blank), Open]),
            }
        },
        {
            "Currency collapsed but the Blank and EUR, spelled in another case, over Product, in Columns",
            new PivotQuery(rows: [F("Desk")], columns: [F("Currency"), F("Product")], values: [V("Pnl")], maxLeaves: 100)
            {
                Levels = new([Open], [Collapsed(PivotItemKey.Blank, T("eur")), Open]),
            }
        },
        {
            "Region collapsed but APAC and an Item with a letter outside ASCII; the innermost field's rule changes nothing",
            new PivotQuery(rows: [F("Region"), F("Desk")], values: [V("Pnl")], maxLeaves: 10)
            {
                Levels = new([Collapsed(T("Émea"), T("apac")), Collapsed(T("Rates"))], []),
            }
        },
        {
            "every Item collapsed, toggled only by an Item no stored value can be",
            new PivotQuery(rows: [F("Region"), F("TradeId")], columns: [F("Confirmed")], values: [V("Pnl")], maxLeaves: 100)
            {
                Levels = new([Collapsed(PivotItemKey.Number(1)), Open], [Open]),
            }
        },
        {
            "the leaves fit: answered with the leaves, levels or not",
            new PivotQuery(rows: [F("Region"), F("Desk")], values: [V("Pnl")], maxLeaves: 100)
            {
                Levels = new([Collapsed(), Open], []),
            }
        },
        {
            "every Item expanded: the groups shown pass the cap too, and the question is refused",
            new PivotQuery(rows: [F("Region"), F("Book")], values: [V("Pnl")], maxLeaves: 150)
            {
                Levels = new([Open, Open], []),
            }
        },
    };

    [Theory] // ADR-0370 section 3: the SQL source's Level Aggregates are PivotSource.From's, cell for cell
    [MemberData(nameof(Questions))]
    public async Task ADR0370_the_SQL_source_answers_Level_Aggregates_as_PivotSource_From(string layout, PivotQuery query)
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);
        Assert.True(sources.Sql.Features.AnswersLevels);

        var answer = await sources.Sql.AggregateAsync(query, Token);
        var reference = await sources.ReferenceAnswer(query);

        SameAnswer(reference, answer);
        Assert.Equal(!layout.StartsWith("the leaves fit", StringComparison.Ordinal) && !answer.IsRefused, answer.IsLevels);
        Assert.Equal(layout.StartsWith("every Item expanded", StringComparison.Ordinal), answer.IsRefused);
    }

    /// <summary>Sets ordered by a value past the window's limit: under each Region, its trades by
    /// their P&amp;L, from the first and from a start asked for, past the end and across a column
    /// field; and Books windowed above their trades, so a set under a Book merged into a rest has no
    /// window, and one under a Book shown has its own. No P&amp;L here ties at a margin, where the
    /// sources may choose another of the tied siblings (the next test).</summary>
    public static TheoryData<string, PivotQuery> WindowQuestions
    {
        get
        {
            var trades = new[] { F("Region"), F("TradeId") };
            var byPnl = new PivotLevelOrder(0, PivotAggregation.Sum, descending: true);
            return new()
            {
                {
                    "each Region's trades by P&L, from the first",
                    new PivotQuery(rows: trades, values: [V("Pnl"), V("Quantity", PivotParts.Sum)], maxLeaves: 5_000)
                    {
                        Levels = new([Open, new PivotLevelRule(order: byPnl)], [], new PivotLevelWindow(1_000)),
                    }
                },
                {
                    "from a start under EMEA, spelled in another case, and past the end under APAC",
                    new PivotQuery(rows: trades, values: [V("Pnl")], maxLeaves: 5_000)
                    {
                        Levels = new([Open, new PivotLevelRule(order: byPnl)], [],
                            new PivotLevelWindow(1_000, [new PivotWindowStart([T("emea")], 2_500), new PivotWindowStart([T("APAC")], 1_000_000)])),
                    }
                },
                {
                    "by an Average, ascending, across a column field",
                    new PivotQuery(rows: trades, columns: [F("Confirmed")], values: [V("Pnl")], maxLeaves: 10_000)
                    {
                        Levels = new([Open, new PivotLevelRule(order: new PivotLevelOrder(0, PivotAggregation.Average, descending: false))], [Open],
                            new PivotLevelWindow(1_000, [new PivotWindowStart([T("Americas")], 3_000)])),
                    }
                },
                {
                    "Books windowed above their trades",
                    new PivotQuery(rows: [F("Book"), F("TradeId")], values: [V("Pnl")], maxLeaves: 5_000)
                    {
                        Levels = new([new PivotLevelRule(order: byPnl), new PivotLevelRule(order: byPnl)], [],
                            new PivotLevelWindow(20, [new PivotWindowStart([], 90)])),
                    }
                },
            };
        }
    }

    [Theory] // ADR-0370 section 3: the SQL source's Sibling Windows are PivotSource.From's, rest cells and all
    [MemberData(nameof(WindowQuestions))]
    public async Task ADR0370_the_SQL_source_answers_a_Sibling_Window_as_PivotSource_From(string layout, PivotQuery query)
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);

        var answer = await sources.Sql.AggregateAsync(query, Token);

        Assert.False(answer.IsRefused, $"{layout}: {answer.Refusal?.Message}");
        Assert.True(answer.IsLevels, layout);
        Assert.NotEmpty(answer.Windows);
        SameAnswer(await sources.ReferenceAnswer(query), answer);
    }

    [Fact] // ADR-0370 section 3: a window is widened over the siblings tied with its first or its last
    public async Task ADR0370_a_window_is_widened_over_ties_as_PivotSource_From_widens_it()
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);
        // Each Region's Books by how many trades they hold: about a hundred each, so many tie.
        const int limit = 10;
        var query = new PivotQuery(rows: [F("Region"), F("Book")], columns: [F("Confirmed")], values: [V("Pnl", PivotParts.Counts | PivotParts.Sum)], maxLeaves: 300)
        {
            Levels = new([Open, new PivotLevelRule(order: new PivotLevelOrder(0, PivotAggregation.Count, descending: true))], [Open],
                new PivotLevelWindow(limit, [new PivotWindowStart([T("EMEA")], 20), new PivotWindowStart([T("APAC")], 40)])),
        };

        var answer = await sources.Sql.AggregateAsync(query, Token);

        Assert.False(answer.IsRefused, answer.Refusal?.Message);
        Assert.Equal(3, answer.Windows.Count);
        Assert.Contains(answer.Windows, window => SiblingsAnswered(answer, window) - Margins(window) > limit);
        SameWindowedAnswer(await sources.ReferenceAnswer(query), answer, (read, cell) => read.Values[0].ValuesAt(cell), descending: true);
    }

    // The siblings a window's answer holds cells for at the grand total of the columns: its margins
    // and the siblings it shows.
    private static int SiblingsAnswered(PivotAnswer answer, PivotAnswerWindow window)
        => Enumerable.Range(0, answer.LeafCount).Count(cell => answer.RestAt(cell) is null
            && answer.Columns.All(axis => axis.ItemAt(cell) < 0)
            && answer.Rows[0].ItemAt(cell) == window.Parent[0] && answer.Rows[1].ItemAt(cell) >= 0);

    private static int Margins(PivotAnswerWindow window) => (window.Before > 0 ? 1 : 0) + (window.After > 0 ? 1 : 0);

    [Fact] // ADR-0370 section 3: a window's cells count toward the cap, rest cells included
    public async Task ADR0370_a_windowed_answer_past_the_cap_is_refused()
    {
        using var client = server.Factory.CreateClient();
        var sources = await PivotSources.Over(server, client);
        var query = new PivotQuery(rows: [F("Region"), F("TradeId")], values: [V("Pnl")], maxLeaves: 1_000)
        {
            Levels = new([Open, new PivotLevelRule(order: new PivotLevelOrder(0, PivotAggregation.Sum, descending: true))], [], new PivotLevelWindow(1_000)),
        };

        var answer = await sources.Sql.AggregateAsync(query, Token);

        Assert.Equal(PivotSourceRefusalKind.TooManyLeaves, answer.Refusal?.Kind);
        SameAnswer(await sources.ReferenceAnswer(query), answer);
    }
}
