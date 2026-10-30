using System.Collections.Concurrent;
using Xunit;

[assembly: AssemblyFixture(typeof(ExGrid.Testing.UnobservedTaskWatch))]

namespace ExGrid.Testing;

/// <summary>
/// CON-7 and WR-9: no unobserved <see cref="Task"/> exception, anywhere in a layer-2 suite.
/// Compiled into each test project that references bUnit (dotnet/tests/Directory.Build.targets), so
/// the core's suite and every Wrapper's suite are watched alike.
/// </summary>
/// <remarks>
/// The handler is installed before the first test of the assembly runs and stays for all of
/// them. When the last test has finished, a forced collection finalises every faulted task
/// nobody awaited, and the fixture's disposal fails the run, naming each exception, if any was
/// reported. A single scripted session (UnobservedExceptionTests) sees only its own tasks;
/// this sees the other two thousand tests' as well. Which test leaked a task is not known at
/// that point — the finalizer runs long after — so the report carries the exceptions' own
/// stack traces, and the place is found from those.
/// </remarks>
public sealed class UnobservedTaskWatch : IDisposable
{
    private readonly ConcurrentQueue<Exception> _unobserved = new();

    /// <summary>Installs the handler, before any test of the assembly runs.</summary>
    public UnobservedTaskWatch() => TaskScheduler.UnobservedTaskException += OnUnobserved;

    private void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs e) => _unobserved.Enqueue(e.Exception);

    /// <summary>Forces the collection at the end of the suite and fails on any event.</summary>
    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        TaskScheduler.UnobservedTaskException -= OnUnobserved;
        if (!_unobserved.IsEmpty)
        {
            throw new InvalidOperationException(
                $"CON-7: {_unobserved.Count} unobserved Task exception(s) in this suite:{Environment.NewLine}"
                + string.Join(Environment.NewLine + "---" + Environment.NewLine, _unobserved));
        }
    }
}
