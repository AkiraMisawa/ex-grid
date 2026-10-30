using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExGrid;

/// <summary>
/// The JSON form of a <see cref="GridViewState"/>, a <see cref="ColumnViewState"/> or a
/// <see cref="Columns.ColumnWidth"/> handed to <c>System.Text.Json</c> directly: none — refused by
/// name, pointing to <see cref="GridViewStateJson"/> (ADR-0207). Written directly, a View State came
/// back quietly wrong: an Auto width threw on the way out, every Fixed width read back as Auto, and
/// every operand as a <see cref="JsonElement"/> that compares equal to nothing (principle 1).
///
/// <para>Internal, so under reflection the attribute names it and it refuses, and under a Consumer's
/// source-generated context the generator cannot reach it: the build warns that it is not a
/// converter it can use (SYSLIB1220, naming this type) and generates nothing for the type
/// (SYSLIB1030), and the serializer refuses the type at run time. Neither route reads or writes it.
/// </para>
/// </summary>
/// <typeparam name="T">The View State's type it stands on.</typeparam>
internal sealed class OnlyThroughGridViewStateJson<T> : JsonConverter<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw Refusal();

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => throw Refusal();

    private static NotSupportedException Refusal() => new(
        $"A {typeof(T).Name} has no JSON form of its own: a View State is written with GridViewStateJson.Write and read "
        + "with GridViewStateJson.Read, which keep each filter operand's type and an Auto width as the intent (ADR-0207).");
}
