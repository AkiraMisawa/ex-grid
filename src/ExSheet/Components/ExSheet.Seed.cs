using ExSheet.Engine;
using Microsoft.AspNetCore.Components;

namespace ExSheet.Components;

/// <summary>
/// The Sheet Seed (ADR-0180): what <c>RAND()</c> and <c>RANDBETWEEN</c> draw from. The engine is given
/// it as data; this part keeps it. ExSheet takes the Consumer's <see cref="Seed"/>, or chooses one
/// when it opens a Sheet Document, and the count of changes starts from 0 there. Neither is recorded
/// in the Sheet Document. A prerender chooses none: the instance the circuit or the browser runs next
/// would choose another, and every draw on the page would change with nothing done, so what a reader
/// saw would depend on when they read it. Until then the draws wait with <c>#GETTING_DATA</c>, as
/// <c>TODAY</c> waits for the browser's zone (ADR-0122).
/// </summary>
public partial class ExSheet
{
    /// <summary>
    /// A fixed Sheet Seed (ADR-0180): what <c>RAND()</c> and <c>RANDBETWEEN</c> draw from, with the
    /// cell, the call's place in its Formula and the count of changes since the Sheet Document was
    /// opened — for a sheet that must show the same draws whenever it is opened, such as a simulation
    /// someone else reviews. The draws still change at each change of the Workbook, and never on the
    /// clock. Null, the default, has ExSheet choose a seed whenever it opens a Sheet Document, so a
    /// reopened sheet draws again, as Excel's reopened workbook does; a prerender chooses none, and its
    /// draws show <c>#GETTING_DATA</c> until the page is interactive. The seed is not recorded in the
    /// Sheet Document.
    /// </summary>
    [Parameter] public ulong? Seed { get; set; }

    /// <summary>
    /// The seed chosen when the Sheet Document was opened, which draws while no <see cref="Seed"/> is
    /// given; <see langword="null"/> in a prerender, which chooses none.
    /// </summary>
    private ulong? _chosenSeed;

    private bool _seedParameterSeen;
    private ulong? _seedParameter;

    // What a change of Seed recalculated, painted once the parameters are set.
    private SheetChange? _seedChange;

    /// <summary>
    /// Gives a Workbook just opened its seed, the Consumer's or one chosen now, at a count of 0 changes.
    /// A prerender chooses none, so without the Consumer's its draws wait.
    /// </summary>
    private void GiveTheSeed(global::ExSheet.Engine.Workbook workbook)
    {
        _chosenSeed = RendererInfo.IsInteractive ? ChooseSeed() : null;
        workbook.SetSeed(Seed ?? _chosenSeed, 0);
    }

    private static ulong ChooseSeed()
    {
        Span<byte> drawn = stackalloc byte[sizeof(ulong)];
        Random.Shared.NextBytes(drawn);
        return BitConverter.ToUInt64(drawn);
    }

    /// <summary>
    /// Called as the parameters are set: a new <see cref="Seed"/> — or none, which goes back to the
    /// seed chosen at opening — draws again at the same count of changes, painted from
    /// <see cref="PaintTheSeedAsync"/>. It is not a change of the Sheet.
    /// </summary>
    private void FollowTheSeedParameter()
    {
        if (_seedParameterSeen && Seed == _seedParameter) return;
        _seedParameterSeen = true;
        _seedParameter = Seed;
        if (_workbook is not { } workbook) return;
        var change = workbook.SetSeed(Seed ?? _chosenSeed);
        if (_sheet is { } shown) _seedChange = change.For(shown);
    }

    private async Task PaintTheSeedAsync()
    {
        if (_seedChange is not { } change) return;
        _seedChange = null;
        await ChangedAsync(change, raiseDocument: false, byUser: false);
    }
}
