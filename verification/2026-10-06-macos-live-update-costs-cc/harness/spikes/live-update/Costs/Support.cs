using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace Costs;

/// <summary>Private members, reached by name: this harness times steps that are not public, and
/// src/ is not changed for it. A member that is renamed fails here by name.</summary>
public static class Reflect
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static MethodInfo Method(Type type, string name)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            var found = t.GetMethods(Any | BindingFlags.DeclaredOnly).Where(m => m.Name == name).ToArray();
            if (found.Length == 1)
                return found[0];
            if (found.Length > 1)
                throw new InvalidOperationException($"{type.Name}.{name} is overloaded: {found.Length} methods.");
        }
        throw new InvalidOperationException($"{type.Name} has no method {name}.");
    }

    public static FieldInfo Field(Type type, string name)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t.GetField(name, Any | BindingFlags.DeclaredOnly) is { } field)
                return field;
        }
        throw new InvalidOperationException($"{type.Name} has no field {name}.");
    }

    public static object? Get(object target, string field) => Field(target.GetType(), field).GetValue(target);

    public static T Get<T>(object target, string field) => (T)Get(target, field)!;

    public static void Set(object target, string field, object? value) => Field(target.GetType(), field).SetValue(target, value);

    public static object? Call(object target, string method, params object?[] args)
        => Method(target.GetType(), method).Invoke(target, args);

    /// <summary>A component's StateHasChanged, which is protected.</summary>
    public static void StateHasChanged(IComponent component)
        => typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component, null);
}

/// <summary>A series of timings: the least is what is read (the ticket's method), the median and
/// the most beside it, so a wide spread shows.</summary>
public sealed record Stat(int Runs, double Min, double Median, double Max, double[] All)
{
    public static Stat Of(IReadOnlyCollection<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return new Stat(sorted.Length, sorted[0], sorted[sorted.Length / 2], sorted[^1], [.. values.Select(v => Math.Round(v, 4))]);
    }

    public override string ToString() => $"{Min,9:F3} {Median,9:F3} {Max,9:F3}";
}

public static class Clock
{
    public static double Ms(long from) => (Stopwatch.GetTimestamp() - from) * 1000.0 / Stopwatch.Frequency;

    /// <summary>Times <paramref name="action"/> <paramref name="runs"/> times after
    /// <paramref name="warmup"/> untimed calls, a full collection before each.</summary>
    public static Stat Repeat(int warmup, int runs, Action action)
    {
        for (var i = 0; i < warmup; i++)
            action();
        var times = new List<double>(runs);
        for (var i = 0; i < runs; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var t0 = Stopwatch.GetTimestamp();
            action();
            times.Add(Ms(t0));
        }
        return Stat.Of(times);
    }

    public static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

/// <summary>What the machine was doing when a batch ran: its load average and the busiest
/// processes, written beside the numbers.</summary>
public static class Machine
{
    public static object Snapshot()
    {
        return new
        {
            at = DateTimeOffset.Now.ToString("O"),
            loadavg = Run("sysctl", "-n vm.loadavg").Trim(),
            top = Run("/bin/sh", "-c \"ps -axo pcpu,etime,command -r 2>/dev/null | head -12 | cut -c1-160\"").Split('\n', StringSplitOptions.RemoveEmptyEntries),
            tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            gc = System.Runtime.GCSettings.IsServerGC ? "server" : "workstation",
        };
    }

    private static string Run(string file, string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(file, args) { RedirectStandardOutput = true, UseShellExecute = false })!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
        catch (Exception error)
        {
            return $"unavailable: {error.Message}";
        }
    }

    public static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"wrote {path}");
    }
}
