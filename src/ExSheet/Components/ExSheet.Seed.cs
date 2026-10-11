using ExSheet.Engine;
using Microsoft.AspNetCore.Components;

namespace ExSheet.Components;

/// <summary>
/// The Sheet Seed (ADR-0180): what <c>RAND()</c> and <c>RANDBETWEEN</c> draw from. The engine is given
/// it as data; this part keeps it. ExSheet takes the Consumer's <see cref="Seed"/>, or chooses one
/// when it opens a Sheet Document, and the count of changes starts from 0 there. Neither is recorded
/// in the Sheet Document.
/// </summary>
public partial class ExSheet
{
    /// <summary>
    /// A fixed Sheet Seed (ADR-0180): what <c>RAND()</c> and <c>RANDBETWEEN</c> draw from, with the
    /// cell, the call's place in its Formula and the count of changes since the Sheet Document was
    /// opened — for a sheet that must show the same draws whenever it is opened, such as a simulation
    /// someone else reviews. The draws still change at each change of the Sheet, and never on the
    /// clock. Null, the default, has ExSheet choose a seed whenever it opens a Sheet Document, so a
    /// reopened sheet draws again, as Excel's reopened workbook does. The seed is not recorded in the
    /// Sheet Document.
    /// </summary>
    [Parameter] public ulong? Seed { get; set; }

    /// <summary>The seed chosen when the Sheet Document was opened, which draws while no <see cref="Seed"/> is given.</summary>
    private ulong _chosenSeed;

    private bool _seedParameterSeen;
    private ulong? _seedParameter;

    // What a change of Seed recalculated, painted once the parameters are set.
    private SheetChange? _seedChange;

    /// <summary>Gives a Sheet just opened its seed, the Consumer's or one chosen now, at a count of 0 changes.</summary>
    private void GiveTheSeed(Sheet sheet)
    {
        Span<byte> drawn = stackalloc byte[sizeof(ulong)];
        Random.Shared.NextBytes(drawn);
        _chosenSeed = BitConverter.ToUInt64(drawn);
        sheet.SetSeed(Seed ?? _chosenSeed, 0);
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
        if (_sheet is not { } sheet) return;
        _seedChange = sheet.SetSeed(Seed ?? _chosenSeed);
    }

    private async Task PaintTheSeedAsync()
    {
        if (_seedChange is not { } change) return;
        _seedChange = null;
        await ChangedAsync(change, raiseDocument: false, byUser: false);
    }
}
