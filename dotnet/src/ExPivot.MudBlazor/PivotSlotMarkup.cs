namespace ExPivot.MudBlazor;

/// <summary>
/// What the parts MudPivotChrome draws over the Script Core share (ADR-0410): the attributes that mark a control for the
/// adapter's script, written as a component's <c>UserAttributes</c>.
/// </summary>
internal static class PivotSlotMarkup
{
    /// <summary>A control's mark: one attribute and its value.</summary>
    internal static Dictionary<string, object?> Marked(string attribute, object value) => new() { [attribute] = value };

    /// <summary>A control's two marks.</summary>
    internal static Dictionary<string, object?> Marked(string attribute, object value, string other, object otherValue)
        => new() { [attribute] = value, [other] = otherValue };

    /// <summary>A control's marks and the ARIA it carries beside them.</summary>
    internal static Dictionary<string, object?> With(this Dictionary<string, object?> marks, string attribute, object? value)
    {
        marks[attribute] = value;
        return marks;
    }
}
