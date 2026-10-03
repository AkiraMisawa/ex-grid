using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Finding;
using ExGrid.MudBlazor;
using Xunit;
using Color = global::MudBlazor.Color;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// SH-47 and the Chrome's shape (ADR-0019's note of 2026-09-30, ADR-0021, ADR-0071): the package
/// references ExSheet, ExGrid.MudBlazor and MudBlazor, and the direction stays one-way — no
/// ExSheet package in ExGrid.MudBlazor, no MudBlazor in ExSheet or the core; it adds no script; and
/// its Chrome is ExGrid.MudBlazor's in every seam of the grid, so the Sheet's grid looks and behaves
/// under it as under <see cref="MudGridChrome"/>.
/// </summary>
public class MudSheetPackageTests : MudSheetTestContext
{
    private static readonly string[] ScriptExtensions = [".js", ".mjs", ".cjs"];

    // The types a component needs to call into JavaScript or to be called from it. FocusAsync is
    // Blazor's own, and is how a Chrome focuses its control (ADR-0021/0030), so it is not here.
    private static readonly Regex Interop = new(
        @"\b(IJSRuntime|IJSInProcessRuntime|IJSUnmarshalledRuntime|IJSObjectReference|IJSInProcessObjectReference|DotNetObjectReference|JSInvokable|JSImport|JSExport)\b",
        RegexOptions.CultureInvariant);

