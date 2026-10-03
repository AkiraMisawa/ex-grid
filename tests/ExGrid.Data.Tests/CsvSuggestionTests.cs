using System.Globalization;
using System.Text;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// A Schema suggested from a file's first rows (ADR-0064, Q33, DA-9): each column's kind and reading,
/// with every column whose kind is not clear marked, for the user to confirm. Nothing applies it: a
/// file is read under a suggestion only once it, or a Schema changed from it, is handed back.
/// </summary>
public class CsvSuggestionTests
{
    private static CsvSuggestion Suggest(string text, CsvSuggestionOptions? options = null)
        => Suggest(Utf8(text), options);

    private static CsvSuggestion Suggest(byte[] bytes, CsvSuggestionOptions? options = null)
        => CsvSchema.SuggestAsync(new Trickle(bytes), options, TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult();

    private static CsvColumnSuggestion Column(CsvSuggestion suggestion, string name)
        => suggestion.Columns.Single(c => c.Column.Name == name);

    private static CsvDoubt[] Doubts(CsvColumnSuggestion column) => [.. column.Marks.Select(m => m.Doubt)];

    [Fact] // ADR-0064: a suggestion proposes each column's kind and reading, and marks nothing that is clear
    public void A_suggestion_proposes_each_columns_kind_and_reading()
    {
        var suggestion = Suggest("Desk,Qty,Notional,Rate,When,Live\nFX,5,1234.5,1.5e-3,2026-09-30,TRUE\nRates,-7,0.25,2E10,2026-10-01,false\n");

        Assert.Equal(
            [("Desk", SnapshotKind.Text), ("Qty", SnapshotKind.Integer), ("Notional", SnapshotKind.Decimal), ("Rate", SnapshotKind.Double), ("When", SnapshotKind.Date), ("Live", SnapshotKind.Boolean)],
            suggestion.Schema.Columns.Select(c => (c.Name, c.Kind)));
        Assert.Equal(["yyyy-MM-dd"], suggestion.Schema.Columns[4].DateFormats);
        Assert.False(suggestion.IsUnclear);
        Assert.Empty(suggestion.Marks);
        Assert.All(suggestion.Columns, c => Assert.Empty(c.Marks));
        Assert.Equal(2, suggestion.SampledRows);
        Assert.Equal(CsvSeparator.Comma, suggestion.Schema.Separator);
        Assert.True(suggestion.Schema.HasHeader);
        Assert.Same(CsvEncoding.Utf8, suggestion.Schema.Encoding);
        Assert.Equal(["FX", "Rates"], Column(suggestion, "Desk").Examples);
    }

    [Fact] // ADR-0064 (Q33): digits with a leading zero are suggested as Text and marked as a possible identifier, never read as numbers
    public void Leading_zeros_are_suggested_as_text_and_marked()
    {
        var suggestion = Suggest("Account,Qty\n00123,1\n4567,2\n");

        var account = Column(suggestion, "Account");
        Assert.Equal(SnapshotKind.Text, account.Column.Kind);
        Assert.Equal([CsvDoubt.LeadingZeros], Doubts(account));
        Assert.Contains("'00123' (row 1)", account.Marks[0].Note, StringComparison.Ordinal);
        Assert.Contains("could be an identifier", account.Marks[0].Note, StringComparison.Ordinal);
        Assert.True(suggestion.IsUnclear);
        Assert.Empty(Column(suggestion, "Qty").Marks);

        // Read under the suggestion, the account number stays exactly as written.
        Assert.Equal(["00123", "4567"], Values(Read(suggestion.Schema, "Account,Qty\n00123,1\n4567,2\n"), "Account"));
    }

    [Fact] // ADR-0064 (Q33): dates written in more than one format are marked, and every format is suggested
    public void Mixed_date_formats_are_marked()
    {
        var suggestion = Suggest("When\n2026-09-30\n30.09.2026\n2026-10-01\n");

        var when = Column(suggestion, "When");
        Assert.Equal(SnapshotKind.Date, when.Column.Kind);
        Assert.Equal(["yyyy-MM-dd", "d.M.yyyy"], when.Column.DateFormats);
        Assert.Equal([CsvDoubt.MixedDateFormats], Doubts(when));
        Assert.Equal(
            [new DateTime(2026, 9, 30), new DateTime(2026, 9, 30), new DateTime(2026, 10, 1)],
            Values(Read(suggestion.Schema, "When\n2026-09-30\n30.09.2026\n2026-10-01\n"), "When"));
    }

    [Fact] // ADR-0064 (Q33): a date whose day and month could be either way round is marked, and the user's culture decides the suggestion
    public void A_date_that_could_be_day_or_month_first_is_marked()
    {
        const string File = "When\n01/02/2026\n03/04/2026\n";

        var american = Column(Suggest(File, new() { Culture = CultureInfo.GetCultureInfo("en-US") }), "When");
        var british = Column(Suggest(File, new() { Culture = CultureInfo.GetCultureInfo("en-GB") }), "When");

        Assert.Equal(["M/d/yyyy"], american.Column.DateFormats);
        Assert.Equal(["d/M/yyyy"], british.Column.DateFormats);
        Assert.Equal([CsvDoubt.DayOrMonth], Doubts(american));
        Assert.Equal([CsvDoubt.DayOrMonth], Doubts(british));
        // A day past the twelfth settles it, and nothing is marked.
        Assert.Empty(Column(Suggest("When\n01/02/2026\n25/04/2026\n"), "When").Marks);
    }

    [Fact] // ADR-0064 (Q33): numbers written with both a comma and a full stop are marked, with the reading suggested
    public void Numbers_with_both_separators_are_marked()
    {
        var notional = Column(Suggest("Notional\n\"1,234.5\"\n12.75\n"), "Notional");

        Assert.Equal(SnapshotKind.Decimal, notional.Column.Kind);
        Assert.Null(notional.Column.DecimalPoint);
        Assert.Equal(",", notional.Column.ThousandsSeparator);
        Assert.Equal([CsvDoubt.BothSeparators], Doubts(notional));

        var german = Column(Suggest("Betrag\n1.234,5\n12,75\n", new() { Separator = CsvSeparator.Semicolon }), "Betrag");
        Assert.Equal(",", german.Column.DecimalPoint);
        Assert.Equal(".", german.Column.ThousandsSeparator);
        Assert.Equal([CsvDoubt.BothSeparators], Doubts(german));
    }

    [Fact] // ADR-0064 (Q33): a comma that could be the decimal point or the thousands separator is marked
    public void A_separator_that_could_be_either_is_marked()
    {
        var english = Column(Suggest("Amount\n\"1,234\"\n\"5,678\"\n"), "Amount");
        var german = Column(Suggest("Amount\n\"1,234\"\n\"5,678\"\n", new() { Culture = CultureInfo.GetCultureInfo("de-DE") }), "Amount");

        Assert.Equal([CsvDoubt.AmbiguousSeparator], Doubts(english));
        Assert.Equal((SnapshotKind.Integer, ","), (english.Column.Kind, english.Column.ThousandsSeparator));
        Assert.Equal([CsvDoubt.AmbiguousSeparator], Doubts(german));
        Assert.Equal((SnapshotKind.Decimal, ","), (german.Column.Kind, german.Column.DecimalPoint));
    }

    [Fact] // ADR-0064 (Q33): a column empty in every sampled row is marked, and suggested as Text
    public void An_empty_column_is_marked()
    {
        var note = Column(Suggest("Desk,Note\nFX,\nRates,\"\"\n"), "Note");

        Assert.Equal(SnapshotKind.Text, note.Column.Kind);
        Assert.Equal([CsvDoubt.Empty], Doubts(note));
    }

    [Fact] // ADR-0064 (Q33): texts such as NULL among numbers are suggested as Blanks, and marked
    public void Blank_words_among_numbers_are_suggested_as_blanks_and_marked()
    {
        var suggestion = Suggest("Qty,When\n5,2026-09-30\nNULL,-\n7,2026-10-01\n");

        var qty = Column(suggestion, "Qty");
        Assert.Equal(SnapshotKind.Integer, qty.Column.Kind);
        Assert.Equal(["NULL"], qty.Column.BlankText);
        Assert.Equal([CsvDoubt.BlankText], Doubts(qty));
        Assert.Equal(["-"], Column(suggestion, "When").Column.BlankText);
        Assert.Equal([5L, null, 7L], Values(Read(suggestion.Schema, "Qty,When\n5,2026-09-30\nNULL,-\n7,2026-10-01\n"), "Qty"));
    }

    [Fact] // ADR-0064 (Q33): Booleans are never guessed: 0 and 1, and yes and no, are marked as possible Booleans, and suggested as what they are written as
    public void Possible_booleans_are_marked_not_guessed()
    {
        var suggestion = Suggest("Flag,Answer\n0,yes\n1,no\n1,Yes\n");

        var flag = Column(suggestion, "Flag");
        var answer = Column(suggestion, "Answer");
        Assert.Equal(SnapshotKind.Integer, flag.Column.Kind);
        Assert.Equal([CsvDoubt.CouldBeBoolean], Doubts(flag));
        Assert.Equal(SnapshotKind.Text, answer.Column.Kind);
        Assert.Equal([CsvDoubt.CouldBeBoolean], Doubts(answer));
        // A count that is never more than one is not marked: only 0 and 1 side by side are.
        Assert.Empty(Column(Suggest("Count\n1\n1\n"), "Count").Marks);
    }

    [Fact] // ADR-0064 (Q33): numbers with more digits than a Decimal holds are suggested as Text, and marked
    public void Numbers_too_long_for_a_decimal_are_text_and_marked()
    {
        var card = Column(Suggest("Card\n123456789012345678901234567890\n1\n"), "Card");

        Assert.Equal(SnapshotKind.Text, card.Column.Kind);
        Assert.Equal([CsvDoubt.TooLong], Doubts(card));
    }

    [Fact] // ADR-0064 (Q33): a column of numbers with a few texts among them is suggested as Text, and marked
    public void A_column_of_mixed_kinds_is_text_and_marked()
    {
        var text = new StringBuilder("Qty\n");
        for (var i = 1; i <= 20; i++)
            text.Append(i == 7 ? "n/a?" : i.ToString(CultureInfo.InvariantCulture)).Append('\n');

        var qty = Column(Suggest(text.ToString()), "Qty");

        Assert.Equal(SnapshotKind.Text, qty.Column.Kind);
        Assert.Equal([CsvDoubt.MixedKinds], Doubts(qty));
        Assert.Contains("'n/a?' (row 7)", qty.Marks[0].Note, StringComparison.Ordinal);
    }

    [Theory] // ADR-0064: the separator is told from the sample: the one that splits every record alike
    [InlineData("A,B\n1,2\n3,4\n", CsvSeparator.Comma)]
    [InlineData("A\tB\n1,5\t2,5\n3\t4\n", CsvSeparator.Tab)]
    [InlineData("A;B\n1,5;2,5\n3;4\n", CsvSeparator.Semicolon)]
    public void The_separator_is_told_from_the_sample(string file, CsvSeparator separator)
    {
        var suggestion = Suggest(file);

        Assert.Equal(separator, suggestion.Schema.Separator);
        Assert.Equal(["A", "B"], suggestion.Schema.Columns.Select(c => c.Name));
        Assert.DoesNotContain(suggestion.Marks, m => m.Doubt == CsvDoubt.Separator);
    }

    [Fact] // ADR-0064: a separator that cannot be told for sure is marked
    public void A_separator_that_cannot_be_told_is_marked()
    {
        var suggestion = Suggest("A,B;C\n1,2;3\n4,5;6\n");

        Assert.Contains(suggestion.Marks, m => m.Doubt == CsvDoubt.Separator);
        Assert.True(suggestion.IsUnclear);
    }

    [Fact] // ADR-0064: without a header row, the columns are named by position, and the first record is data
    public void A_file_without_a_header_row_is_told_and_its_columns_named()
    {
        var suggestion = Suggest("1,2026-09-30,1.5\n2,2026-10-01,2.5\n");

        Assert.False(suggestion.Schema.HasHeader);
        Assert.Equal(["Column1", "Column2", "Column3"], suggestion.Schema.Columns.Select(c => c.Name));
        Assert.Equal([SnapshotKind.Integer, SnapshotKind.Date, SnapshotKind.Decimal], suggestion.Schema.Columns.Select(c => c.Kind));
        Assert.Equal(2, suggestion.SampledRows);
        Assert.Equal([1L, 2L], Values(Read(suggestion.Schema, "1,2026-09-30,1.5\n2,2026-10-01,2.5\n"), "Column1"));
    }

    [Fact] // ADR-0064: a file of text alone cannot say whether its first record is a header, and is marked
    public void A_header_row_that_cannot_be_told_is_marked()
    {
        var suggestion = Suggest("Desk,Book\nFX,B1\nRates,B2\n");

        Assert.True(suggestion.Schema.HasHeader);
        Assert.Contains(suggestion.Marks, m => m.Doubt == CsvDoubt.Header);
    }

    [Fact] // ADR-0064: what the caller knows of the file is taken as given, not told from the sample
    public void What_the_caller_knows_is_taken_as_given()
    {
        var suggestion = Suggest("Desk,Book\nFX,B1\n", new() { HasHeader = false, Separator = CsvSeparator.Semicolon });

        Assert.False(suggestion.Schema.HasHeader);
        Assert.Equal(CsvSeparator.Semicolon, suggestion.Schema.Separator);
        Assert.Single(suggestion.Schema.Columns);
        Assert.Empty(suggestion.Marks);
    }

    [Fact] // ADR-0064: a header that is empty or stands twice is matched by its position, under a name of its own, and marked
    public void An_empty_or_repeated_header_is_matched_by_position_and_marked()
    {
        var suggestion = Suggest("Amount,,Amount\n1,2,3\n");

        Assert.Equal(["Column1", "Column2", "Column3"], suggestion.Schema.Columns.Select(c => c.Name));
        Assert.Equal([0, 1, 2], suggestion.Schema.Columns.Select(c => c.Position));
        Assert.All(suggestion.Columns, c => Assert.Equal([CsvDoubt.HeaderName], Doubts(c)));
        Assert.Equal([3L], Values(Read(suggestion.Schema, "Amount,,Amount\n1,2,3\n"), "Column3"));
    }

    [Fact] // ADR-0064: records with more or fewer fields than the header are marked, since reading under the Schema refuses them
    public void Records_of_another_length_are_marked()
    {
        var suggestion = Suggest("A,B\n1,2\n3\n4,5\n");

        var mark = Assert.Single(suggestion.Marks, m => m.Doubt == CsvDoubt.FieldCount);
        Assert.Contains("Row 2 has other than the 2 fields", mark.Note, StringComparison.Ordinal);
    }

    [Fact] // ADR-0064 (Q33): the suggestion is made from the first rows only, 1,000 by default, and reads the stream no further than it needs
    public async Task A_suggestion_reads_only_its_first_rows()
    {
        var text = new StringBuilder("Id,Desk\n");
        for (var i = 0; i < 200_000; i++)
            text.Append(i).Append(",D").Append(i % 7).Append('\n');
        var bytes = Utf8(text.ToString());
        var stream = new Trickle(bytes);

        var suggestion = await CsvSchema.SuggestAsync(stream, null, TestContext.Current.CancellationToken);
        var fewer = Suggest(bytes, new() { Rows = 10 });

        Assert.Equal(1_000, suggestion.SampledRows);
        Assert.Equal(10, fewer.SampledRows);
        Assert.True(stream.Position < bytes.Length / 4, $"The suggestion read {stream.Position:N0} of {bytes.Length:N0} bytes.");
    }

    [Fact] // ADR-0064 (Q33, DA-9): a suggestion applies nothing: the file is read under it only when it, or a Schema changed from it, is handed back, and that read is as strict as any
    public void A_suggestion_applies_nothing_until_it_is_handed_back()
    {
        var text = new StringBuilder("Id,Desk,Qty\n");
        for (var i = 1; i <= 1_200; i++)
            text.Append(i).Append(",D").Append(i % 3).Append(',').Append(i == 1_100 ? "lots" : "5").Append('\n');
        var file = text.ToString();

        // A suggestion is a proposal and nothing more: it holds no rows.
        var suggestion = Suggest(file);
        Assert.Equal(SnapshotKind.Integer, Column(suggestion, "Qty").Column.Kind);

        // Handed back as it is, it reads strictly: the row past the sample that does not fit is refused by row and column.
        Assert.Equal("Row 1,100, column 'Qty': 'lots' is not an integer (line 1,101).", Refusal(suggestion.Schema, file).Message);

        // Changed by the user before it is handed back, it reads as changed.
        var changed = suggestion.Schema with
        {
            Columns = [.. suggestion.Schema.Columns.Select(c => c.Name == "Qty" ? c with { Kind = SnapshotKind.Text } : c)],
            RecordKey = "Id",
        };
        var snapshot = Read(changed, file);
        Assert.Equal(1_200, snapshot.RowCount);
        Assert.Equal("lots", Values(snapshot, "Qty")[1_099]);
        Assert.Equal("Id", snapshot.RecordKey?.Name);
    }

    [Fact] // ADR-0064: a Shift-JIS file is suggested from when its encoding is given, and the suggestion carries it
    public void A_shift_jis_file_is_suggested_in_its_declared_encoding()
    {
        var cp932 = CodePagesEncodingProvider.Instance.GetEncoding(932)!;
        var bytes = cp932.GetBytes("取引先,金額\r\n東京表,1234\r\n大阪,-5\r\n");

        var suggestion = Suggest(bytes, new() { Encoding = CsvEncoding.ShiftJis });

        Assert.Same(CsvEncoding.ShiftJis, suggestion.Schema.Encoding);
        Assert.Equal([("取引先", SnapshotKind.Text), ("金額", SnapshotKind.Integer)], suggestion.Schema.Columns.Select(c => (c.Name, c.Kind)));
        Assert.Equal(["東京表", "大阪"], Values(Read(suggestion.Schema, bytes), "取引先"));
        // Undeclared, the same bytes are refused rather than suggested from garbled text.
        Assert.Contains("is not valid UTF-8", Assert.Throws<SnapshotException>(() => Suggest(bytes)).Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0064: a file with nothing in it has nothing to suggest from, and is refused
    public void An_empty_file_is_refused()
    {
        Assert.Equal("The file is empty, so no Schema can be suggested.", Assert.Throws<SnapshotException>(() => Suggest("")).Message);
    }

    [Fact] // ADR-0064: a suggestion asked for with a cancelled token reads nothing
    public async Task A_cancelled_suggestion_reads_nothing()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var stream = new Trickle(Utf8("A\n1\n"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await CsvSchema.SuggestAsync(stream, null, cancel.Token));

        Assert.Equal(0, stream.Reads);
    }
}
