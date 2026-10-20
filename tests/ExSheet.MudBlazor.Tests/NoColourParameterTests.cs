using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// No colour is ever a C# parameter (ADR-0027; UX-1): appearance travels as Visual Tokens the
/// stylesheet reads, so no public component of a shipped package takes a colour, whatever its
/// type says. Read across every shipped package that holds components — this project references
/// ExSheet.MudBlazor and ExPivot.MudBlazor, which between them reach ExGrid, ExGrid.MudBlazor,
/// ExSheet and ExPivot — and the test fails if a Razor package under <c>src/</c> is not loaded.
/// <para>MudBlazor's <c>Color</c> is a palette role, not a colour value, and it is refused all the
/// same: a role picked per instance through a parameter is appearance chosen in .NET, which
/// ADR-0027 routes through the cascade instead (a Wrapper maps its tokens onto
/// <c>--mud-palette-*</c> in its stylesheet, as <c>mud-ex-grid.css</c> does). A Wrapper that
/// wants one needs an ADR first.</para>
/// <para>An <see cref="EventCallback{TValue}"/> is outbound — the grid telling the Consumer, as
/// <c>OnReferenceKeyColoursChanged</c> tells which reference took which colour index — and is not
/// a colour given to the grid, so it is not read. A delegate the grid calls is inbound: what it
/// returns is read.</para>
/// <para>The members of a type a parameter carries are not read. So <c>ExGrid.CellAppearance</c>, a
/// delegate returning <c>CellAppearance</c>, whose <c>RgbColour</c> is a Sheet's own cell format, passes:
/// that colour is the Consumer's data, which ADR-0050 and ADR-0071 let a Consumer give per cell, not the
/// grid's appearance. A colour put inside another carried type would pass the same way, and is not held
/// here.</para>
/// </summary>
public class NoColourParameterTests
{
    // The words a colour-carrying parameter's name says, in either spelling: whole words of its
    // PascalCase name, so Ink is a word in SheetInk and not in ShowLink.
    private static readonly HashSet<string> ColourWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Color", "Colors", "Colour", "Colours", "Background", "Foreground", "Palette", "Tint", "Shade", "Hue", "Ink", "Paint",
    };

    private static bool NameSaysColour(string name)
        => Regex.Matches(name, @"[A-Z]?[a-z]+|[A-Z]+(?![a-z])").Any(word => ColourWords.Contains(word.Value));

    // A type that is a colour: System.Drawing.Color, MudBlazor's palette role, or any type named one.
    private static bool IsColourType(Type type)
        => type.FullName is "System.Drawing.Color" or "MudBlazor.Color"
           || Regex.IsMatch(type.Name, @"Colou?rs?$");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        return directory.FullName;
    }

    /// <summary>The shipped packages that can hold components: every Razor project under src/.</summary>
    private static IReadOnlyList<Assembly> ComponentPackages()
    {
        var names = Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => XDocument.Load(project).Root?.Attribute("Sdk")?.Value == "Microsoft.NET.Sdk.Razor")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["ExChart", "ExChart.MudBlazor", "ExGrid", "ExGrid.MudBlazor", "ExPivot", "ExPivot.MudBlazor", "ExSheet", "ExSheet.MudBlazor"], names);
        return names.Select(name => Assembly.Load(name!)).ToList();
    }

    /// <summary>What a parameter of this type hands the component: the type itself, what a collection,
    /// a nullable or a generic holds, and what a delegate it calls returns — never what an
    /// <see cref="EventCallback"/> sends out.</summary>
    private static IEnumerable<Type> Carried(Type type)
    {
        if (type.IsGenericParameter)
            yield break;
        if (type == typeof(EventCallback) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>)))
            yield break;
        if (type == typeof(RenderFragment) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RenderFragment<>)))
            yield break;
        yield return type;
        if (type.HasElementType)
        {
            foreach (var inner in Carried(type.GetElementType()!))
                yield return inner;
        }
        if (typeof(Delegate).IsAssignableFrom(type) && type.GetMethod("Invoke") is { } invoke)
        {
            foreach (var inner in Carried(invoke.ReturnType))
                yield return inner;
            yield break;
        }
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var inner in Carried(argument))
                    yield return inner;
            }
        }
    }

    /// <summary>Every public parameter of a public component of a shipped package, inherited ones
    /// included, from another shipped package or from MudBlazor: a Consumer can set either.</summary>
    private static IEnumerable<(Type Component, PropertyInfo Parameter)> PublicParameters()
    {
        var packages = ComponentPackages();
        return packages
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttribute<ParameterAttribute>(inherit: true) is not null)
                .Select(property => (type, property)));
    }

    /// <summary>
    /// The colour parameters a component inherits and does not offer, each with why (2026-10-13, decided with
    /// the user: inherited parameters are counted, and these are named). <c>MudPivotButton</c> is a
    /// <c>MudButton</c> and inherits its <c>Color</c> and <c>IconColor</c>; it is marked
    /// <c>EditorBrowsable(Never)</c> and is not the contract (ADR-0201). Any other inherited colour fails.
    /// </summary>
    private static readonly IReadOnlySet<string> InheritedNotOffered = new HashSet<string>
    {
        "ExPivot.MudBlazor.MudPivotButton.Color",
        "ExPivot.MudBlazor.MudPivotButton.IconColor",
    };

    [Fact] // ADR-0027 / UX-1: no public parameter of a shipped component carries a colour, by its type or by a string its name says is one
    public void UX1_no_public_component_parameter_is_a_colour()
    {
        var parameters = PublicParameters().ToList();
        Assert.NotEmpty(parameters);

        var offenders = parameters
            .Where(entry =>
            {
                var carried = Carried(entry.Parameter.PropertyType).ToList();
                return carried.Any(IsColourType)
                    || (carried.Any(type => type == typeof(string) || type == typeof(object)) && NameSaysColour(entry.Parameter.Name));
            })
            .Select(entry => (Name: $"{entry.Component.FullName}.{entry.Parameter.Name}", Type: entry.Parameter.PropertyType))
            .Distinct()
            .ToList();

        Assert.Empty(offenders.Where(o => !InheritedNotOffered.Contains(o.Name)).Select(o => $"{o.Name}: {o.Type}"));
        // A named exception that no longer exists is stale, and is taken out.
        Assert.Equal(InheritedNotOffered.Order(StringComparer.Ordinal), offenders.Select(o => o.Name).Where(InheritedNotOffered.Contains).Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0027 / UX-1: the reading above sees a colour wherever a parameter would hand one in, and not in what a callback sends out
    public void UX1_the_colour_reading_sees_through_wrappers_and_not_into_callbacks()
    {
        Assert.Contains(Carried(typeof(global::MudBlazor.Color?)), IsColourType);
        Assert.Contains(Carried(typeof(IReadOnlyList<global::MudBlazor.Color>)), IsColourType);
        Assert.Contains(Carried(typeof(Func<int, global::MudBlazor.Color>)), IsColourType);
        Assert.DoesNotContain(Carried(typeof(EventCallback<global::MudBlazor.Color>)), IsColourType);
        Assert.Contains(typeof(string), Carried(typeof(Func<int, string>)));
        Assert.True(NameSaysColour("RowBackground"));
        Assert.True(NameSaysColour("HeaderColour"));
        Assert.True(NameSaysColour("SheetInk"));
        Assert.False(NameSaysColour("ShowLink"));
        Assert.False(NameSaysColour("Placeholder"));
    }
}
