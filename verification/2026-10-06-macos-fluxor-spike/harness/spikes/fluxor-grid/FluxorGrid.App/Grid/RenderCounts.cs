using System.Text.Json;

namespace FluxorGrid.Grid;

/// <summary>Row renders and mounts by trade id, one per circuit (scoped). Touched only on the
/// renderer's context, so it needs no lock.</summary>
public sealed class RenderCounts
{
    private readonly Dictionary<string, int> _renders = new();
    private readonly Dictionary<string, int> _mounts = new();

    public int TotalRenders { get; private set; }

    public int TotalMounts { get; private set; }

    public void Rendered(string id)
    {
        _renders[id] = _renders.GetValueOrDefault(id) + 1;
        TotalRenders++;
    }

    public void Mounted(string id)
    {
        _mounts[id] = _mounts.GetValueOrDefault(id) + 1;
        TotalMounts++;
    }

    public void Reset()
    {
        _renders.Clear();
        _mounts.Clear();
        TotalRenders = 0;
        TotalMounts = 0;
    }

    public string ToJson() => JsonSerializer.Serialize(new
    {
        totalRenders = TotalRenders,
        totalMounts = TotalMounts,
        renders = _renders,
        mounts = _mounts,
    });
}
