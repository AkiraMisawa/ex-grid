using System.Reflection;

namespace ExGrid.Docs;

/// <summary>A public type of a shipped package, as the API reference lists it (ADR-0201).</summary>
/// <param name="Package">The package, which is its assembly's name.</param>
/// <param name="Namespace">Its namespace.</param>
/// <param name="Name">How a reader writes it: <c>ExGrid&lt;TRow&gt;</c>.</param>
/// <param name="Unbound">Its full name with its type parameters left out, as a page's <c>typeof</c> names it.</param>
/// <param name="Id">Its documentation id, which keys its summary.</param>
/// <param name="Kind">What it is: <c>component</c>, <c>record</c>, <c>enum</c>, …</param>
/// <param name="Contract">Whether it is the contract, rather than public and not the contract.</param>
/// <param name="Type">The type itself, for its API table.</param>
internal sealed record ApiType(string Package, string Namespace, string Name, string Unbound, string Id, string Kind, bool Contract, Type Type)
{
    /// <summary>The anchor its row carries on the API reference: <c>api-exgrid-components-exgrid-1</c>.</summary>
    public string Anchor { get; } = "api-" + new string(Id[2..].Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());

    /// <summary>Its summary as HTML, as the build read it from the package's documentation.</summary>
    public string? Summary => ApiDocs.SummaryOf(Id);
}

/// <summary>A member of a contract type that is public and not the contract (ADR-0201).</summary>
/// <param name="Package">The package.</param>
/// <param name="TypeId">Its type's documentation id.</param>
/// <param name="Type">Its type, as a reader writes it.</param>
/// <param name="Name">The member, as a reader writes it.</param>
/// <param name="Id">Its documentation id, which keys its summary.</param>
/// <param name="Interop">Whether the grid's own script calls it, rather than it being hidden from a Consumer's editor.</param>
internal sealed record ApiMember(string Package, string TypeId, string Type, string Name, string Id, bool Interop)
{
    /// <summary>Its summary as HTML, as the build read it from the package's documentation.</summary>
    public string? Summary => ApiDocs.SummaryOf(Id);
}

/// <summary>
/// The public surface of the family's packages (ADR-0201), read by the build from the shipped assemblies
/// (ExGrid.Docs.Generator): every public type, and which are the contract. A Razor component a Consumer does
/// not place, a type only the grid's own script names, and a member that script calls or that is hidden from a
/// Consumer's editor are public and not the contract: they may change in any version. The API reference lists
/// the two apart, and every API table does too.
/// </summary>
internal static partial class ApiSurface
{
    /// <summary>The route of the API reference.</summary>
    public const string Route = "reference/api";

    /// <summary>What the site says of everything public and not the contract.</summary>
    public const string NotTheContract = "Not the contract: may change in any version";

    /// <summary>Every public type of the family's packages, by package in the Packages page's order, then by namespace and name.</summary>
    public static IReadOnlyList<ApiType> Types { get; } = ReadTypes();

    /// <summary>The members of contract types that are public and not the contract.</summary>
    public static IReadOnlyList<ApiMember> NotContractMembers { get; } = ReadNotContract();

    private static readonly Dictionary<string, ApiType> ById = Types.ToDictionary(t => t.Id, StringComparer.Ordinal);

    private static readonly HashSet<string> NotContractIds = NotContractMembers.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>The API reference's entry for <paramref name="type"/>, if it is a public type of a shipped package.</summary>
    public static ApiType? Of(Type type) => ById.GetValueOrDefault(ApiDocs.Id(type.IsGenericType ? type.GetGenericTypeDefinition() : type));

    /// <summary>Whether <paramref name="member"/> of a contract type is public and not the contract.</summary>
    public static bool IsNotContract(MemberInfo member) => NotContractIds.Contains(ApiDocs.Id(member));

    /// <summary>The types of <paramref name="package"/> that are the contract, or public and not the contract.</summary>
    public static IEnumerable<ApiType> TypesOf(string package, bool contract) =>
        Types.Where(t => t.Package == package && t.Contract == contract);

    /// <summary>The members of <paramref name="package"/>'s contract types that are public and not the contract.</summary>
    public static IEnumerable<ApiMember> NotContractMembersOf(string package) =>
        NotContractMembers.Where(m => m.Package == package);

    /// <summary>
    /// The topic page section whose API table shows <paramref name="type"/>, as the site map read the pages'
    /// <c>ApiTable</c>s, with the page's title; <see langword="null"/> when no topic page shows it.
    /// </summary>
    public static (string Href, string Title)? ShownOn(ApiType type)
    {
        foreach (var page in SiteMap.Pages)
        {
            if (page.Routes.Contains(Route, StringComparer.OrdinalIgnoreCase))
                continue;
            foreach (var section in page.Sections)
            {
                if (section.Types.Any(t => t == type.Unbound || type.Unbound.EndsWith("." + t, StringComparison.Ordinal)))
                    return ($"{page.Route}#{section.Anchor}", page.Title ?? page.Route);
            }
        }
        return null;
    }

    /// <summary>The search's entries: each public type, leading to its row on the API reference.</summary>
    public static IEnumerable<SiteSearchEntry> SearchEntries() =>
        Types.Select(t => new SiteSearchEntry(
            t.Name,
            t.Contract ? $"API reference · {t.Package}" : $"API reference · {t.Package} · not the contract",
            $"{Route}#{t.Anchor}",
            SiteSearchKind.Type));

    // The build writes these from the shipped assemblies (ExGrid.Docs.Generator).
    private static partial ApiType[] ReadTypes();

    private static partial ApiMember[] ReadNotContract();
}
