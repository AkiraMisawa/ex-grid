using Bunit;
using Microsoft.JSInterop.Infrastructure;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// A browser's answer the test gives when it chooses: how a round trip on a circuit is staged
/// (ADR-0012), held for as long as the test holds it. Every call it matches from its setup on waits
/// for the answer; once given, the calls after it get it at once, and a later answer is the next
/// calls' (as bUnit's own handler does).
/// </summary>
/// <remarks>
/// bUnit's own handler for an answer not yet given fails the call once its wait timeout has passed
/// since the call (<see cref="BunitContext.DefaultWaitTimeout"/>, ten seconds in these suites):
/// an outcome set by the clock and by whatever else the process is running, not by the test. A whole
/// suite at 32 threads on four cores holds a test's next step for longer than that now and then — a
/// wait in the same runs failed having checked nothing in ten seconds. With that timeout shrunk to
/// 1 ms, tests holding an answer failed on the clock alone: the call threw
/// <c>JSRuntimeInvocationNotSetException</c> (EditingNotificationTests, ScrollBurstTests), a press
/// held behind the key gate was answered as if the gate had replied (FormulaBarEditingTests,
/// FormulaBarChromeTests), a caret placement held for a second press landed first
/// (ReferenceCycleTests). A browser does not give up on a round trip. The continuation the answer
/// wakes is posted to the renderer's dispatcher, wherever the answer is given from, as with
/// bUnit's own handler.
/// </remarks>
internal sealed class HeldJSAnswer<TResult> : JSRuntimeInvocationHandlerBase<TResult>
{
    private readonly bool _void;
    private TaskCompletionSource<TResult> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HeldJSAnswer(string identifier, bool isVoid)
        : base(invocation => invocation.Identifier == identifier, isCatchAllHandler: false)
    {
        _void = isVoid;
        // The base's own answer is given at once, so it never starts its timer: what a call waits
        // for is this handler's answer.
        SetResultBase(default!);
    }

    /// <summary>Stands up an answer the browser has not given yet for every <paramref name="identifier"/>
    /// call <paramref name="interop"/> hears from here on.</summary>
    internal static HeldJSAnswer<TResult> Setup(BunitJSInterop interop, string identifier)
    {
        var held = new HeldJSAnswer<TResult>(identifier, isVoid: typeof(TResult) == typeof(IJSVoidResult));
        interop.AddInvocationHandler(held);
        return held;
    }

    /// <inheritdoc/>
    public override bool IsVoidResultHandler => _void;

    /// <summary>The browser answers <paramref name="result"/>.</summary>
    public void SetResult(TResult result)
    {
        if (_answer.Task.IsCompleted)
            _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _answer.SetResult(result);
    }

    /// <summary>The browser has carried out a call that returns nothing.</summary>
    public void SetVoidResult() => SetResult(default!);

    /// <inheritdoc/>
    protected override Task<TResult> HandleAsync(JSRuntimeInvocation invocation)
    {
        // Recorded as bUnit records it; its own task is already answered, and not the one waited on.
        _ = base.HandleAsync(invocation);
        return _answer.Task;
    }
}
