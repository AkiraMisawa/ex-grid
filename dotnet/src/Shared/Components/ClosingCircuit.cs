using Microsoft.JSInterop;

namespace ExGrid.Components;

/// <summary>
/// What a call to the browser throws when the page it would reach is gone (CON-6, ADR-0451): the circuit has closed, so
/// the call cannot cross; or the component has been disposed, and the closing circuit called the call off or the
/// reference it was made on was let go. Nothing is wrong then, and a call made in the renderer's own turn (a parameter's
/// change, a source's change, a notice, the disposal) lets go of it rather than raising it into the circuit, where the
/// host logs it as an unhandled exception. Any other failure is raised. Compiled into each adapter, so every one lets go
/// of the same things.
/// </summary>
internal static class ClosingCircuit
{
    /// <summary>Whether <paramref name="failure"/> says only that the page has gone.</summary>
    /// <param name="failure">What the call threw.</param>
    /// <param name="disposed">Whether the component that made the call has been disposed.</param>
    public static bool Ended(Exception failure, bool disposed) =>
        failure is JSDisconnectedException
        || (disposed && failure is OperationCanceledException or ObjectDisposedException);
}
