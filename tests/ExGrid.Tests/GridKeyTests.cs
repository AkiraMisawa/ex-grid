using ExGrid.Keys;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The key table (ADR-0010: the core arbitrates the keys). What each key means, which keys a grid takes
/// and which declarations are refused are in the shared cases, <c>tests/cases/keys/grid-keys.json</c>;
/// what stays here is a property of the whole table, and the null a browser's field may be in C#.
/// </summary>
public class GridKeyTests
{
    [Fact] // The browser sends what it likes; in C# the field may be null
    public void A_null_key_is_not_an_error()
    {
        Assert.Equal(GridKeyKind.None, GridKeys.Resolve(null, false, false, false, false, false).Kind);
    }

    [Fact] // ADR-0010: the set handed to the listener is exactly the set this side resolves
    public void Every_taken_key_resolves_to_something()
    {
        Assert.NotEmpty(GridKeys.Taken);
        foreach (var canonical in GridKeys.Taken)
            AssertResolves(canonical);
    }

    [Fact] // ADR-0010: the widest set a grid can take still resolves, key by key
    public void Every_conditionally_taken_key_resolves_to_something()
    {
        var everything = GridKeys.TakenFor(new GridKeyClaims(CanEdit: true, CanUndo: true, CanRedo: true));
        foreach (var canonical in everything)
            AssertResolves(canonical);
        Assert.Superset(new HashSet<string>(GridKeys.Taken), new HashSet<string>(everything));
    }

    // Split the canonical form back into fields, the way the listener forwards them. A key in the set
    // that resolved to None would be taken from the browser and then do nothing at all.
    private static void AssertResolves(string canonical)
    {
        var ctrl = canonical.Contains("Control+", StringComparison.Ordinal);
        var shift = canonical.Contains("Shift+", StringComparison.Ordinal);
        var alt = canonical.Contains("Alt+", StringComparison.Ordinal);
        var key = canonical.EndsWith("+ ", StringComparison.Ordinal) ? " " : canonical[(canonical.LastIndexOf('+') + 1)..];
        Assert.NotEqual(GridKeyKind.None, GridKeys.Resolve(key, ctrl, shift, alt, meta: false, metaIsPrimary: false).Kind);
    }
}
