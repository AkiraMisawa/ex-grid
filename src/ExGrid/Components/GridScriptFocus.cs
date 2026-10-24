using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

/// <summary>
/// A box's first control taking the keyboard where no grid stands to hand it on (ADR-0360): the
/// grid's script, imported on its own, finds the control inside the box as a grid hands the keyboard
/// to one (<see cref="ExGrid{TRow}.HandKeyboardToAsync(ElementReference)"/>) and focuses it. ExPivot
/// holds one while its report is not drawn, when a Chrome's panel names a box that cannot take focus
/// itself.
/// </summary>
/// <remarks>Not the contract (ADR-0201): a sibling product calls it, so it is public, but it is
/// hidden from a Consumer's editor and may change in any version.</remarks>
/// <param name="js">The runtime of the component that asks.</param>
/// <param name="assets">That component's <see cref="ComponentBase.Assets"/>, where the host knows the
/// script's fingerprinted address.</param>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GridScriptFocus(IJSRuntime js, ResourceAssetCollection assets) : IAsyncDisposable
{
    private readonly IJSRuntime _js = js ?? throw new ArgumentNullException(nameof(js));
    private readonly ResourceAssetCollection _assets = assets ?? throw new ArgumentNullException(nameof(assets));
    private Task<IJSObjectReference?>? _module;

    /// <summary>
    /// Imports the grid's script at the address of the file this build ships (ADR-0123), once, and
    /// answers whether it is this build's. A script of another version is refused and let go, and every
    /// later call answers the same. A load that throws is not kept: the next call tries again.
    /// </summary>
    /// <returns>Whether the script is loaded and is this build's.</returns>
    public async Task<bool> LoadAsync() => await ModuleAsync() is not null;

    /// <summary>
    /// Has the first control inside <paramref name="box"/> take DOM focus: the box itself when it is a
    /// control, else its first enabled text field, whose text is selected, else its first enabled
    /// control. Loads the script first if it is not loaded; with a script refused, nothing is focused.
    /// </summary>
    /// <param name="box">The box, or the control itself.</param>
    /// <returns>Whether a control inside the box took DOM focus.</returns>
    public async Task<bool> FocusBoxAsync(ElementReference box)
        => await ModuleAsync() is { } module && await module.InvokeAsync<bool>("focusBox", box);

    /// <summary>Lets go of the script, where it was loaded.</summary>
    public async ValueTask DisposeAsync()
    {
        var loading = _module;
        _module = null;
        if (loading is null || !loading.IsCompletedSuccessfully || loading.Result is not { } module)
            return;
        try
        {
            await module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            // The circuit or the page went first; there is nothing left to let go of.
        }
    }

    private async Task<IJSObjectReference?> ModuleAsync()
    {
        var loading = _module ??= ImportAsync();
        try
        {
            return await loading;
        }
        catch
        {
            if (ReferenceEquals(_module, loading))
                _module = null;
            throw;
        }
    }

    private async Task<IJSObjectReference?> ImportAsync()
    {
        var address = "./" + PackageStylesheet.AddressOf(_assets, ShippedAssets.ScriptPath, ShippedAssets.ScriptVersion);
        var module = await _js.InvokeAsync<IJSObjectReference>("import", address);
        // Only a runtime that answers nothing at all, as a test's loose stand-in does.
        if (module is null)
            return null;
        if (await GridScriptStamp.IsOwnAsync(module))
            return module;
        await module.DisposeAsync();
        return null;
    }
}
