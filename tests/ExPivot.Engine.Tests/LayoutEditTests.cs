using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>What each Field List gesture means (ADR-0060), as the engine's pure rules.</summary>
public class LayoutEditTests
{
    private static string Describe(PivotLayout layout)
    {
        var sigma = layout.Values.Count >= 2;
        string Area(IReadOnlyList<PivotFieldPlacement> placements, PivotAxis axis)
            => string.Join(",", placements.Select(p => p.Field).Concat(sigma && layout.ValuesAxis == axis ? ["Σ"] : []));
        return $"F[{string.Join(",", layout.Filters.Select(p => p.Field))}] "
            + $"C[{Area(layout.Columns, PivotAxis.Columns)}] "
            + $"R[{Area(layout.Rows, PivotAxis.Rows)}] "
            + $"V[{string.Join(",", layout.Values.Select(v => v.Aggregation + ":" + v.Field))}]";
    }

    [Fact] // ADR-0060: ticking places a Number field in Values with Sum, any other at the end of Rows
    public void Ticking_places_where_excel_places()
    {
        var layout = PivotLayoutEdits.Tick(PivotLayout.Empty, Info("Region"));
        layout = PivotLayoutEdits.Tick(layout, Info("Amount"));
        layout = PivotLayoutEdits.Tick(layout, Info("Date"));
        layout = PivotLayoutEdits.Tick(layout, Info("Online"));

        Assert.Equal("F[] C[] R[Region,Date,Online] V[Sum:Amount]", Describe(layout));
        Assert.Same(layout, PivotLayoutEdits.Tick(layout, Info("Region")));
    }

