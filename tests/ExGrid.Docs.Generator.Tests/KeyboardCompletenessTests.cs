using System.Collections.Immutable;
using Bunit;
using ExGrid.Keys;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using SheetComponent = global::ExSheet.Components.ExSheet;
using SheetGrid = global::ExGrid.Components.ExGrid<global::ExSheet.SheetRow>;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The Keyboard reference held against the keys the products take (ADR-0110: a reference page's facts are read
/// from a named source, and the Keyboard page lists every key in the three products). The rows are the site's own
/// <c>KeyboardByHand</c>, read as the build reads them; the keys are ExGrid's key table (<c>GridKeys</c>, KB-2),
/// every key claimed on a grid that edits, undoes, redoes and finds, and the keys ExSheet declares to its grid, read
/// off the component as it renders with everything that adds a key turned on; ExPivot declares none. A key a menu,
/// a panel or an open edit answers in its own handler is in no table, and is not checked here.
/// </summary>
public class KeyboardCompletenessTests : BunitContext
{
    private static readonly ImmutableArray<HandRow> Rows = ReadRows();

    public KeyboardCompletenessTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    [Fact] // ADR-0110, ADR-0012: every key ExGrid's table takes is named by an ExGrid row of the Keyboard page
    public void Every_key_ExGrid_takes_is_named_by_an_ExGrid_row()
    {
        var taken = GridKeys.TakenFor(new GridKeyClaims(CanEdit: true, CanUndo: true, CanRedo: true, CanFind: true));

        AssertNamed("ExGrid", taken);
    }

    [Fact] // ADR-0110, ADR-0071, ADR-0100: every key ExSheet declares to its grid is named by an ExSheet row
    public void Every_key_ExSheet_declares_is_named_by_an_ExSheet_row()
    {
        var declared = SheetDeclared();

        Assert.NotEmpty(declared);
        AssertNamed("ExSheet", declared);
    }

    [Fact] // ADR-0110: every key a row names is one its product answers, so a row cannot name a key nothing takes
    public void Every_key_a_row_names_is_one_its_product_answers()
    {
        var declared = new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["ExGrid"] = [],
            ["ExSheet"] = SheetDeclared(),
            // ExPivot declares no key of its own to its grids (@ex-grid/pivot's report grid takes no declaredKeys):
            // every key an ExPivot row names is one the core answers.
            ["ExPivot"] = [],
        };
        var unanswered = (
            from row in Rows
            from key in row.List("Canonical")
            let product = row.Property
            where !CoreAnswers(key.Value!) && !declared[product].Select(Fold).Contains(Fold(key.Value!))
            select $"{product}: '{key.Value}' ({row.Text("Keys").Value})").ToList();

        Assert.True(unanswered.Count == 0,
            "These keys are named by a row of the Keyboard page, and neither ExGrid's core nor the product's declared keys answer them: "
            + string.Join("; ", unanswered) + ". Correct the key, or take the row's claim away.");
    }

    [Fact] // ADR-0110: the rows read here are the site's, as the build reads them
    public void The_rows_are_read_as_the_build_reads_them()
    {
        foreach (var product in KeyboardReference.Products)
            Assert.Contains(Rows, r => r.Property == product);
        Assert.All(Rows.SelectMany(r => r.List("Canonical")), key =>
        {
            Assert.NotNull(key.Value);
            Assert.True(KeyboardReference.IsCanonicalForm(key.Value), $"'{key.Value}' is not in the canonical form.");
        });
        Assert.Contains(Rows, r => r.List("Canonical").Any(k => k.Value == "Control+Shift+ArrowDown"));
    }

    // Every key in `keys` is named, folded, by a row of `product`.
    private static void AssertNamed(string product, IEnumerable<string> keys)
    {
        var named = Rows.Where(r => r.Property == product)
            .SelectMany(r => r.List("Canonical"))
            .Select(k => Fold(k.Value!))
            .ToHashSet(StringComparer.Ordinal);
        var missing = keys.Where(k => !named.Contains(Fold(k))).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0,
            $"{product} takes these keys, and no {product} row of the Keyboard page names them: "
            + string.Join(", ", missing.Select(k => $"'{k}'"))
            + $". Add a KeyRow to KeyboardByHand.{product} that explains each, linking the section of the page that does, with the key in its Canonical list.");
    }

    // The keys ExSheet declares to its grid where the most are declared: the toolbar shown, with its toggle bound.
    private IReadOnlyCollection<string> SheetDeclared()
    {
        var sheet = Render<SheetComponent>(ps => ps
            .Add(s => s.ShowToolbar, true)
            .Add(s => s.ShowToolbarChanged, EventCallback.Factory.Create<bool>(this, (bool _) => { })));
        return [.. sheet.FindComponents<SheetGrid>().SelectMany(g => g.Instance.DeclaredKeys ?? [])];
    }

    // Whether ExGrid's core answers `key` itself: its table, the editor's keys, or the clipboard's. The core
    // refuses a Consumer's declaration of exactly those keys, by name (GridKeys.Declare, ADR-0050 item 14).
    private static bool CoreAnswers(string key)
    {
        Assert.True(KeyboardReference.IsCanonicalForm(key), $"'{key}' is not in the canonical form.");
        try
        {
            GridKeys.Declare([key]);
            return false;
        }
        catch (ArgumentException refused) when (refused.Message.Contains("one the core answers itself", StringComparison.Ordinal))
        {
            return true;
        }
    }

    /// <summary>
    /// A key as the rows and the tables are compared: a letter in either case, and a character typed with Control
    /// with Shift or without, are one key, since the tables take both forms — the first for CapsLock (GridKeys), the
    /// second for the layouts that need Shift to type the character (SheetFormatKeys, SheetStructureKeys).
    /// </summary>
    private static string Fold(string key)
    {
        var rest = key;
        var control = Take(ref rest, "Control+");
        var meta = Take(ref rest, "Meta+");
        var shift = Take(ref rest, "Shift+");
        var alt = Take(ref rest, "Alt+");
        if (rest.Length == 1 && char.IsAsciiLetter(rest[0]))
            rest = rest.ToLowerInvariant();
        else if (rest.Length == 1 && control && !meta && !alt)
            shift = false;
        return (control ? "Control+" : "") + (meta ? "Meta+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "") + rest;

        static bool Take(ref string rest, string prefix)
        {
            if (!rest.StartsWith(prefix, StringComparison.Ordinal) || rest.Length == prefix.Length)
                return false;
            rest = rest[prefix.Length..];
            return true;
        }
    }

    // KeyboardByHand's rows, read by the generator's own reader from the site's source, with the record they
    // construct beside them.
    private static ImmutableArray<HandRow> ReadRows()
    {
        var site = Path.Combine(Repository(), "samples", "ExGrid.Docs");
        var usings = CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.Linq;",
            cancellationToken: CancellationToken.None);
        var record = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(site, "Shared", "Keyboard.cs")),
            cancellationToken: CancellationToken.None);
        var hand = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(site, "Pages", "KeyboardByHand.cs")),
            cancellationToken: CancellationToken.None);
        var compilation = CSharpCompilation.Create("Keyboard", [usings, record, hand], GeneratorHarness.Platform,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var declaration = hand.GetRoot(CancellationToken.None).DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == KeyboardReference.ClassName);
        return HandReader.Read(declaration, compilation.GetSemanticModel(hand), KeyboardReference.Types, CancellationToken.None);
    }

    // The repository's root: the folder that holds ExGrid.slnx.
    private static string Repository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException($"no ExGrid.slnx above {AppContext.BaseDirectory}");
    }
}
