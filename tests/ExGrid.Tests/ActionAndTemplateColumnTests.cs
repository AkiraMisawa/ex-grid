using ExGrid.Cells;
using ExGrid.Columns;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What an Action or Template Column is allowed to be, and what the query engine does
/// when asked to order by one that has no value (ADR-0020).
/// </summary>
public class ActionAndTemplateColumnTests
{
    private static readonly GridAction Open = new("open", "Open");
    private static readonly RenderFragment<TemplateCellContext<Trade>> Bar =
        cell => builder => builder.AddContent(0, cell.Row.Book);

    [Fact] // ADR-0016 / FN-12: every kind of column refuses a Fixed width below MinWidth by name
    public void Action_and_template_columns_refuse_a_width_below_min_width_by_name()
    {
        var narrow = new ColumnWidthSpec(ColumnWidth.Fixed(10), minWidthPx: 40);

        Assert.Contains("'Actions'", Assert.Throws<ArgumentOutOfRangeException>(
            () => GridColumn<Trade>.ActionColumn("Actions", [Open], width: narrow)).Message);
        Assert.Contains("'Book'", Assert.Throws<ArgumentOutOfRangeException>(
            () => GridColumn<Trade>.TemplateColumn("Book", ColumnType.Text, r => r.Book, Bar, width: narrow)).Message);
    }

    [Fact] // ADR-0020: an Action Column has no value, and says so
    public void An_action_column_is_not_queryable()
    {
        var column = GridColumn<Trade>.ActionColumn("Actions", [Open]);

        Assert.False(column.IsQueryable);
        Assert.False(column.PaintsValue);
        Assert.Equal([Open], column.Actions);
        // Empty by default: an Action Column's header usually names nothing (ADR-0020).
        Assert.Equal("", column.Header);
    }

    [Fact] // ADR-0020: the number of actions is declared, so declaring none is a mistake, not an empty cell
    public void An_action_column_with_no_actions_is_refused()
    {
        Assert.Throws<ArgumentException>(() => GridColumn<Trade>.ActionColumn("Actions", []));
    }

    [Fact] // ADR-0020: an action is recognised by name when it is reported back, so two cannot share one
    public void Two_actions_cannot_share_a_name()
    {
        Assert.Throws<ArgumentException>(() => GridColumn<Trade>.ActionColumn(
            "Actions", [new GridAction("open", "Open"), new GridAction("open", "Open in tab")]));
    }

    [Fact] // ADR-0020: an icon-only action still needs its label — it is the accessible name
    public void An_action_without_a_label_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new GridAction("open", "", cssClass: "icon-open"));
    }

    [Fact] // ADR-0020 / ADR-0009: a Template Column still carries the value accessor sorting and copy read
    public void A_template_column_still_carries_its_value()
    {
        var column = GridColumn<Trade>.TemplateColumn("Book", ColumnType.Text, r => r.Book, Bar);

        Assert.True(column.IsQueryable);
        Assert.False(column.PaintsValue);
        Assert.NotNull(column.Template);
        Assert.Equal("Alpha", column.Value(new Trade(Book: "Alpha")));
    }

    [Fact] // ADR-0020: a template is rendering only; there is no template-without-a-value column
    public void A_template_column_without_a_template_or_a_value_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => GridColumn<Trade>.TemplateColumn(
            "Book", ColumnType.Text, r => r.Book, null!));
        Assert.Throws<ArgumentNullException>(() => GridColumn<Trade>.TemplateColumn(
            "Book", ColumnType.Text, null!, Bar));
    }

    [Fact] // ADR-0020: sorting by a column with no value is refused by name, never silently accepted
    public void Sorting_by_an_action_column_is_refused_by_name()
    {
        ColumnInfo<Trade>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book),
            GridColumn<Trade>.ActionColumn("Actions", [Open]).Info,
        ];

        var error = Assert.Throws<InvalidOperationException>(() => GridQueryEngine.Apply(
            [new Trade(Book: "Alpha")], columns, filter: null, [new SortSpec("Actions", SortDirection.Ascending)]));

        Assert.Contains("Actions", error.Message);
    }

    [Fact] // ADR-0020: and filtering likewise — the refusal is in one place, for both
    public void Filtering_on_an_action_column_is_refused_by_name()
    {
        ColumnInfo<Trade>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book),
            GridColumn<Trade>.ActionColumn("Actions", [Open]).Info,
        ];
        var filter = new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Actions"] = new([new FilterClause(FilterOperator.IsBlank)]),
        });

        var error = Assert.Throws<InvalidOperationException>(() => GridQueryEngine.Apply(
            [new Trade(Book: "Alpha")], columns, filter, sorts: null));

        Assert.Contains("Actions", error.Message);
    }

    [Fact] // ADR-0020: a Template Column sorts and filters like any other — the value is what is read
    public void A_template_column_sorts_on_its_value()
    {
        ColumnInfo<Trade>[] columns = [GridColumn<Trade>.TemplateColumn("Book", ColumnType.Text, r => r.Book, Bar).Info];
        Trade[] rows = [new(Book: "Beta"), new(Book: "Alpha")];

        var sorted = GridQueryEngine.Apply(rows, columns, filter: null, [new SortSpec("Book", SortDirection.Ascending)]);

        Assert.Equal("Alpha", sorted[0].Book);
    }
}
