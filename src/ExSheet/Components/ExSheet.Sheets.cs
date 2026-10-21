using ExSheet.Engine;
using Microsoft.AspNetCore.Components;

namespace ExSheet.Components;

/// <summary>
/// Commands and addresses name their Sheet (ADR-0331 §6). One that names none is taken only while the
/// Workbook holds one Sheet; with more it is refused by name, because taken against the Sheet shown its
/// outcome would depend on whether a press on a tab reached the instance before it. A command that acts
/// on the Selection acts where the Selection is, on the Sheet shown, read with it when the command runs.
/// The application adds, deletes, renames, moves and copies Sheets with <see cref="DoAsync(string, SheetEdit)"/>,
/// each one step on the one undo stack, and hears every change to the Sheets through <see cref="SheetsChanged"/>.
/// </summary>
public partial class ExSheet
{
    // The Sheets' names as the application last heard them, so a change says what it was taken from.
    private IReadOnlyList<string> _sheetNames = [];

    /// <summary>
    /// Raised when a Sheet is added, deleted, renamed, moved or copied — by a command, by the user, or
    /// by an undo or redo of one — after <see cref="WorkbookChanged"/>, with the Sheets' names before
    /// and after the change (ADR-0331 §6). Showing another Sheet changes no Sheet, and is not raised.
    /// </summary>
    [Parameter] public EventCallback<SheetsChange> SheetsChanged { get; set; }

    /// <summary>
    /// A Consumer command: does <paramref name="edit"/> on the Sheet named <paramref name="sheet"/>, shown
    /// or not, as one operation on the user's undo stack, in its place in the order (ADR-0048, ADR-0331
    /// §6), and raises <see cref="WorkbookChanged"/>. A Sheet operation — <see cref="SheetEdit.InsertSheet"/>,
    /// <see cref="SheetEdit.Rename"/>, <see cref="SheetEdit.MoveSheet"/>, <see cref="SheetEdit.CopySheet"/>,
    /// <see cref="SheetEdit.DeleteSheet"/> — acts on that Sheet and raises <see cref="SheetsChanged"/>; the
    /// Sheet shown stays shown unless it is the one deleted, and then the Sheet that takes its place is.
    /// </summary>
    /// <exception cref="ArgumentException">No Sheet of the Workbook is named <paramref name="sheet"/>; nothing changed.</exception>
    /// <exception cref="SheetRefusedException">The Sheet refuses the operation, or an edit is open
    /// (<see cref="IsEditing"/>); nothing changed.</exception>
    /// <exception cref="FormulaSyntaxException">A typed Formula cannot be read; nothing changed.</exception>
    public Task DoAsync(string sheet, SheetEdit edit)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(edit);
        return InvokeAsync(async () =>
        {
            var target = NamedSheet(sheet);
            RefuseWhileEditing();
            var step = CurrentWorkbook.Do(target.Name, edit);
            _history.Record(step, target);
            await ChangedAsync([step.Change]);
        });
    }

    /// <summary>
    /// The Cell Format of the cell at <paramref name="address"/> on the Sheet named
    /// <paramref name="sheet"/>, shown or not, as it shows (<see cref="CellFormatAt(CellAddress)"/>).
    /// </summary>
    /// <exception cref="ArgumentException">No Sheet of the Workbook is named <paramref name="sheet"/>.</exception>
    public CellFormat CellFormatAt(string sheet, CellAddress address) => NamedSheet(sheet).GetCellFormat(address);

    private Sheet NamedSheet(string sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return CurrentWorkbook.Find(sheet) ?? throw new ArgumentException($"No Sheet of the Workbook is named '{sheet}'.", nameof(sheet));
    }

    /// <summary>A command or an address that names no Sheet, refused while the Workbook holds several (ADR-0331 §6).</summary>
    private void RefuseUnnamedSheet()
    {
        if (CurrentWorkbook.Sheets.Count > 1)
        {
            throw new SheetRefusedException(new SheetRefusal(SheetRefusalReason.SheetNotNamed, SheetWords.SheetNotNamed(CurrentWorkbook.Sheets.Count)));
        }
    }

    /// <summary>The Sheets' names as they stand, taken as the ones the application has heard: at open, the document's own.</summary>
    private void HearSheetNames() => _sheetNames = [.. CurrentWorkbook.Sheets.Select(s => s.Name)];

    /// <summary>Raises <see cref="SheetsChanged"/> when the Sheets' names, or their order, are no longer the ones last heard.</summary>
    private async Task RaiseSheetsChangedAsync()
    {
        var before = _sheetNames;
        HearSheetNames();
        if (before.SequenceEqual(_sheetNames, StringComparer.Ordinal)) return;
        await SheetsChanged.InvokeAsync(new SheetsChange(before, _sheetNames, CurrentWorkbook.Shown.Name));
    }
}
