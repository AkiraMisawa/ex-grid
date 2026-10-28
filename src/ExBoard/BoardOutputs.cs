using System.Globalization;
using System.Text.Json;
using ExGrid.Data;

namespace ExBoard;

/// <summary>An output a Board declares (ADR-0340, Q6): what the user hands on, and the action that hands it.</summary>
/// <param name="Name">The output's name.</param>
/// <param name="From">The grid whose selected rows it takes.</param>
/// <param name="On">The columns whose values it hands on, declared by the grid's input.</param>
/// <param name="Confirm">The confirm action's label; nothing is raised but by this action.</param>
public sealed record BoardOutputDeclaration(string Name, string From, IReadOnlyList<string> On, string Confirm);

/// <summary>
/// What a Board hands on when the user confirms (ADR-0340, Q6): a value condition on an input, and the
/// version of the input's data it was taken against. The engine executes it where the data lives, or
/// takes a Snapshot of it (<see cref="Take"/>), and refuses a version that has moved on (spine rule 6):
/// the values the user chose are taken from the data they chose them in, or nothing is. It hands on
/// values, not rows: every row holding the chosen values in the output's columns is taken, so an output
/// meant to name single rows names a column that tells them apart.
/// </summary>
public sealed class BoardOutput
{
    private readonly Snapshot _takenFrom;
    private readonly SnapshotKind[] _kinds;

    internal BoardOutput(string name, string input, Snapshot takenFrom, LinkCondition condition, SnapshotKind[] kinds)
    {
        Name = name;
        Input = input;
        _takenFrom = takenFrom;
        _kinds = kinds;
        SourceVersion = takenFrom.Version;
        Condition = condition;
    }

