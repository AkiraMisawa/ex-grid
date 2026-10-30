using System.Text.Json;
using ScriptFunction = global::ExGrid.Data.ScriptFunction;

namespace ExGrid.Components;

/// <summary>
/// A function of the Consumer's own script as the browser's half imports it, its module and its export (ADR-0420
/// section 3, ADR-0431; packages/blazor/src/script-functions.ts reads it). Compiled into each adapter that hands one
/// across, so every one names a function alike.
/// </summary>
internal static class ScriptFunctionJson
{
    /// <summary>Writes <paramref name="script"/> as a value, where a property or an array's item stands.</summary>
    public static void Write(Utf8JsonWriter json, ScriptFunction script)
    {
        json.WriteStartObject();
        json.WriteString("module", script.Module);
        json.WriteString("export", script.Export);
        json.WriteEndObject();
    }
}
