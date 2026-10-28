using System.Runtime.ExceptionServices;
using System.Text.Json;
using ExSheet.Engine;
using Microsoft.JSInterop;

namespace ExSheet.Script;

/// <summary>
/// Opening and saving a file in the Input Order (ADR-0330 §10), as the Razor component does: the Sheet in the browser
/// places the opening or the save among its inputs, and calls back into .NET for the read, or for the write, in its
/// turn. The application's read and write run here, in .NET, where the application's file formats live; what crosses
/// is a Workbook Document, and for a save the Workbook's state beside it, so that the Workbook .NET writes shows the
/// Values the browser shows.
/// </summary>
public sealed partial class ExSheet
{
    // The openings and saves under way, by the number each crossed under: the browser calls back with it.
    private readonly Dictionary<int, Opening> _openings = [];
    private readonly Dictionary<int, Saving> _savings = [];
    private int _files;

    private sealed class Opening(Func<CancellationToken, Task<WorkbookDocument>> read) : IDisposable
    {
        public Func<CancellationToken, Task<WorkbookDocument>> Read { get; } = read;
        public CancellationTokenSource Cancel { get; } = new();
        public Exception? Failure { get; set; }
        public void Dispose() => Cancel.Dispose();
    }

    private sealed class Saving(Func<global::ExSheet.Engine.Workbook, Task> write)
    {
        public Func<global::ExSheet.Engine.Workbook, Task> Write { get; } = write;
        public bool Written { get; set; }
        public Exception? Failure { get; set; }
    }

    /// <summary>
    /// Replaces the Workbook with the Workbook Document <paramref name="read"/> answers, taken at this call's place in
    /// the Input Order of the Sheet in the browser, as the Razor component's <see cref="Components.ExSheet.OpenAsync"/>
    /// does (ADR-0330 §10): the inputs made before it are taken against the Workbook they were made on, and those made
    /// after it wait until the document arrives. <paramref name="read"/> runs in .NET, in the opening's turn; a plain
    /// Escape typed in the Sheet while the opening shows cancels its token, and the Workbook stays as it was. An edit
    /// still open in the opening's turn refuses it by name, and nothing is read. When the document arrives it replaces
    /// the whole Workbook: the undo stack is cleared, the Linked Tables declared are gone with it, and
    /// <see cref="WorkbookChanged"/> (and <see cref="DocumentChanged"/> while it holds one Sheet) is raised with the
    /// Workbook opened, as the Sheet's other changes are, after the opening ends.
    /// </summary>
    /// <param name="read">Reads the document, looking at the token as it goes; a read that sees it cancelled ends in
    /// <see cref="OperationCanceledException"/>.</param>
    /// <returns><see langword="true"/> once the document opened; <see langword="false"/> when Escape cancelled the
    /// read, or the Sheet was disposed first or meanwhile.</returns>
    /// <exception cref="SheetRefusedException">An edit is open; nothing was read.</exception>
    /// <exception cref="InvalidOperationException">Called from a <see cref="ToolbarButton"/>'s <c>OnClick</c>, whose press
    /// holds the Input Order the opening would wait for.</exception>
    /// <exception cref="SheetDocumentException">The document is one the engine in the browser does not read; nothing
    /// opened.</exception>
    /// <exception cref="Exception">Whatever <paramref name="read"/> failed with; nothing opened.</exception>
    public async Task<bool> OpenAsync(Func<CancellationToken, Task<WorkbookDocument>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        RefuseInsidePress(nameof(OpenAsync));
        if (_disposed)
            return false;
        var id = ++_files;
        using var opening = new Opening(read);
        _openings[id] = opening;
        try
        {
            var sheet = await ReadyAsync();
            // The read may wait behind the user's inputs and take as long as the file does: no timeout of the
            // circuit's cuts it short.
            var answer = await AskAsync(sheet, "open", CancellationToken.None, [id]);
            // Disposed meanwhile: whatever the browser answered, nothing landed that anyone will see.
            if (_disposed)
                return false;
            // The browser's answer decides how the opening ended, as the Sheet said it: a read that failed as an
            // Escape cancelled it ended cancelled; one that failed otherwise is thrown as it was.
            if (answer.TryGetProperty("thrown", out _) && opening.Failure is { } failure)
                ExceptionDispatchInfo.Throw(failure);
            return Answered(answer).GetBoolean();
        }
        catch (Exception) when (_disposed)
        {
            return false;
        }
        finally
        {
            _openings.Remove(id);
        }
    }