    /// <summary>
    /// The output <paramref name="json"/> holds, as <see cref="ToJson"/> writes it and ExBoard's Script Core does too,
    /// taken against <paramref name="takenFrom"/>, the Snapshot the browser took it from (ADR-0370 section 1). Each value
    /// is read as its column of <paramref name="takenFrom"/> holds it, as a Board Input reads a row
    /// (<see cref="BoardInput.FromJson(string)"/>), so the values are the ones a Board bound in .NET would have handed on.
    /// </summary>
    internal static BoardOutput Read(string json, Snapshot takenFrom)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var name = Text(root.GetProperty("output"));
        var input = Text(root.GetProperty("input"));
        var version = root.GetProperty("version").GetInt64();
        if (version != takenFrom.Version)
            throw new InvalidOperationException($"Internal: output '{name}' was taken against version {version}, and the Snapshot it names is version {takenFrom.Version}.");
        string[] on = [.. root.GetProperty("on").EnumerateArray().Select(Text)];
        var columns = on.Select(column => takenFrom.TryGetColumn(column, out var held)
            ? new DataShapeColumn(column, held.Kind)
            : throw new InvalidOperationException($"Internal: output '{name}' hands on column '{column}', which the Snapshot it was taken from does not have.")).ToArray();
        // The values as rows of the output's columns, read back as a Board Input reads its rows.
        var rows = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(rows))
        {
            writer.WriteStartArray();
            foreach (var tuple in root.GetProperty("values").EnumerateArray())
            {
                if (tuple.GetArrayLength() != on.Length)
                    throw new InvalidOperationException($"Internal: output '{name}' holds a tuple of {tuple.GetArrayLength()} values on {on.Length} columns.");
                writer.WriteStartObject();
                var c = 0;
                foreach (var value in tuple.EnumerateArray())
                {
                    writer.WritePropertyName(on[c++]);
                    value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        var read = new BoardInput(input, columns).FromJson(System.Text.Encoding.UTF8.GetString(rows.WrittenSpan));
        var held = columns.Select(column => read[column.Name]).ToArray();
        var tuples = read.Rows.Select(row => (IReadOnlyList<object?>)[.. held.Select(column => read.ValueAt(row, column))]);
        return new BoardOutput(name, input, takenFrom, new LinkCondition(on, tuples), [.. columns.Select(column => column.Kind)]);
    }

    private static string Text(JsonElement json) => json.GetString() ?? throw new InvalidOperationException("Internal: an output names nothing where it names a text.");

    /// <summary>The output's name, as the Board declares it.</summary>
    public string Name { get; }

    /// <summary>The input the condition is on.</summary>
    public string Input { get; }

    /// <summary>The version of the input's data the condition was taken against (<see cref="Snapshot.Version"/>).</summary>
    public long SourceVersion { get; }

    /// <summary>The values handed on: each selected row's values in the output's columns, once.</summary>
    public LinkCondition Condition { get; }

    /// <summary>
    /// The rows of <paramref name="data"/> the condition lets through, in its order, as a Snapshot of
    /// their own.
    /// </summary>
    /// <exception cref="BoardRefusedException"><paramref name="data"/> is of another version than the
    /// one the output was taken against; is other data at version 0, which names no version, so that only
    /// the very data it was taken from can vouch for it; or lacks a column the condition names, or holds
    /// it of another kind.</exception>
    public Snapshot Take(Snapshot data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!ReferenceEquals(data, _takenFrom))
        {
            if (data.Version != SourceVersion)
            {
                throw new BoardRefusedException($"outputs.{Name}", FormattableString.Invariant(
                    $"Output '{Name}' was taken against version {SourceVersion} of input '{Input}', and the data is version {data.Version}; the values the user chose may no longer be these."));
            }
            if (SourceVersion == 0)
            {
                throw new BoardRefusedException($"outputs.{Name}",
                    $"Output '{Name}' was taken against input '{Input}' at version 0, which names no version, so only that same data can be taken; a source that moves on gives its data a version.");
            }
        }
        for (var i = 0; i < Condition.On.Count; i++)
        {
            var column = Condition.On[i];
            if (!data.TryGetColumn(column, out var held))
                throw new BoardRefusedException($"outputs.{Name}", $"Output '{Name}' hands on column '{column}', which the data of input '{Input}' does not have.");
            if (held.Kind != _kinds[i])
            {
                throw new BoardRefusedException($"outputs.{Name}",
                    $"Output '{Name}', column '{column}': chosen as a {BoardInput.NameOf(_kinds[i])}, and the data holds a {BoardInput.NameOf(held.Kind)}.");
            }
        }
        return Narrowing.Copy(data, Narrowing.Positions(data, [Condition]));
    }

    /// <summary>
    /// The output as JSON, so that any engine can take it:
    /// <c>{"output": "chosen", "input": "trades", "version": 3, "on": ["tradeId"], "values": [["T-1"], ["T-7"]]}</c>.
    /// A value is written as the input holds it: text and numbers as JSON's, a date as
    /// <c>yyyy-MM-dd</c> or, with a time of day, <c>yyyy-MM-ddTHH:mm:ss</c> and its fraction of a second
    /// to the tick (as a Board Input reads one), a boolean as one, a Blank as null,
    /// and a double that is no number (NaN, an infinity) as text, which JSON's numbers cannot hold.
    /// </summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("output", Name);
            json.WriteString("input", Input);
            json.WriteNumber("version", SourceVersion);
            json.WriteStartArray("on");
            foreach (var column in Condition.On)
                json.WriteStringValue(column);
            json.WriteEndArray();
            json.WriteStartArray("values");
            foreach (var tuple in Condition.Values)
            {
                json.WriteStartArray();
                foreach (var value in tuple)
                    Write(json, value);
                json.WriteEndArray();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null:
                json.WriteNullValue();
                break;
            case string text:
                json.WriteStringValue(text);
                break;
            case decimal number:
                json.WriteNumberValue(number);
                break;
            case double number when double.IsFinite(number):
                json.WriteNumberValue(number);
                break;
            case double number:
                // JSON has no NaN or infinity: written as .NET writes them, never as a number.
                json.WriteStringValue(number.ToString(CultureInfo.InvariantCulture));
                break;
            case long number:
                json.WriteNumberValue(number);
                break;
            case bool flag:
                json.WriteBooleanValue(flag);
                break;
            case DateTime date:
                json.WriteStringValue(date.ToString(date.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"A {value.GetType().Name} is no value an input holds.");
        }
    }
}
