using System.Globalization;
using ExGrid.Clipboard;
using ExGrid.Columns;

namespace ExGrid.Docs.Examples.Grid;

public sealed class NumericHeaderCopy
{
    public sealed record Sample(string Name, double Amount);

    public InMemoryGridSource<Sample> Source { get; } = GridSource.From(new[]
    {
        new Sample("First", 10),
        new Sample("Second", 20),
    });

    public GridColumn<Sample>[] Columns { get; } =
    [
        new("Name", ColumnType.Text, row => row.Name, header: "Sample", width: new(ColumnWidth.Fixed(140))),
        new("Amount", ColumnType.Number, row => row.Amount, header: "1.23457E+11", width: new(ColumnWidth.Fixed(180))),
    ];

    public GridCopyAnswer Answer(GridCopyRequest request)
    {
        // This small source holds the whole result, in the current filter and sort order.
        // Read that order synchronously, under the version the request was taken against.
        string Cell(int row, int column) => column == 0
            ? Source.Window[row].Name
            : Source.Window[row].Amount.ToString("R", CultureInfo.InvariantCulture);
        string Header(int column) => column == 0 ? "Sample" : "1.23457E+11";
        string? HeaderRaw(int column) => column == 0 ? null : "123456789012.345";

        var copy = ClipboardData.Assemble(request.Plan, Cell, Cell,
            headerAt: request.WithHeaders ? Header : null,
            headerRawAt: HeaderRaw);
        return GridCopyAnswer.Write(copy.Text, copy.Html);
    }
}
