using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// The keys typed while the keyboard is on its way into Format Cells' dialog (ADR-0010's hold, as a
/// Chrome keeps it; ADR-0071): held in order until the tabs hold the keyboard, handed on then, and
/// a Tab dropped with every key behind it, as the core drops one.
/// </summary>
public class KeysOnTheirWayTests
{
    private readonly List<string> _handed = [];

    private Task<bool> Hand(KeyboardEventArgs key)
    {
        _handed.Add(key.Key);
        return Task.FromResult(key.Key is not ("Tab" or "Escape"));
    }

    private static KeyboardEventArgs Key(string key) => new() { Key = key };

    [Fact] // ADR-0010 / ADR-0071: keys typed before the tabs hold the keyboard reach them, in order, once they do
    public async Task Keys_typed_before_the_tabs_hold_the_keyboard_reach_them_in_order()
    {
        var keys = new KeysOnTheirWay();
        await keys.TypedAsync(Key("End"));
        await keys.TypedAsync(Key("ArrowLeft"));
        Assert.Empty(_handed);

        await keys.ArrivedAsync(Hand);
        await keys.TypedAsync(Key("Home"));

        Assert.Equal(["End", "ArrowLeft", "Home"], _handed);
    }

    [Fact] // ADR-0010 / ADR-0021: a held Tab cannot be reproduced without script, and is dropped with every key behind it
    public async Task A_held_tab_is_dropped_with_every_key_behind_it()
    {
        var keys = new KeysOnTheirWay();
        await keys.TypedAsync(Key("End"));
        await keys.TypedAsync(Key("Tab"));
        await keys.TypedAsync(Key("ArrowLeft"));

        await keys.ArrivedAsync(Hand);
        await keys.TypedAsync(Key("Home"));

        Assert.Equal(["End", "Tab"], _handed);
    }
}
