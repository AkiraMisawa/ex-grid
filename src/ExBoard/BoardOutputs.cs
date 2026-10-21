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
/// the rows the user saw are the rows handed on, or nothing is.
/// </summary>
public sealed class BoardOutput
{
    internal BoardOutput(string name, string input, long sourceVersion, LinkCondition condition)
    {
        Name = name;
        Input = input;
        SourceVersion = sourceVersion;
        Condition = condition;
    }

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
    /// one the output was taken against, or lacks a column the condition names.</exception>
    public Snapshot Take(Snapshot data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Version != SourceVersion)
        {
            throw new BoardRefusedException($"outputs.{Name}", FormattableString.Invariant(
                $"Output '{Name}' was taken against version {SourceVersion} of input '{Input}', and the data is version {data.Version}; the rows the user chose may no longer be these."));
        }
        foreach (var column in Condition.On)
        {
            if (!data.TryGetColumn(column, out _))
                throw new BoardRefusedException($"outputs.{Name}", $"Output '{Name}' hands on column '{column}', which the data of input '{Input}' does not have.");
        }
        return Narrowing.Copy(data, Narrowing.Positions(data, [Condition]));
    }

    /// <summary>
    /// The output as JSON, so that any engine can take it:
    /// <c>{"output": "chosen", "input": "trades", "version": 3, "on": ["tradeId"], "values": [["T-1"], ["T-7"]]}</c>.
    /// A value is written as the input holds it: text and numbers as JSON's, a date as
    /// <c>yyyy-MM-dd</c> or, with a time of day, <c>yyyy-MM-ddTHH:mm:ss</c>, a boolean as one, a Blank as null,
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
                json.WriteStringValue(date.ToString(date.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"A {value.GetType().Name} is no value an input holds.");
        }
    }
}
