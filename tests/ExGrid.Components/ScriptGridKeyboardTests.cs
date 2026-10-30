using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// DC-61 / ADR-0070: Blazor's ExGrid over the Script Core gives the keyboard back, and hands it on to a control of
/// the Consumer's, by asking the core's handle, which judges the condition in the browser. Once the grid is gone
/// it gives nothing back, and the control takes the keyboard by its own focus.
/// </summary>
public class ScriptGridKeyboardTests : BunitContext
{
    private readonly BunitJSModuleInterop _grid;

    public ScriptGridKeyboardTests()
    {
        var module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        module.Setup<string?>("stamp").SetResult(global::ExGrid.Components.ScriptCoreAsset.Stamp);
        _grid = module.SetupModule("attach", _ => true);
        _grid.SetupVoid("update", _ => true).SetVoidResult();
        _grid.SetupVoid("state", _ => true).SetVoidResult();
        _grid.SetupVoid("returnKeyboard", _ => true).SetVoidResult();
        _grid.SetupVoid("handKeyboardTo", _ => true).SetVoidResult();
        _grid.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private IRenderedComponent<global::ExGrid.Components.ExGrid<TestRow>> Grid() => Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
        .Add(g => g.Rows, TestRows.Many(3))
        .Add(g => g.Columns, TestRows.Columns()));

    private ElementReference Control(string id) => new(id, new WebElementReferenceContext(JSInterop.JSRuntime));

    [Fact] // DC-61: the keyboard is given back by the handle, which reads where DOM focus is; a frame is named to it
    public async Task DC61_ADR0070_the_keyboard_is_given_back_through_the_handle_from_the_frame_named()
    {
        var cut = Grid();

        await cut.InvokeAsync(() => cut.Instance.ReturnKeyboardAsync());
        await cut.InvokeAsync(() => cut.Instance.ReturnKeyboardAsync(Control("frame")));

        var asked = _grid.Invocations["returnKeyboard"];
        Assert.Equal(2, asked.Count);
        Assert.Null(asked[0].Arguments[0]);
        Assert.Equal("frame", Assert.IsType<ElementReference>(asked[1].Arguments[0]).Id);
    }

    [Fact] // DC-61: the hand-on is asked of the handle, which grants it under the hand-back's condition
    public async Task DC61_ADR0070_a_control_is_handed_the_keyboard_through_the_handle()
    {
        var cut = Grid();

        await cut.InvokeAsync(() => cut.Instance.HandKeyboardToAsync(Control("tab"), preventScroll: true));

        var asked = Assert.Single(_grid.Invocations["handKeyboardTo"]);
        Assert.Equal("tab", Assert.IsType<ElementReference>(asked.Arguments[0]).Id);
        Assert.Equal(true, asked.Arguments[1]);
        Assert.DoesNotContain(JSInterop.Invocations, i => i.Identifier.EndsWith(".focus", StringComparison.Ordinal));
    }

    [Fact] // DC-61: once the grid is gone nothing is given back, and the control takes the keyboard by its own focus
    public async Task DC61_ADR0070_once_the_grid_is_gone_the_control_takes_the_keyboard_by_its_own_focus()
    {
        var cut = Grid();
        var grid = cut.Instance;
        await DisposeComponentsAsync();

        await grid.ReturnKeyboardAsync();
        await grid.HandKeyboardToAsync(Control("close"));

        Assert.Empty(_grid.Invocations["returnKeyboard"]);
        Assert.Empty(_grid.Invocations["handKeyboardTo"]);
        Assert.Equal("close", JSInterop.VerifyFocusAsyncInvoke().Arguments[0] is ElementReference control ? control.Id : null);
    }

    [Fact] // DC-61 and the spine's first rule: a container with nothing to take the keyboard is refused by the core, and the refusal reaches the Consumer
    public async Task DC61_ADR0070_a_hand_on_the_core_refuses_reaches_the_Consumer()
    {
        _grid.SetupVoid("handKeyboardTo", _ => true).SetException(new JSException("handKeyboardTo is handed an element that takes no keyboard."));
        var cut = Grid();

        await Assert.ThrowsAsync<JSException>(() => cut.InvokeAsync(() => cut.Instance.HandKeyboardToAsync(Control("empty"))));
    }

    [Fact] // DC-61: a control not yet drawn names no element, and is refused before anything is asked
    public async Task DC61_ADR0070_a_control_not_yet_drawn_is_refused()
    {
        var cut = Grid();

        await Assert.ThrowsAsync<ArgumentException>(() => cut.InvokeAsync(() => cut.Instance.HandKeyboardToAsync(default)));
        Assert.Empty(_grid.Invocations["handKeyboardTo"]);
    }

    [Fact] // DC-61: a grid whose script is refused is never attached, so nothing is given back and the control takes its own focus
    public async Task DC61_ADR0070_a_grid_never_attached_gives_nothing_back_and_the_control_takes_its_own_focus()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        module.Setup<string?>("stamp").SetResult("another build's");
        var cut = context.Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Many(3))
            .Add(g => g.Columns, TestRows.Columns()));

        await cut.InvokeAsync(() => cut.Instance.ReturnKeyboardAsync());
        await cut.InvokeAsync(() => cut.Instance.HandKeyboardToAsync(new ElementReference("note", new WebElementReferenceContext(context.JSInterop.JSRuntime))));

        Assert.DoesNotContain(module.Invocations, i => i.Identifier == "attach");
        Assert.Equal("note", context.JSInterop.VerifyFocusAsyncInvoke().Arguments[0] is ElementReference control ? control.Id : null);
    }
}
