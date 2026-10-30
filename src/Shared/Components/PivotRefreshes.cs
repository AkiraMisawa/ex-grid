using ExPivot.Engine;

namespace ExGrid.Components;

/// <summary>
/// The Refreshes a Script Core pivot asked of its C# Pivot Sources and .NET is running, by the number the browser asks
/// each source under. A source that refreshes says it changed; that notice is the browser's own, which asks again once
/// the Refresh is done, so it is not passed on and the report is asked once, as the Razor pivot asks it (its question
/// after the Refresh answers the changes said meanwhile). A notice said after the Refresh is done is passed on. Read on
/// whatever thread a source says it changed on, and counted, so one of two Refreshes out at once ending does not pass
/// on the other's notice. Compiled into ExPivot's adapter and ExBoard's, as <see cref="PivotSourceCrossing"/> is.
/// </summary>
internal sealed class PivotRefreshes
{
    private readonly Dictionary<int, int> _out = [];
    private readonly Lock _lock = new();

    /// <summary>Whether a Refresh of the source asked under <paramref name="number"/> is out.</summary>
    public bool Out(int number)
    {
        lock (_lock)
            return _out.ContainsKey(number);
    }

    /// <summary>Refreshes <paramref name="source"/>, asked under <paramref name="number"/>, holding its notices back
    /// until it is done.</summary>
    public async Task RefreshAsync(int number, PivotSource source)
    {
        lock (_lock)
            _out[number] = _out.GetValueOrDefault(number) + 1;
        try
        {
            await source.RefreshAsync();
        }
        finally
        {
            lock (_lock)
            {
                if (_out[number] == 1)
                    _out.Remove(number);
                else
                    _out[number]--;
            }
        }
    }
}
