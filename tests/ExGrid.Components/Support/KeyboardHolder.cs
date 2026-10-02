using AngleSharp.Dom;
using Xunit;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// What the markup says about the element that holds the keyboard (ADR-0033, ADR-0080): on a grid
/// that edits, its Keyboard Field holds it with no edit open, and carries
/// <c>aria-activedescendant</c>; on any other grid the root does. Never both.
/// </summary>
internal static class KeyboardHolder
{
    /// <summary>This grid's own Keyboard Field, or null where it has none — never the field of a
    /// grid nested in one of its cells. Told apart by the roots' ids: bUnit hands out a new
    /// wrapper for each element it finds.</summary>
    internal static IElement? KeyFieldOf(IElement root)
        => root.QuerySelectorAll("input.ex-key-field")
            .FirstOrDefault(field => field.Closest(".ex-grid")?.Id is { } id && id == root.Id);

    /// <summary>The <c>aria-activedescendant</c> of the grid whose root is
    /// <paramref name="root"/>, read from the element that carries it: its Keyboard Field where it
    /// has one, its root otherwise. Asserts that the other element carries none (A11Y-5).</summary>
    internal static string? ActiveDescendant(IElement root)
    {
        var field = KeyFieldOf(root);
        if (field is null)
            return root.GetAttribute("aria-activedescendant");
        Assert.Null(root.GetAttribute("aria-activedescendant"));
        return field.GetAttribute("aria-activedescendant");
    }
}