    /// <summary>
    /// Saves the Workbook at this call's place in the Input Order of the Sheet in the browser, as the Razor component's
    /// <see cref="Components.ExSheet.SaveAsync"/> does (ADR-0330 §10): <paramref name="write"/> is handed the Workbook
    /// as the inputs made before the call left it — every Sheet with its Values as the browser shows them, its Cell
    /// Formats and widths, and the rows each Linked Table holds. The Workbook is .NET's copy of the browser's, made for
    /// this call: the Workbook Document, each Linked Table's rows, the seed, the count of changes, the Sheet Day and
    /// the moment <c>NOW</c> reads, so that both engines compute the same Values. The inputs made after the save are
    /// taken as soon as the browser has handed the Workbook over, not once the write ends.
    /// </summary>
    /// <param name="write">Reads the Workbook, and writes it where the application wants it.</param>
    /// <returns>A task that completes once <paramref name="write"/>'s task has.</returns>
    /// <exception cref="SheetRefusedException">An edit is open: the file would quietly lack what is being typed. The
    /// Sheet says so, and nothing was written.</exception>
    /// <exception cref="ObjectDisposedException">The Sheet was disposed before the save's turn; nothing was written.</exception>
    /// <exception cref="InvalidOperationException">Called from a <see cref="ToolbarButton"/>'s <c>OnClick</c>, whose press
    /// holds the Input Order the save would wait for.</exception>
    /// <exception cref="Exception">Whatever <paramref name="write"/> failed with.</exception>
    public async Task SaveAsync(Func<global::ExSheet.Engine.Workbook, Task> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        RefuseInsidePress(nameof(SaveAsync));
        var sheet = await ReadyAsync();
        var id = ++_files;
        var saving = new Saving(write);
        _savings[id] = saving;
        try
        {
            var answer = await AskAsync(sheet, "save", CancellationToken.None, [id]);
            if (answer.TryGetProperty("thrown", out _) && saving.Failure is { } failure)
                ExceptionDispatchInfo.Throw(failure);
            Answered(answer);
        }
        catch (JSDisconnectedException) when (_disposed && !saving.Written)
        {
            throw new ObjectDisposedException(GetType().Name, "The Sheet was disposed before the save's turn: nothing was written.");
        }
        finally
        {
            _savings.Remove(id);
        }
    }

    /// <summary>The read of an opening, in its turn in the browser: its document, or that it failed or saw its token
    /// cancelled. The failure is kept to be thrown from <see cref="OpenAsync"/> as it was, since what crosses a circuit
    /// is a message.</summary>
    internal async Task<string> ReadAsync(int id)
    {
        if (!_openings.TryGetValue(id, out var opening))
            throw new InvalidOperationException($"Internal: the browser asked for the read of an opening {id} that is not under way.");
        var token = opening.Cancel.Token;
        try
        {
            var document = await opening.Read(token) ?? throw new InvalidOperationException("The read answered no Workbook Document.");
            return Json(json =>
            {
                json.WriteStartObject();
                json.WriteString("document", document.ToJson());
                json.WriteEndObject();
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return """{"cancelled":true}""";
        }
        catch (Exception failed)
        {
            opening.Failure = failed;
            return Failed(failed);
        }
    }

    // A read's or a write's failure as the browser is told it: its message, which the Sheet says.
    private static string Failed(Exception failed) => Json(json =>
    {
        json.WriteStartObject();
        json.WriteString("failed", failed.Message);
        json.WriteEndObject();
    });

    /// <summary>An Escape typed against an opening the page showed: its read's token is cancelled.</summary>
    internal void CancelRead(int id)
    {
        if (_openings.TryGetValue(id, out var opening))
            opening.Cancel.Cancel();
    }

    /// <summary>The write of a save, in its turn in the browser: the Workbook rebuilt from what the browser handed over,
    /// lent to the application's write.</summary>
    internal async Task<string> WriteAsync(int id, JsonElement saved)
    {
        if (!_savings.TryGetValue(id, out var saving))
            throw new InvalidOperationException($"Internal: the browser asked for the write of a save {id} that is not under way.");
        try
        {
            // Written even when the Sheet is being disposed, as it was handed over before.
            saving.Written = true;
            await saving.Write(WorkbookOf(saved));
            return """{"done":true}""";
        }
        catch (Exception failed)
        {
            saving.Failure = failed;
            return Failed(failed);
        }
    }

    // The browser's Workbook in .NET: opened from its document, then given each Linked Table's rows, the moment NOW
    // reads, the Sheet Day, and last the seed and the count of changes the draws of RAND are made from (ADR-0180), so
    // that every Value is the one the browser shows.
    private static global::ExSheet.Engine.Workbook WorkbookOf(JsonElement root)
    {
        var workbook = global::ExSheet.Engine.Workbook.Open(WorkbookDocument.FromJson(Text(root.GetProperty("document"))));
        foreach (var table in root.GetProperty("tables").EnumerateArray())
        {
            if (table.GetProperty("rows") is { ValueKind: JsonValueKind.String } rows)
                workbook.PushLinkedTable(Text(table.GetProperty("name")), SheetWire.ReadRows(Text(rows)));
        }
        if (root.GetProperty("now") is { ValueKind: JsonValueKind.Object } now)
        {
            var moment = new DateTime(
                now.GetProperty("year").GetInt32(), now.GetProperty("month").GetInt32(), now.GetProperty("day").GetInt32(),
                now.GetProperty("hour").GetInt32(), now.GetProperty("minute").GetInt32(), now.GetProperty("second").GetInt32(),
                now.GetProperty("millisecond").GetInt32(), DateTimeKind.Unspecified);
            workbook.NowSource = () => moment;
            workbook.RecalculateVolatile();
        }
        workbook.SetToday(root.GetProperty("today") is { ValueKind: JsonValueKind.Object } today
            ? new DateOnly(today.GetProperty("year").GetInt32(), today.GetProperty("month").GetInt32(), today.GetProperty("day").GetInt32())
            : null);
        workbook.SetSeed(
            root.GetProperty("seed") is { ValueKind: JsonValueKind.String } seed ? ulong.Parse(Text(seed), System.Globalization.CultureInfo.InvariantCulture) : null,
            root.GetProperty("changes").GetInt64());
        return workbook;
    }
}
