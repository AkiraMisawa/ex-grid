using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The Order Key (ADR-0059, Q19/Q20/Q27; PV-31): a function from an Item's value to a key, called
/// once per Item. Items are ordered by key, ties by label; an Item with no key comes after the keyed
/// ones; descending reverses the whole order with (blank) still last; a function that throws is
/// refused by name; two values with one key stay two Items; it orders the Item lists, and never a
/// sort by value. Rate and credit deltas by tenor are the case it was decided for.
/// </summary>
public class OrderKeyTests
{
    private sealed record Delta(string? Tenor, decimal Amount);

    /// <summary>A tenor's length in days, as a desk would key it: ON and TN first, then weeks,
    /// months and years, a compound tenor summed — "1Y6M" is 540, as "18M" is. Anything else has
    /// no key. The engine parses no tenor; this is the Consumer's.</summary>
    internal static IComparable? Tenor(string tenor)
    {
        switch (tenor)
        {
            case "ON":
                return 0;
            case "TN":
                return 1;
        }
        var days = 0;
        var number = 0;
        var any = false;
        foreach (var c in tenor)
        {
            if (char.IsAsciiDigit(c))
            {
                number = (number * 10) + (c - '0');
                continue;
            }
            var unit = c switch { 'D' => 1, 'W' => 7, 'M' => 30, 'Y' => 360, _ => -1 };
            if (unit < 0 || number == 0)
                return null;
            days += number * unit;
            number = 0;
            any = true;
        }
        return any && number == 0 ? days : null;
    }

    private static readonly string?[] Tenors = ["30Y", "1Y6M", "ON", "Other", "18M", "1W", null, "TN", "3M", "2Y", "Bucket", "1M", "1Y"];

    private static PivotFields<Delta> Fields(Func<string, IComparable?>? key = null, IReadOnlyList<object>? itemOrder = null)
        => PivotFields.Of<Delta>().Text("Tenor", d => d.Tenor, itemOrder: itemOrder, orderKey: key ?? Tenor).Number("Amount", d => d.Amount);

    private static Delta[] Deltas => [.. Tenors.Select((tenor, i) => new Delta(tenor, i + 1))];

    private static async Task<string[]> Order(PivotSource source, PivotSort? sort = null)
    {
        var layout = new PivotLayout { Rows = [P("Tenor") with { Sort = sort ?? PivotSort.Ascending }], Values = [Sum("Amount")] };
        var report = await SnapshotSourceTests.ReportOf(source, layout);
        return [.. report.Rows.Where(r => r.Role != PivotRowRole.GrandTotal).Select(r => r.Labels[0].Text!)];
    }

    [Fact] // ADR-0059 (PV-31): Items are ordered by their key, ascending, ties by label; an Item with no key comes after the keyed ones, in label order
    public async Task Items_are_ordered_by_key_and_unkeyed_items_come_after()
    {
        var order = await Order(PivotSource.From(Deltas, Fields()));

        Assert.Equal(["ON", "TN", "1W", "1M", "3M", "1Y", "18M", "1Y6M", "2Y", "30Y", "Bucket", "Other", "(blank)"], order);
        Assert.Equal(540, Tenor("1Y6M"));
        Assert.Equal(540, Tenor("18M"));
        Assert.Null(Tenor("Other"));
    }

    [Fact] // ADR-0059 (PV-31): descending reverses the whole order, and (blank) stays last
    public async Task Descending_reverses_the_whole_order_with_blank_last()
    {
        var order = await Order(PivotSource.From(Deltas, Fields()), PivotSort.Descending);

        Assert.Equal(["Other", "Bucket", "30Y", "2Y", "1Y6M", "18M", "1Y", "3M", "1M", "1W", "TN", "ON", "(blank)"], order);
    }

    [Fact] // ADR-0059 (PV-31): two values with one key stay two Items, side by side, each with its own records
    public async Task Two_values_with_one_key_stay_two_items()
    {
        var source = PivotSource.From([new Delta("18M", 1m), new Delta("1Y6M", 2m), new Delta("18m", 4m)], Fields());

        var report = await SnapshotSourceTests.ReportOf(source, new PivotLayout { Rows = [P("Tenor")], Values = [Sum("Amount")] });

        Assert.Equal(["i 18M || 5", "i 1Y6M || 2", "t Grand Total || 7"], Lines(report));
    }

    [Fact] // ADR-0059 (PV-31): a key function that throws is refused, naming the field and the value — never a quiet fall-back to the labels
    public async Task A_key_function_that_throws_is_refused_by_name()
    {
        var source = PivotSource.From([new Delta("1Y", 1m), new Delta("7Y", 2m)], Fields(tenor => tenor == "7Y" ? throw new FormatException("no 7Y bucket") : Tenor(tenor)));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => Order(source));

