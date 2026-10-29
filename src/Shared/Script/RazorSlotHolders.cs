using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExGrid.Script;

/// <summary>
/// The Chrome slots a Blazor Script adapter's module mounted, filled with Razor, .NET's half (ADR-0410): each slot's
/// holder, rendered beside the component's root, holds the one element its fragment is drawn in, which the Shared
/// Script's <c>RazorSlotHost</c> moves into the slot and back. The module says <c>MountSlot</c>, <c>UpdateSlot</c> and
/// <c>UnmountSlot</c>, each with the number of the data it sent; once a slot is drawn the module is told
/// <c>slotRendered</c> with that number. Compiled into each adapter that fills slots with Razor (ExGrid's today), so every product's slots cross alike; what a slot's
/// data read as, and which fragment draws it, are the product's.
/// </summary>
/// <param name="viewOf">The view a slot named as the script names it is drawn from, read from the data it sent.</param>
internal sealed class RazorSlotHolders(Func<string, string, object> viewOf)
{
    private readonly SortedDictionary<long, RazorSlotHeld> _slots = [];

    /// <summary>The attribute the element the fragment is drawn in carries: the number of the data it was drawn from,
    /// which the script reads a press on the contents against (razor-slot-host.ts' <c>drawnVersionAttribute</c>).</summary>
    public const string DrawnVersionAttribute = "data-ex-version";

    /// <summary>Whether any slot stands.</summary>
    public bool Any => _slots.Count > 0;

    /// <summary>The script mounted slot <paramref name="id"/>, named <paramref name="name"/>, with its context as data.</summary>
    public void Mount(long id, int version, string name, string context) => _slots.Add(id, new RazorSlotHeld(id, name, version, viewOf(name, context)));

    /// <summary>The slot <paramref name="id"/> was mounted again in place, or its context changed: whether it stands.</summary>
    public bool Update(long id, int version, string context)
    {
        if (!_slots.TryGetValue(id, out var slot))
            return false;
        slot.Version = version;
        slot.View = viewOf(slot.Name, context);
        return true;
    }

    /// <summary>The core took the slot <paramref name="id"/> away, and the script has moved its contents back: whether it
    /// stood.</summary>
    public bool Unmount(long id) => _slots.Remove(id);

    /// <summary>Another Chrome draws the slots standing: each is owed to the script again, which wires what it drew.</summary>
    public void Redrawn()
    {
        foreach (var slot in _slots.Values)
            slot.Reported = 0;
    }

    /// <summary>Each slot's holder beside the root, never inside it: hidden, and holding only the element the fragment is
    /// drawn in, which the script moves into the slot and back (ADR-0410 section 1). Sequence numbers from
    /// <paramref name="sequence"/> to seven after it are used.</summary>
    public void Render(RenderTreeBuilder builder, int sequence, Func<string, object, RenderFragment?> fragment)
    {
        foreach (var slot in _slots.Values)
        {
            slot.Drawn = slot.Version;
            builder.OpenElement(sequence, "div");
            builder.SetKey(slot.Id);
            builder.AddAttribute(sequence + 1, "hidden", true);
            builder.OpenElement(sequence + 2, "div");
            builder.AddAttribute(sequence + 3, "class", "ex-razor-slot");
            // It draws no box of its own: the contents lay themselves out in the slot as a React Chrome's do.
            builder.AddAttribute(sequence + 4, "style", "display: contents");
            // The data the contents are drawn from, by the script's number: a press on them is read against these.
            builder.AddAttribute(sequence + 5, DrawnVersionAttribute, slot.Drawn);
            builder.AddElementReferenceCapture(sequence + 6, element => slot.Contents = element);
            builder.AddContent(sequence + 7, fragment(slot.Name, slot.View));
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    /// <summary>Each slot drawn again is handed to the script on <paramref name="handle"/>, which moves it in, or wires it
    /// for the context it now holds; nothing once <paramref name="gone"/> says the component is gone.</summary>
    public async Task ReportAsync(IJSObjectReference? handle, Func<bool> gone)
    {
        // A slot mounted after the render this report follows has no element yet: it is reported after the render
        // its mount asked for, which draws it. Each is reported with the number it was drawn from, never the number its
        // data stand at now: on Blazor Server an update can arrive between the render and this report, and the
        // contents show the earlier data until the render it asked for lands, which is reported in its turn.
        var owed = _slots.Values.Where(slot => slot.Drawn != slot.Reported && slot.Contents.Id is not null).ToArray();
        // Disposed since the render: the component is gone, and nothing is handed to it.
        if (owed.Length == 0 || handle is null || gone())
            return;
        var drawn = owed.Select(slot => (slot.Id, Version: slot.Drawn, slot.Contents)).ToArray();
        foreach (var slot in owed)
            slot.Reported = slot.Drawn;
        try
        {
            // Sent together, in order, and awaited together: a screen of Template cells is one round trip, not one each
            // (measured on /script-grid-mud with 37 rows, 2026-10-28: a page down settled in about 150 ms awaiting each,
            // about 80 ms sent together, and about 50 ms with no Template column).
            await Task.WhenAll(drawn.Select(slot => handle.InvokeVoidAsync("slotRendered", slot.Id, slot.Version, slot.Contents).AsTask()));
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the component with it.
        }
    }

    /// <summary>A slot the script mounted: its number, its name, the view its data last crossed as, and the element its
    /// fragment is drawn in once rendered.</summary>
    private sealed class RazorSlotHeld(long id, string name, int version, object view)
    {
        public long Id { get; } = id;

        public string Name { get; } = name;

        public int Version { get; set; } = version;

        public object View { get; set; } = view;

        public ElementReference Contents { get; set; }

        /// <summary>The number of the data the contents were last drawn from: the attribute they carry.</summary>
        public int Drawn { get; set; }

        /// <summary>The number the script was last told the contents were drawn from; 0 for none, or for contents another
        /// Chrome draws again, which the script wires afresh.</summary>
        public int Reported { get; set; }
    }
}
