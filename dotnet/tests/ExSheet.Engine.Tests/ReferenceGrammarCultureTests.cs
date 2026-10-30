using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// SH-6, ADR-0047: the Reference grammar — <c>A1</c>, <c>$A$1</c>, <c>A$1</c>, <c>A1:B2</c>,
/// <c>A:A</c>, <c>1:1</c>, a Sheet qualifier, structured references — parses and round-trips in the
/// invariant syntax in every culture. Each form is entered on a Sheet declared in the culture, with
/// the thread in it too, then read back: as entered, through a Sheet Document and its JSON, and
/// written again after a row inserted above it moved what it names. Here, under the cultures only .NET holds data for,
/// with the process in that culture too; the cultures both engines hold are shared cases.
/// </summary>
public class ReferenceGrammarCultureTests
{
    // The cultures only .NET holds data for: the invariant culture, en-US, de-DE, fr-FR and ja-JP are shared cases
    // (tests/cases/sheet/reference-grammar-culture.json), which the Script Core runs too.
    private static readonly string[] Cultures = ["ar-SA", "tr-TR"];

    /// <summary>
    /// The forms: the Sheet's name, the Formula as typed, as written back, and as written after a row
    /// is inserted at the top, and its Value over the data in <see cref="Fill"/>.
    /// </summary>
    private static readonly (string Sheet, string Typed, string Written, string AfterInsert, double Value)[] Forms =
    [
        (Sheet.DefaultName, "=A1", "=A1", "=A2", 1),
        (Sheet.DefaultName, "=$A$1", "=$A$1", "=$A$2", 1),
        (Sheet.DefaultName, "=A$1", "=A$1", "=A$2", 1),
        (Sheet.DefaultName, "=$B1", "=$B1", "=$B2", 4),
        (Sheet.DefaultName, "=SUM(A1:B2)", "=SUM(A1:B2)", "=SUM(A2:B3)", 15),
        (Sheet.DefaultName, "=SUM($A$1:B$2)", "=SUM($A$1:B$2)", "=SUM($A$2:B$3)", 15),
        (Sheet.DefaultName, "=SUM(A:A)", "=SUM(A:A)", "=SUM(A:A)", 3),
        (Sheet.DefaultName, "=SUM($A:B)", "=SUM($A:B)", "=SUM($A:B)", 15),
        (Sheet.DefaultName, "=SUM(1:1)", "=SUM(1:1)", "=SUM(2:2)", 5),
        (Sheet.DefaultName, "=SUM($1:2)", "=SUM($1:2)", "=SUM($2:3)", 15),
        (Sheet.DefaultName, "=Sheet1!A1", "=Sheet1!A1", "=Sheet1!A2", 1),
        (Sheet.DefaultName, "=SUM(Sheet1!$B$2:C3)", "=SUM(Sheet1!$B$2:C3)", "=SUM(Sheet1!$B$3:C4)", 24),
        ("Q 3", "='Q 3'!B2*2", "='Q 3'!B2*2", "='Q 3'!B3*2", 16),
        ("It's", "=SUM('It''s'!A:A)", "=SUM('It''s'!A:A)", "=SUM('It''s'!A:A)", 3),
        (Sheet.DefaultName, "=SUM(Positions[PV])", "=SUM(Positions[PV])", "=SUM(Positions[PV])", 300.5),
        (Sheet.DefaultName, "=SUM(Positions[Net PV])", "=SUM(Positions[Net PV])", "=SUM(Positions[Net PV])", -50),
        (Sheet.DefaultName, "=SUM(Positions[Rate'#])", "=SUM(Positions[Rate'#])", "=SUM(Positions[Rate'#])", 1.5),
        (Sheet.DefaultName, "=SUM(Positions[PV])*$A$1+SUM(Sheet1!A1:A2,1:1)", "=SUM(Positions[PV])*$A$1+SUM(Sheet1!A1:A2,1:1)", "=SUM(Positions[PV])*$A$2+SUM(Sheet1!A2:A3,2:2)", 308.5),
        // Typed in lower case, written in the invariant upper case: under tr-TR, 'i' upper-cased by the
        // culture is 'İ', which no column is.
        (Sheet.DefaultName, "=sum(i3,$i$3:i$3)", "=SUM(I3,$I$3:I$3)", "=SUM(I4,$I$4:I$4)", 64),
        // A Sheet's name and a Linked Table's, upper case with an 'I', typed in lower case: matched
        // without the culture's case rules — under tr-TR, 'I' and 'i' are not one letter's two cases.
        // The qualifier is written in the Sheet's own case, and a structured reference in its table's
        // and its column's declared case (ticket 180): LIMIT with a column Line.
        ("RISK", "=risk!i3", "=RISK!I3", "=RISK!I4", 32),
        (Sheet.DefaultName, "=sum(limit[line])", "=SUM(LIMIT[Line])", "=SUM(LIMIT[Line])", 7),
    ];

