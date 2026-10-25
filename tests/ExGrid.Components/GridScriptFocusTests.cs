using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0360: the grid's script, imported on its own where no grid stands, is refused once when it is not
/// this build's and is let go when its holder is, even while it is still loading.
/// </summary>
public class GridScriptFocusTests
{
    [Fact] // ADR-0360: a script with no stamp is another version's, refused once and let go
    public async Task ADR0360_a_script_with_no_stamp_is_refused_once_and_let_go()
    {
        var runtime = new ModuleRuntime(stamp: null);
        var focus = new GridScriptFocus(runtime, ResourceAssetCollection.Empty);

        Assert.False(await focus.FocusBoxAsync(default));
        Assert.False(await focus.FocusBoxAsync(default));

        Assert.Equal(1, runtime.Imports);
        Assert.Equal(1, runtime.Disposed);
        Assert.Equal(0, runtime.Focused);
    }

    [Fact] // ADR-0360: a load still on its way when the holder is disposed is let go once it lands
    public async Task ADR0360_a_load_on_its_way_at_disposal_is_let_go_when_it_lands()
    {
        var landing = new TaskCompletionSource();
        var runtime = new ModuleRuntime(ShippedAssets.ScriptStamp, landing.Task);
        var focus = new GridScriptFocus(runtime, ResourceAssetCollection.Empty);

        var loading = focus.LoadAsync();
        var disposing = focus.DisposeAsync().AsTask();
        landing.SetResult();
        await disposing;
        await loading;

        Assert.Equal(1, runtime.Disposed);
        Assert.False(await focus.FocusBoxAsync(default));
        Assert.Equal(1, runtime.Imports);
        Assert.Equal(0, runtime.Focused);
    }

    /// <summary>A runtime whose <c>import</c> answers a module stamped <c>stamp</c>, or one with no stamp
    /// export (its call throws) when it is null, once <c>landing</c> completes.</summary>
    private sealed class ModuleRuntime(string? stamp, Task? landing = null) : IJSRuntime
    {
        public int Imports { get; private set; }
        public int Disposed { get; set; }
        public int Focused { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Assert.Equal("import", identifier);
            Imports++;
            if (landing is not null)
                await landing;
            return (TValue)(object)new Module(this, stamp);
        }
    }

    private sealed class Module(ModuleRuntime runtime, string? stamp) : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "stamp")
            {
                return stamp is null
                    ? throw new JSException("stamp is not a function")
                    : ValueTask.FromResult((TValue)(object)stamp);
            }
            Assert.Equal("focusBox", identifier);
            runtime.Focused++;
            return ValueTask.FromResult((TValue)(object)true);
        }

        public ValueTask DisposeAsync()
        {
            runtime.Disposed++;
            return ValueTask.CompletedTask;
        }
    }
}
