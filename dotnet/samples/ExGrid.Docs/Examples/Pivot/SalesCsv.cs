using System.Globalization;
using System.Text;

namespace ExGrid.Docs.Examples.Pivot;

/// <summary>The sales as an order system's export writes them: a file made in memory, as a user
/// would otherwise choose one with Blazor's <c>InputFile</c>.</summary>
public static class SalesCsv
{
    /// <summary>The data record a malformed file breaks, counted from one.</summary>
    public const int MalformedRow = 1_234;

    /// <summary><paramref name="count"/> sales in UTF-8 with CR LF line ends: a comma between
    /// fields, ISO dates, money with a thousands separator, quoted. With
    /// <paramref name="malformed"/>, the revenue of record <see cref="MalformedRow"/> has a
    /// letter O for its first digit, as a hand-edited file might.</summary>
    public static byte[] Write(int count, bool malformed = false)
    {
        var csv = new StringBuilder("Order,Region,Country,Category,Product,Channel,Order date,Units,Revenue,Cost\r\n");
        var row = 0;
        foreach (var s in Sales.Sample(count))
        {
            row++;
            var revenue = s.Revenue.ToString("#,##0.00", CultureInfo.InvariantCulture);
            if (malformed && row == MalformedRow)
                revenue = "O" + revenue[1..];
            csv.Append(CultureInfo.InvariantCulture,
                $"{s.Id},{s.Region},{s.Country},{s.Category},\"{s.Product.Replace("\"", "\"\"")}\",{s.Channel},{s.Date:yyyy-MM-dd},{s.Units},\"{revenue}\",\"{s.Cost:#,##0.00}\"\r\n");
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(csv.ToString());
    }
}
