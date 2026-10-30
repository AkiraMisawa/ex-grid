namespace ExGrid.MudBlazor;

/// <summary>
/// What the parts MudGridChrome draws over the Script Core share (ADR-0410): the attributes that mark a control for
/// the adapter's script, and a value given to a control only as it is first drawn. A view arrives a round trip behind
/// what the user typed or ticked, and a value bound to it would write an older one back (SRV-7).
/// </summary>
internal static class ScriptSlotMarkup
{
    private static readonly IReadOnlyDictionary<string, object> None = new Dictionary<string, object>();

    /// <summary>A control's mark: one attribute and its value.</summary>
    internal static Dictionary<string, object?> Marked(string attribute, object value) => new() { [attribute] = value };

    /// <summary>A control's two marks.</summary>
    internal static Dictionary<string, object?> Marked(string attribute, object value, string other, object otherValue)
        => new() { [attribute] = value, [other] = otherValue };

    /// <summary>The component's <c>Value</c> where <paramref name="opens"/> is given, as the control is first drawn,
    /// and nothing after, so the control keeps what the user made of it.</summary>
    internal static IReadOnlyDictionary<string, object> ValueOnce(object? opens)
        => opens is null ? None : new Dictionary<string, object> { ["Value"] = opens };

    /// <summary>What a control opens with as it is drawn: <paramref name="current"/> the first time, and null while it
    /// stays drawn; nothing while it is not drawn, so that drawn again it opens again.</summary>
    internal static (T? Opens, bool Drawn) Opening<T>(bool shown, T current, bool drawn) where T : class
        => shown ? (drawn ? null : current, true) : (null, false);
}
