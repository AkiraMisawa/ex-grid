using Xunit;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// Every column width to the hundredth crosses both ways in Excel's own unit (ADR-0330 §4, ADR-0046, ADR-0047); the widths Excel
/// writes, and widths read and written, are held by the shared cases (<c>tests/cases/sheet-xlsx</c>).
/// </summary>
public class ColumnWidthTests
{
    [Fact] // ADR-0330 §4: every width to the hundredth, from 0.01 to 255, reads back as it was written
    public void ADR0330_every_width_to_the_hundredth_round_trips()
    {
        var wrong = new List<double>();
        for (var hundredths = 1; hundredths <= 25_500; hundredths++)
        {
            var characters = hundredths / 100.0;
            if (ColumnWidthUnit.FromFile(ColumnWidthUnit.ToFile(characters)) != characters) wrong.Add(characters);
        }

        Assert.Empty(wrong);
    }
}