        Assert.Equal("The Order Key of Tenor failed on '7Y'.", refusal.Message);
        Assert.IsType<FormatException>(refusal.InnerException);
    }

    [Fact] // ADR-0059 (PV-31): the key is called once per Item, however many records and branches the Item has
    public async Task The_key_is_called_once_per_item()
    {
        var calls = new List<string>();
        var fields = PivotFields.Of<Delta>()
            .Text("Tenor", d => d.Tenor, orderKey: tenor => { calls.Add(tenor); return Tenor(tenor); })
            .Text("Book", d => d.Amount > 100 ? "B" : "A")
            .Number("Amount", d => d.Amount);
        // Each tenor three times, under two books.
        var source = PivotSource.From([.. Enumerable.Range(0, 3).SelectMany(k => Deltas.Select(d => d with { Amount = d.Amount + (k * 100) }))], fields);
        var layout = new PivotLayout { Rows = [P("Book"), P("Tenor")], Values = [Sum("Amount")] };
        var query = PivotQuery.For(layout);
        var cube = PivotEngine.Cube(query, await source.AggregateAsync(query, Ct), source.Fields);

        PivotEngine.Report(cube, layout);
        PivotEngine.Report(cube, layout with { Rows = [P("Book"), P("Tenor") with { Sort = PivotSort.Descending }] });

        // Twelve Items with a value — (blank) is never keyed — each called once.
        Assert.Equal(12, calls.Count);
        Assert.Equal(12, calls.Distinct().Count());
    }

    [Fact] // ADR-0059/0060 (PV-31): the key orders the Item lists of Filter… and of the report filter band
    public async Task The_key_orders_the_item_lists()
    {
        var fields = Fields();
        var source = PivotSource.From(Deltas, fields);
        var layout = new PivotLayout { Filters = [P("Tenor") with { HiddenItems = [PivotItemKey.Text("TN")] }], Values = [Sum("Amount")] };
        var version = (await source.AggregateAsync(PivotQuery.For(layout), Ct)).SourceVersion;
        var page = await source.ItemsAsync(new PivotItemsQuery("Tenor", version), Ct);

        var listed = PivotEngine.ItemsOf(page, layout, fields.Fields[0], EnUs);

        Assert.Equal(["ON", "TN", "1W", "1M", "3M", "1Y", "18M", "1Y6M", "2Y", "30Y", "Bucket", "Other", "(blank)"], listed.Select(i => i.Label));
        Assert.True(listed[1].IsHidden);
        // And through the records' own path, which lists a cube's Items itself.
        PivotField<Delta>[] untyped =
        [
            new("Tenor", PivotFieldType.Text, d => d.Tenor) { OrderKey = value => Tenor((string)value) },
            new("Amount", PivotFieldType.Number, d => d.Amount),
        ];
        var cube = PivotEngine.Aggregate(Deltas, untyped, layout);
        Assert.Equal(listed.Select(i => i.Label), PivotEngine.ItemsOf(cube, layout, "Tenor", EnUs).Select(i => i.Label));
    }

    [Fact] // ADR-0059 (PV-31): a sort by a Value Field does not read the key — ties fall back to the label
    public async Task A_sort_by_value_does_not_read_the_key()
    {
        var calls = 0;
        var source = PivotSource.From(
            [new Delta("ON", 5m), new Delta("30Y", 5m), new Delta("1W", 1m), new Delta("TN", 9m)],
            Fields(tenor => { calls++; return Tenor(tenor); }));

        var order = await Order(source, new PivotSort(PivotSortDirection.Ascending, ByValue: 0));

        // By value: 1W (1), then the tie 30Y and ON (5) by label, though ON is keyed first, then TN (9).
        Assert.Equal(["1W", "30Y", "ON", "TN"], order);
        Assert.Equal(0, calls);
    }

    [Fact] // ADR-0059 (PV-31): a declared Item order still comes first, and the key orders the rest
    public async Task A_declared_item_order_comes_first()
    {
        var order = await Order(PivotSource.From(Deltas, Fields(itemOrder: ["2Y", "1w"])));

        Assert.Equal(["2Y", "1W", "ON", "TN", "1M", "3M", "1Y", "18M", "1Y6M", "30Y", "Bucket", "Other", "(blank)"], order);
    }

    [Fact] // ADR-0059 (PV-31): a number's key is given the number; a field's keys are of one type, or the field is refused by name
    public async Task A_numbers_key_is_given_the_number_and_keys_are_of_one_type()
    {
        var strikes = PivotFields.Of<decimal>().Number("Strike", d => d, orderKey: d => Math.Abs(d)).Number("Amount", d => 1m);
        var source = PivotSource.From([-3m, 2m, -1m, 4m], strikes);
        var report = await SnapshotSourceTests.ReportOf(source, new PivotLayout { Rows = [P("Strike")], Values = [Sum("Amount")] });

        Assert.Equal(["-1", "2", "-3", "4"], report.Rows.Where(r => r.Role == PivotRowRole.Item).Select(r => r.Labels[0].Text!));
        var mixed = PivotSource.From(Deltas, Fields(tenor => tenor == "ON" ? "zero" : Tenor(tenor)));
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => Order(mixed));
        Assert.Contains("The Order Key of Tenor", refusal.Message);
        Assert.Contains("one type", refusal.Message);
    }
}
