namespace ExGrid.Components;

/// <summary>
/// The per-instance id prefix every grid writes (ADR-0018/0033). The counter lives here, outside the
/// generic <c>ExGrid&lt;TRow&gt;</c>, because a static field of a generic type is one per closed type:
/// an <c>ExGrid&lt;SheetRow&gt;</c> and an <c>ExGrid&lt;Position&gt;</c> on one page each counted from 1
/// and rendered the same root id (ticket 08 of the widened run).
/// </summary>
internal static class InstanceIds
{
    private static int Counter;

    /// <summary>The next prefix, unique in the process: <c>ex1-</c>, <c>ex2-</c>, …</summary>
    public static string Next() => FormattableString.Invariant($"ex{Interlocked.Increment(ref Counter)}-");
}