    [Fact] // ADR-0060: unticking removes the field from every Area, each Value Field of it too
    public void Unticking_removes_the_field_everywhere()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Columns = [P("Product") with { Sort = new PivotSort(PivotSortDirection.Descending, ByValue: 2) }],
            Values = [Sum("Amount"), Value("Region", PivotAggregation.Count), Sum("Quantity")],
        };

        var untick = PivotLayoutEdits.Untick(layout, "Region");

        Assert.Equal("F[] C[Product,Σ] R[] V[Sum:Amount,Sum:Quantity]", Describe(untick));
        // The order by Quantity follows it to its new index.
        Assert.Equal(new PivotSort(PivotSortDirection.Descending, 1), untick.Columns[0].Sort);
        Assert.Same(untick, PivotLayoutEdits.Untick(untick, "Region"));
    }

    [Fact] // ADR-0059/0060: removing the Value Field an order is by falls back to the label, same direction
    public void Removing_the_value_field_an_order_is_by()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Descending, 0) }],
            Values = [Sum("Amount")],
        };

        var removed = PivotLayoutEdits.Remove(layout, new PivotEntry(PivotArea.Values, 0));

        Assert.Equal(PivotSort.Descending, removed.Rows[0].Sort);
    }

    [Fact] // ADR-0060: a field dropped from the list on an Area, at a position
    public void Placing_from_the_list()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")] };

        Assert.Equal("F[] C[] R[Region,Date,Product] V[]", Describe(PivotLayoutEdits.Place(layout, Info("Date"), PivotArea.Rows, 1)));
        Assert.Equal("F[] C[Date] R[Region,Product] V[]", Describe(PivotLayoutEdits.Place(layout, Info("Date"), PivotArea.Columns, 0)));
        Assert.Equal("F[] C[] R[Region,Product,Date] V[]", Describe(PivotLayoutEdits.Place(layout, Info("Date"), PivotArea.Rows, 99)));
    }

    [Fact] // ADR-0060: a field standing in Rows dropped from the list on Columns moves there, with its settings
    public void A_field_moves_between_areas_with_its_settings()
    {
        var hidden = P("Region") with { HiddenItems = [PivotItemKey.Text("West")], Sort = PivotSort.Descending, Subtotals = false };
        var layout = new PivotLayout { Rows = [hidden, P("Product")] };

        var moved = PivotLayoutEdits.Place(layout, Info("Region"), PivotArea.Columns, 0);

        Assert.Equal("F[] C[Region] R[Product] V[]", Describe(moved));
        Assert.Same(hidden, moved.Columns[0]);
    }

    [Fact] // ADR-0060: a field dropped on Values from the list is a new Value Field, even a second one
    public void Dropping_on_values_adds_a_value_field()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };

        var twice = PivotLayoutEdits.Place(layout, Info("Amount"), PivotArea.Values, 1);
        var region = PivotLayoutEdits.Place(twice, Info("Region"), PivotArea.Values, 0);

        Assert.Equal("F[] C[Σ] R[Region] V[Sum:Amount,Sum:Amount]", Describe(twice));
        Assert.Equal("F[] C[Σ] R[Region] V[Count:Region,Sum:Amount,Sum:Amount]", Describe(region));
    }

    [Fact] // ADR-0060: an entry reordered within its Area, by its menu's Move Up, Down, Beginning and End
    public void Reordering_within_an_area()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product"), P("Date")] };
        PivotLayout Move(int from, int to) => PivotLayoutEdits.Move(layout, new PivotEntry(PivotArea.Rows, from), PivotArea.Rows, to, Info(layout.Rows[from].Field));

        Assert.Equal("F[] C[] R[Product,Region,Date] V[]", Describe(Move(1, 0)));    // Move Up
        Assert.Equal("F[] C[] R[Region,Date,Product] V[]", Describe(Move(1, 3)));    // Move Down
        Assert.Equal("F[] C[] R[Date,Region,Product] V[]", Describe(Move(2, 0)));    // Move to Beginning
        Assert.Equal("F[] C[] R[Product,Date,Region] V[]", Describe(Move(0, 3)));    // Move to End
        Assert.Same(layout, Move(1, 1));
        Assert.Same(layout, Move(1, 2));
    }

    [Fact] // ADR-0060: an entry moved to Values leaves its Area and becomes a Value Field
    public void A_field_moved_to_values()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Amount")], Values = [Sum("Quantity")] };

        var moved = PivotLayoutEdits.Move(layout, new PivotEntry(PivotArea.Rows, 1), PivotArea.Values, 0, Info("Amount"));

        Assert.Equal("F[] C[Σ] R[Region] V[Sum:Amount,Sum:Quantity]", Describe(moved));
    }

    [Fact] // ADR-0060: a Value Field moved to another Area leaves Values, and its field is placed there
    public void A_value_field_moved_to_an_area()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), Value("Product", PivotAggregation.Count)] };

        var moved = PivotLayoutEdits.Move(layout, new PivotEntry(PivotArea.Values, 1), PivotArea.Rows, 0, Info("Product"));

        Assert.Equal("F[] C[] R[Product,Region] V[Sum:Amount]", Describe(moved));
    }

    [Fact] // ADR-0059/0060: Value Fields reordered, and an order by one follows it
    public void Reordering_value_fields_keeps_orders_on_their_field()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Descending, 0) }],
            Values = [Sum("Amount"), Sum("Quantity"), Value("Amount", PivotAggregation.Max)],
        };

        var moved = PivotLayoutEdits.Move(layout, new PivotEntry(PivotArea.Values, 0), PivotArea.Values, 3, Info("Amount"));

        Assert.Equal("F[] C[Σ] R[Region] V[Sum:Quantity,Max:Amount,Sum:Amount]", Describe(moved));
        Assert.Equal(2, moved.Rows[0].Sort.ByValue);
    }

    [Fact] // ADR-0060: Σ Values moves between Rows and Columns only, and cannot be removed
    public void Values_pseudo_field_moves_between_rows_and_columns_only()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), Sum("Quantity")] };
        var sigma = PivotEntry.ValuesPseudoField(layout.ValuesAxis);

        var toRows = PivotLayoutEdits.Move(layout, sigma, PivotArea.Rows, 0, null);

        Assert.Equal("F[] C[] R[Region,Σ] V[Sum:Amount,Sum:Quantity]", Describe(toRows));
        Assert.Same(layout, PivotLayoutEdits.Move(layout, sigma, PivotArea.Filters, 0, null));
        Assert.Same(layout, PivotLayoutEdits.Move(layout, sigma, PivotArea.Values, 0, null));
        Assert.Same(layout, PivotLayoutEdits.Remove(layout, sigma));
        Assert.True(sigma.IsValuesPseudoField);
    }

    [Fact] // ADR-0060: Remove Field
    public void Removing_an_entry()
    {
        var layout = new PivotLayout { Filters = [P("Online")], Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

        Assert.Equal("F[] C[] R[Region,Product] V[Sum:Amount]", Describe(PivotLayoutEdits.Remove(layout, new PivotEntry(PivotArea.Filters, 0))));
        Assert.Equal("F[Online] C[] R[Product] V[Sum:Amount]", Describe(PivotLayoutEdits.Remove(layout, new PivotEntry(PivotArea.Rows, 0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => PivotLayoutEdits.Remove(layout, new PivotEntry(PivotArea.Rows, 2)));
    }

    [Fact] // ADR-0059/0060: Filter… may hide every Item but one, never every one
    public void Hiding_every_item_is_refused()
    {
        var layout = new PivotLayout { Rows = [P("Region")] };
        PivotItemKey[] items = [PivotItemKey.Text("East"), PivotItemKey.Text("West"), PivotItemKey.Blank];

        var some = PivotLayoutEdits.SetHiddenItems(layout, "Region", [items[0], items[2]], items);
        var every = PivotLayoutEdits.SetHiddenItems(layout, "Region", items, items);

        Assert.False(some.IsRefused);
        Assert.Equal([items[0], items[2]], some.Layout.Rows[0].HiddenItems);
        Assert.True(every.IsRefused);
        Assert.Equal(PivotRefusal.HidesEveryItem, every.Refusal);
        Assert.Same(layout, every.Layout);
        Assert.Equal("Select at least one item.", PivotWords.EnglishFor(every.RefusalWord!));
    }

    [Fact] // ADR-0059: collapsing one Item, and Expand / Collapse Entire Field
    public void Collapsing_items_and_fields()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")] };
        var east = PivotItemKey.Text("East");

        var collapsed = PivotLayoutEdits.SetCollapsed(layout, "Region", east, true);
        Assert.True(collapsed.Rows[0].IsCollapsed(east));
        Assert.Same(collapsed, PivotLayoutEdits.SetCollapsed(collapsed, "Region", PivotItemKey.Text("EAST"), true));

        var all = PivotLayoutEdits.SetFieldCollapsed(collapsed, "Region", true);
        Assert.True(all.Rows[0].Collapsed);
        Assert.Empty(all.Rows[0].ToggledItems);

        var eastExpanded = PivotLayoutEdits.SetCollapsed(all, "Region", east, false);
        Assert.False(eastExpanded.Rows[0].IsCollapsed(east));
        Assert.True(eastExpanded.Rows[0].IsCollapsed(PivotItemKey.Text("West")));
        Assert.Equal(layout.Rows[0].ToggledItems, PivotLayoutEdits.SetCollapsed(collapsed, "Region", east, false).Rows[0].ToggledItems);
    }

    [Fact] // ADR-0059/0060: Value Field Settings… refuses a taken or empty caption and an unusable format
    public void Value_field_settings_refusals()
    {
        var layout = new PivotLayout { Values = [Sum("Amount"), Sum("Quantity")] };

        Assert.Equal(PivotRefusal.CaptionTaken, PivotLayoutEdits.SetValueField(layout, 1, Sum("Quantity") with { Caption = "sum of amount" }, Infos).Refusal);
        Assert.Equal(PivotRefusal.CaptionTaken, PivotLayoutEdits.SetValueField(layout, 1, Sum("Quantity") with { Caption = "Region" }, Infos).Refusal);
        Assert.Equal(PivotRefusal.CaptionEmpty, PivotLayoutEdits.SetValueField(layout, 1, Sum("Quantity") with { Caption = " " }, Infos).Refusal);
        Assert.Equal(PivotRefusal.NumberFormatInvalid, PivotLayoutEdits.SetValueField(layout, 1, Sum("Quantity") with { NumberFormat = "N999999999" }, Infos).Refusal);

        var renamed = PivotLayoutEdits.SetValueField(layout, 1, Value("Quantity", PivotAggregation.Average) with { Caption = "Mean quantity", NumberFormat = "N1" }, Infos);
        Assert.False(renamed.IsRefused);
        Assert.Equal("Mean quantity", renamed.Layout.Values[1].Caption);
        // A Value Field may keep its own caption when it is set again.
        Assert.False(PivotLayoutEdits.SetValueField(renamed.Layout, 1, renamed.Layout.Values[1], Infos).IsRefused);
    }

    [Fact] // ADR-0060: sorting and subtotals on a placed field; an unplaced one is refused
    public void Sort_and_subtotals()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] };

        Assert.Equal(PivotSort.Descending, PivotLayoutEdits.SetSort(layout, "Region", PivotSort.Descending).Rows[0].Sort);
        Assert.False(PivotLayoutEdits.SetSubtotals(layout, "Region", false).Rows[0].Subtotals);
        Assert.Same(layout, PivotLayoutEdits.SetSubtotals(layout, "Region", true));
        Assert.Throws<ArgumentException>(() => PivotLayoutEdits.SetSort(layout, "Product", PivotSort.Descending));
        Assert.Throws<ArgumentOutOfRangeException>(() => PivotLayoutEdits.SetSort(layout, "Region", new PivotSort(ByValue: 1)));
    }
}