    /// <summary>The repository's root, found rather than guessed, and refused if it is not there.</summary>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        return directory!.FullName;
    }

    private static string Project(string name)
    {
        var path = Path.Combine(Root(), "src", name, name + ".csproj");
        Assert.True(File.Exists(path), $"no project at {path}");
        return path;
    }

    private static IReadOnlyList<string> References(string project, string item) =>
        [.. XDocument.Load(Project(project)).Descendants(item)
            .Select(e => (string)e.Attribute("Include")!)
            .Select(include => item == "ProjectReference" ? Path.GetFileNameWithoutExtension(include.Replace('\\', '/')) : include)];

    [Fact] // SH-47 / ADR-0019's note of 2026-09-30: ExSheet.MudBlazor references ExSheet, ExGrid.MudBlazor and MudBlazor
    public void The_package_references_exsheet_the_wrapper_and_mudblazor()
    {
        Assert.Equal(["ExSheet", "ExGrid.MudBlazor"], References("ExSheet.MudBlazor", "ProjectReference").Order().Reverse());
        Assert.Equal(["MudBlazor"], References("ExSheet.MudBlazor", "PackageReference"));

        var built = typeof(MudSheetChrome).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToHashSet();
        Assert.Contains("ExSheet", built);
        Assert.Contains("ExGrid.MudBlazor", built);
        Assert.Contains("MudBlazor", built);
    }

    [Fact] // SH-47 / ADR-0019 / ADR-0046: the direction is one-way — the Wrapper references no ExSheet package, and neither ExSheet nor the core references MudBlazor
    public void The_direction_is_one_way()
    {
        Assert.DoesNotContain(References("ExGrid.MudBlazor", "ProjectReference"), p => p.StartsWith("ExSheet", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(MudGridChrome).Assembly.GetReferencedAssemblies(), a => a.Name!.StartsWith("ExSheet", StringComparison.Ordinal));

        foreach (var (project, assembly) in new[] { ("ExSheet", typeof(FormatCellsDraft).Assembly), ("ExGrid", typeof(IGridChrome).Assembly) })
        {
            Assert.DoesNotContain(References(project, "PackageReference"), p => p.Contains("MudBlazor", StringComparison.Ordinal));
            Assert.DoesNotContain(References(project, "ProjectReference"), p => p.Contains("MudBlazor", StringComparison.Ordinal));
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name!.Contains("MudBlazor", StringComparison.Ordinal));
        }
    }

    /// <summary>Every file of the package's source, its build output left out.</summary>
    private static List<string> SourceFiles(out string root)
    {
        root = Path.GetDirectoryName(Project("ExSheet.MudBlazor"))!;
        var directory = root;
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(directory, file)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .ToList();
        // What the scan must at least have seen, or it proves nothing.
        Assert.Contains(files, file => file.EndsWith(".razor", StringComparison.Ordinal));
        Assert.Contains(files, file => file.EndsWith(".cs", StringComparison.Ordinal));
        return files;
    }

    [Fact] // SH-47 / ADR-0021: no script file in the package's source, and no interop call of its own
    public void The_package_source_adds_no_script()
    {
        var files = SourceFiles(out var root);

        Assert.Empty(files
            .Where(file => ScriptExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(root, file)));
        Assert.Empty(files
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".razor", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (File: Path.GetRelativePath(root, file), Number: index + 1, Line: line.Trim())))
            .Where(entry => Interop.IsMatch(entry.Line))
            .Select(entry => $"{entry.File}:{entry.Number} {entry.Line}"));
    }

    [Fact] // SH-47 / ADR-0021: the package serves its stylesheet and no script — read from the build's own static-web-asset manifest
    public void The_package_serves_no_script()
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "ExSheet.MudBlazor.staticwebassets.runtime.json");
        Assert.True(File.Exists(manifest), $"the build recorded no static assets for the package ({manifest})");

        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var own = new List<string>();
        foreach (var child in document.RootElement.GetProperty("Root").GetProperty("Children").EnumerateObject())
        {
            if (child.Name != "_content") Collect(child.Name, child.Value, own);
        }

        // The stylesheet is the one asset the package is known to ship; not finding it means this
        // read the wrong thing, not that the package is clean.
        Assert.Contains("mud-ex-sheet.min.css", own);
        Assert.DoesNotContain(own, path => ScriptExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));
    }

    private static void Collect(string path, JsonElement node, List<string> into)
    {
        if (node.TryGetProperty("Asset", out var asset) && asset.ValueKind == JsonValueKind.Object) into.Add(path);
        if (node.TryGetProperty("Children", out var children) && children.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in children.EnumerateObject()) Collect(path + "/" + child.Name, child.Value, into);
        }
    }

    [Fact] // ADR-0071 / ADR-0010 / ADR-0030: every seam of the grid's is MudSheetChrome's own, forwarded to its Grid — none falls through to the core's default
    public void Every_seam_of_the_grids_is_forwarded()
    {
        var map = typeof(MudSheetChrome).GetInterfaceMap(typeof(IGridChrome));

        var fallingThrough = map.TargetMethods
            .Where(target => target.DeclaringType != typeof(MudSheetChrome))
            .Select(target => target.Name);

        Assert.Empty(fallingThrough);
    }

    [Fact] // ADR-0071 / ADR-0030 / WR-3: a seam of the grid's draws what the Grid Chrome draws, in its words
    public void A_seam_of_the_grids_is_the_grid_chromes()
    {
        var chrome = new MudSheetChrome { Grid = new MudGridChrome { Label = id => id == FindPanelLabelIds.Next ? "Weiter" : null } };
        var context = new FindContext(
            "", MatchCase: false, WholeCell: false, FindOutcome.None,
            _ => { }, _ => { }, _ => { }, () => Task.CompletedTask, () => Task.CompletedTask, () => { }, FocusRequest: 1);

        var cut = Render(chrome.FindPanel(context)!);

        Assert.Contains("Weiter", cut.Find(".mud-ex-grid-find-next").TextContent);
        Assert.Same(MudGridChrome.Default, MudSheetChrome.Default.Grid);
    }

    private static IReadOnlyList<string> MenuItems(RenderFragment menu, BunitContext context) =>
        [.. context.Render(menu).FindAll("[role=menuitem]").Select(item => item.TextContent.Trim())];

    private static ColumnMenuContext MenuOf(params string[] ids) =>
        new("A", ColumnType.Text, [.. ids.Select(id => new GridCommand(id, true, () => Task.CompletedTask))], () => { });

    [Fact] // ADR-0036 / ADR-0071: the menus word ExSheet's commands — "Format Cells…", not its id — and the grid's own as the Wrapper does
    public void The_menus_word_exsheets_commands()
    {
        var items = MenuItems(MudSheetChrome.Default.ColumnMenu(MenuOf(SheetCommandIds.FormatCells, SheetCommandIds.InsertRows, GridCommandIds.Copy))!, this);

        Assert.Equal(["Format Cells…", "Insert rows above", "Copy"], items);
    }

    [Fact] // ADR-0036 / WR-3: the Consumer's Label words ExSheet's commands first, and ExSheet's English stands where it says nothing
    public void The_consumers_label_words_exsheets_commands_first()
    {
        var chrome = new MudSheetChrome { Grid = new MudGridChrome { Label = id => id == SheetCommandIds.FormatCells ? "Zellen formatieren…" : null } };

        var items = MenuItems(chrome.ColumnMenu(MenuOf(SheetCommandIds.FormatCells, SheetCommandIds.DeleteRows))!, this);

        Assert.Equal(["Zellen formatieren…", "Delete rows"], items);
    }

    [Fact] // ADR-0030 / ADR-0071: the Grid Chrome handed over is carried whole into the seams — every property MudGridChrome has
    public void The_grid_chrome_is_carried_whole()
    {
        // A property MudGridChrome gains is one Worded must carry: this list fails until it does.
        Assert.Equal(
            [nameof(MudGridChrome.Icon), nameof(MudGridChrome.Label), nameof(MudGridChrome.LoadingProgressColor)],
            typeof(MudGridChrome).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Order());
        Func<string, string?> icon = _ => "an-icon";
        var grid = new MudGridChrome { LoadingProgressColor = Color.Error, Icon = icon, Label = _ => null };

        var worded = MudSheetChrome.Worded(grid);

        Assert.Equal(Color.Error, worded.LoadingProgressColor);
        Assert.Same(icon, worded.Icon);
        Assert.Equal("Format Cells…", worded.Label!(SheetCommandIds.FormatCells));
        Assert.Same(grid, new MudSheetChrome { Grid = grid }.Grid);
    }

    [Fact] // ADR-0071 / ADR-0030: under MudSheetChrome the Sheet's grid wears ExGrid.MudBlazor's controls — the Name Box is the Wrapper's
    public void The_sheets_grid_wears_the_wrappers_controls()
    {
        var page = RenderPage();

        Assert.NotEmpty(page.FindAll(".ex-name-box .mud-ex-name-box"));
    }
}