    public static TheoryData<string, string, string, string, string, double> FormsInCultures()
    {
        var data = new TheoryData<string, string, string, string, string, double>();
        foreach (var culture in Cultures)
        {
            foreach (var form in Forms) data.Add(culture, form.Sheet, form.Typed, form.Written, form.AfterInsert, form.Value);
        }
        return data;
    }

    /// <summary>A1=1, A2=2, B1=4, B2=8, C3=16, I3=32, and the Linked Tables Positions and LIMIT.</summary>
    private static void Fill(Sheet sheet)
    {
        foreach (var (address, number) in new[] { ("A1", "1"), ("A2", "2"), ("B1", "4"), ("B2", "8"), ("C3", "16"), ("I3", "32") })
        {
            sheet.Enter(CellAddress.Parse(address), number);
        }
        PushPositions(sheet);
    }

    private static void PushPositions(Sheet sheet)
    {
        sheet.DeclareLinkedTable("LIMIT", ["Line"]);
        PushLimit(sheet);
        sheet.DeclareLinkedTable("Positions", ["Id", "PV", "Net PV", "Rate#"]);
        sheet.PushLinkedTable("Positions",
        [
            [Value.FromText("R-1"), Value.FromNumber(100), Value.FromNumber(-20), Value.FromNumber(0.5)],
            [Value.FromText("R-2"), Value.FromNumber(200.5), Value.FromNumber(-30), Value.FromNumber(1)],
        ]);
    }

    private static readonly CellAddress At = CellAddress.Parse("H10");

    private static readonly CellAddress AtAfterInsert = CellAddress.Parse("H11");

    [Theory] // SH-6, ADR-0047: each Reference form parses, and its text round-trips exactly, in every culture
    [MemberData(nameof(FormsInCultures))]
    public void SH6_each_reference_form_round_trips_in_the_invariant_syntax_in_every_culture(
        string culture, string sheetName, string typed, string written, string afterInsert, double value)
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        var declared = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentCulture = declared;
        CultureInfo.CurrentUICulture = declared;
        try
        {
            var sheet = new Sheet(declared, sheetName);
            Fill(sheet);

            sheet.Enter(At, typed);

            // Parsed: it names what it says, read in no culture's way.
            Assert.Equal(written, sheet.GetEntryText(At));
            var computed = sheet.GetValue(At);
            Assert.NotNull(computed);
            Assert.Equal(ValueKind.Number, computed.Value.Kind);
            Assert.Equal(value, computed.Value.Number);

            // Through a Sheet Document and its JSON, opened again.
            var json = sheet.ToDocument().ToJson();
            var reopened = Sheet.Open(SheetDocument.FromJson(json));
            Assert.Equal(declared.Name, reopened.Culture.Name);
            Assert.Equal(written, reopened.GetEntryText(At));
            PushPositions2(reopened);
            PushLimit(reopened);
            Assert.Equal(computed, reopened.GetValue(At));

            // Written again: a row inserted at the top moves what it names, and the text is the invariant one.
            sheet.InsertRows(0);
            Assert.Equal(afterInsert, sheet.GetEntryText(AtAfterInsert));
            Assert.Equal(computed, sheet.GetValue(AtAfterInsert));
            var again = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));
            Assert.Equal(afterInsert, again.GetEntryText(AtAfterInsert));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private static void PushLimit(Sheet sheet) =>
        sheet.PushLinkedTable("LIMIT", [[Value.FromNumber(3)], [Value.FromNumber(4)]]);

    /// <summary>A reopened Sheet has its Linked Tables declared, waiting for their first snapshot (ADR-0049).</summary>
    private static void PushPositions2(Sheet sheet) =>
        sheet.PushLinkedTable("Positions",
        [
            [Value.FromText("R-1"), Value.FromNumber(100), Value.FromNumber(-20), Value.FromNumber(0.5)],
            [Value.FromText("R-2"), Value.FromNumber(200.5), Value.FromNumber(-30), Value.FromNumber(1)],
        ]);
}
