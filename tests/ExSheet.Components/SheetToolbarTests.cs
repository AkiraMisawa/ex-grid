using System.Reflection;
using System.Reflection.Emit;
using Bunit;
using ExGrid;
using ExGrid.Chrome;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Sheet Toolbar (ADR-0100; ticket 54; SH-48 to SH-50): shown only when asked for, above the
/// Formula Bar inside the Sheet's box, its height a count of rows resolved in C# and taken from the
/// grid's; its content declared in markup order, the default row when none is; each item's state
/// following the Focus cell, its press acting through the public commands as one undo step, every
/// item unavailable while an edit is open; one tab stop whose keys move among the items; and the same
/// meaning under a substituted Chrome.
/// </summary>
public class SheetToolbarTests : SheetTestContext
{
    // The Formula Bar's height under the default Density, which one Toolbar Row takes (ADR-0100).
    private static readonly double RowPx = GridMetrics.Resolve(GridDensity.Compact, SheetComponent.DefaultRowHeightPx).FormulaBarHeightPx;

    private IRenderedComponent<SheetComponent> RenderToolbarSheet(Action<ComponentParameterCollectionBuilder<SheetComponent>>? parameters = null) =>
        RenderSheet(ps =>
        {
            ps.Add(s => s.ShowToolbar, true);
            parameters?.Invoke(ps);
        });

    private static IReadOnlyList<string> ItemNames(IRenderedComponent<SheetComponent> cut) =>
        [.. cut.FindAll(".ex-sheet-toolbar [aria-label]").Where(e => !e.ClassList.Contains("ex-sheet-toolbar-row")
            && !e.ClassList.Contains("ex-sheet-toolbar") && !e.ClassList.Contains("ex-sheet-toolbar-face")
            && !e.ClassList.Contains("ex-sheet-toolbar-arrow")).Select(e => e.GetAttribute("aria-label")!)];

    private static AngleSharp.Dom.IElement Item(IRenderedComponent<SheetComponent> cut, string name) =>
        cut.FindAll(".ex-sheet-toolbar [aria-label]").Single(e => e.GetAttribute("aria-label") == name
            && !e.ClassList.Contains("ex-sheet-toolbar-arrow") && !e.ClassList.Contains("ex-sheet-toolbar-face"));

    private static Task PressItemAsync(IRenderedComponent<SheetComponent> cut, string name)
    {
        var item = Item(cut, name);
        var face = item.ClassList.Contains("ex-sheet-toolbar-split") ? item.QuerySelector(".ex-sheet-toolbar-face")! : item;
        return face.ClickAsync(new MouseEventArgs());
    }

    private static CellFormat FormatAt(IRenderedComponent<SheetComponent> cut, string address) =>
        cut.Instance.CellFormatAt(CellAddress.Parse(address));

    // ---- Shown only when asked for, and where (SH-48) ----

    [Fact] // ADR-0100, SH-48: a Sheet shows no toolbar unless asked, and its grid keeps the whole height
    public void A_sheet_shows_no_toolbar_unless_asked()
    {
        var cut = RenderSheet();

        Assert.Empty(cut.FindAll(".ex-sheet-toolbar"));
        Assert.Equal((ViewportSize)400, Grid(cut).Instance.ViewportHeight);
        Assert.Equal("display: contents", cut.Find(".ex-sheet").GetAttribute("style"));
    }

    [Fact] // ADR-0100, SH-48: shown, it stands above the grid inside the Sheet's box and outside the grid's root
    public void The_toolbar_stands_above_the_grid_outside_its_root()
    {
        var cut = RenderToolbarSheet();

        var frame = cut.Find(".ex-sheet");
        var toolbar = cut.Find(".ex-sheet-toolbar");
        Assert.Contains("ex-sheet-toolbar", frame.Children[0].ClassName);
        Assert.Contains("ex-grid", frame.Children[1].ClassName);
        Assert.Null(toolbar.Closest(".ex-grid"));
        Assert.Equal("toolbar", toolbar.GetAttribute("role"));
        Assert.Equal("0", toolbar.GetAttribute("tabindex"));
        Assert.Contains("width: 700px", frame.GetAttribute("style"));
    }

    [Fact] // ADR-0100, ADR-0028, SH-48: the Sheet's height includes the toolbar, and the grid is given what it leaves
    public void The_grid_is_given_what_the_toolbar_leaves()
    {
        var cut = RenderToolbarSheet();

        Assert.Equal((ViewportSize)(400 - RowPx), Grid(cut).Instance.ViewportHeight);
        Assert.Contains($"height: {RowPx}px", cut.Find(".ex-sheet-toolbar").GetAttribute("style"));
        Assert.Contains($"height: {RowPx}px", cut.Find(".ex-sheet-toolbar-row").GetAttribute("style"));
    }

    [Fact] // ADR-0100, SH-48: two Toolbar Rows take twice one row's height from the grid
    public void Two_rows_take_twice_the_height()
    {
        var page = RenderPage<SheetWithToolbar>();
        page.Render(ps => ps.Add(p => p.TwoRows, true));
        var cut = page.FindComponent<SheetComponent>();

        Assert.Equal(2, cut.FindAll(".ex-sheet-toolbar-row").Count);
        Assert.Contains($"height: {2 * RowPx}px", cut.Find(".ex-sheet-toolbar").GetAttribute("style"));
        Assert.Equal((ViewportSize)(400 - 2 * RowPx), Grid(cut).Instance.ViewportHeight);
    }

    [Fact] // ADR-0100, SH-48: hiding and showing the toolbar keeps the same grid, and its Selection
    public async Task Hiding_and_showing_keeps_the_grid()
    {
        var page = RenderPage<SheetWithToolbar>();
        var cut = page.FindComponent<SheetComponent>();
        var grid = Grid(cut).Instance;
        await GoToAsync(cut, "C3");

        page.Render(ps => ps.Add(p => p.Shown, false));
        Assert.Empty(cut.FindAll(".ex-sheet-toolbar"));
        Assert.Equal((ViewportSize)400, Grid(cut).Instance.ViewportHeight);
        page.Render(ps => ps.Add(p => p.Shown, true));

        Assert.Same(grid, Grid(cut).Instance);
        Assert.Equal(new ExGrid.Selection.CellPosition(2, 2), grid.ReadSelection().Selection.Focus);
        Assert.NotEmpty(cut.FindAll(".ex-sheet-toolbar"));
    }

    [Fact] // ADR-0100: under a Stretch height the Sheet's box is a column, and the grid takes what the toolbar leaves
    public void Under_stretch_the_box_is_a_column()
    {
        var cut = RenderToolbarSheet();
        cut.Render(ps => ps.Add(s => s.ViewportHeight, ViewportSize.Stretch));

        Assert.Contains("flex-direction: column", cut.Find(".ex-sheet").GetAttribute("style"));
        Assert.True(Grid(cut).Instance.ViewportHeight.IsStretch);
    }

    [Fact] // ADR-0100: a declared height that leaves the grid nothing is refused by name
    public void A_height_that_leaves_the_grid_nothing_is_refused()
    {
        var cut = RenderToolbarSheet();
        var ex = Assert.Throws<ArgumentException>(() => cut.Render(ps => ps.Add(s => s.ViewportHeight, (ViewportSize)RowPx)));
        Assert.Contains("Sheet Toolbar", ex.Message);
    }

    // ---- What it holds, in markup order (SH-49) ----

    [Fact] // ADR-0100, SH-49: the default row is Excel's Home tab's formatting commands, in order
    public void The_default_row_is_the_formatting_commands_in_order()
    {
        var cut = RenderToolbarSheet();

        Assert.Equal(
            ["Bold", "Italic", "Underline", "Strikethrough", "Font Colour", "Fill Colour", "Borders",
             "Align Left", "Centre", "Align Right", "Number Format", "Percent Style", "Comma Style", "Format Cells"],
            ItemNames(cut));
        Assert.Equal(5, cut.FindAll(".ex-sheet-toolbar-separator").Count);
    }

    [Fact] // ADR-0100, SH-49: the Consumer's content is laid out in markup order, its own button among the items
    public void Declared_content_is_laid_out_in_markup_order()
    {
        var page = RenderPage<SheetWithToolbar>();
        var cut = page.FindComponent<SheetComponent>();

        Assert.Equal(["Italic", "Approve", "Bold"], ItemNames(cut));
    }

    [Fact] // ADR-0100: a Toolbar Item outside an ExSheet's ToolbarContent is refused by name
    public void An_item_outside_a_toolbar_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Render<BoldItem>());
        Assert.Contains("ToolbarContent", ex.Message);
    }

    [Fact] // ADR-0100, SH-49: ExSheet's items reach the Sheet only through its public commands
    public void Items_reach_the_sheet_only_through_public_members()
    {
        var assembly = typeof(ToolbarItemBase).Assembly;
        var itemTypes = assembly.GetTypes().Where(t => typeof(ToolbarItemBase).IsAssignableFrom(t) || t == typeof(SheetToolbarContext)).ToList();
        Assert.Contains(typeof(BoldItem), itemTypes);
        var reached = new List<string>();
        foreach (var type in itemTypes.Concat(itemTypes.SelectMany(NestedTypes)))
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)))
            {
                foreach (var member in MembersCalled(method))
                {
                    if (member.DeclaringType == typeof(SheetComponent) && !IsPublic(member)) reached.Add($"{type.Name}.{method.Name} → {member.Name}");
                }
            }
        }
        Assert.Empty(reached);
    }

    private static IEnumerable<Type> NestedTypes(Type type) =>
        type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(t => NestedTypes(t).Prepend(t));

    private static bool IsPublic(MemberInfo member) => member switch
    {
        MethodBase method => method.IsPublic,
        FieldInfo field => field.IsPublic,
        _ => true,
    };

    // Every method, constructor and field a method's IL names, read from its call and field opcodes.
    private static IEnumerable<MemberInfo> MembersCalled(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;
        var oneByte = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!)
            .Where(o => o.Size == 1).ToDictionary(o => o.Value);
        var twoByte = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!)
            .Where(o => o.Size == 2).ToDictionary(o => (byte)(o.Value & 0xFF));
        for (var at = 0; at < il.Length;)
        {
            OpCode op = il[at] == 0xFE ? twoByte[il[++at]] : oneByte[il[at]];
            at++;
            switch (op.OperandType)
            {
                case OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, at);
                    MemberInfo? member = null;
                    try
                    {
                        member = method.Module.ResolveMember(token, method.DeclaringType?.GetGenericArguments(),
                            method.IsGenericMethod ? method.GetGenericArguments() : null);
                    }
                    catch (ArgumentException)
                    {
                    }
                    if (member is not null) yield return member;
                    at += 4;
                    break;
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar: at += 1; break;
                case OperandType.InlineVar: at += 2; break;
                case OperandType.InlineI8 or OperandType.InlineR: at += 8; break;
                case OperandType.InlineSwitch: at += 4 + 4 * BitConverter.ToInt32(il, at); break;
                default: at += 4; break;
            }
        }
    }

    // ---- State and press (SH-50) ----

    [Fact] // ADR-0100, SH-50: Bold is shown pressed while the Focus cell is bold, and follows the Selection
    public async Task Bold_shows_pressed_over_a_bold_focus()
    {
        var cut = RenderToolbarSheet(ps => ps.Add(s => s.Document, DocumentWith(sheet =>
            sheet.SetCellFormat([CellRange.Parse("B2")], new CellFormatChange { Bold = true }))));

        await GoToAsync(cut, "B2");
        Assert.Equal("true", Item(cut, "Bold").GetAttribute("aria-pressed"));
        Assert.Equal("false", Item(cut, "Italic").GetAttribute("aria-pressed"));

        await GoToAsync(cut, "C2");
        Assert.Equal("false", Item(cut, "Bold").GetAttribute("aria-pressed"));
    }

    [Fact] // ADR-0100, ADR-0071, SH-50: a press sets bold over the Selection, in the Focus cell's direction, as one undo step
    public async Task A_press_toggles_bold_as_one_step()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2:C3");

        await PressItemAsync(cut, "Bold");

        Assert.True(FormatAt(cut, "B2").Font.Bold);
        Assert.True(FormatAt(cut, "C3").Font.Bold);
        Assert.Equal("true", Item(cut, "Bold").GetAttribute("aria-pressed"));

        await PressItemAsync(cut, "Bold");
        Assert.False(FormatAt(cut, "C3").Font.Bold);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.True(FormatAt(cut, "C3").Font.Bold);
    }

    [Fact] // ADR-0100: an alignment is pressed while the Focus cell has it, and pressing it again sets General
    public async Task An_alignment_toggles_back_to_general()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");

        await PressItemAsync(cut, "Centre");
        Assert.Equal(HorizontalAlignment.Center, FormatAt(cut, "B2").Alignment);
        Assert.Equal("true", Item(cut, "Centre").GetAttribute("aria-pressed"));

        await PressItemAsync(cut, "Centre");
        Assert.Equal(HorizontalAlignment.General, FormatAt(cut, "B2").Alignment);
    }

    [Fact] // ADR-0100: a colour's face sets the colour on it; its list sets the one chosen and the face keeps it
    public async Task A_colour_face_sets_its_colour_and_the_list_changes_it()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");

        await PressItemAsync(cut, "Fill Colour");
        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0xFFFF00)), FormatAt(cut, "B2").Fill);

        await Item(cut, "Fill Colour").QuerySelector(".ex-sheet-toolbar-arrow")!.ClickAsync(new MouseEventArgs());
        var blue = cut.FindAll(".ex-popover .ex-sheet-swatch-choice").First(b => b.GetAttribute("aria-label") == "Blue");
        await blue.ClickAsync(new MouseEventArgs());
        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0x0070C0)), FormatAt(cut, "B2").Fill);
        Assert.Empty(cut.FindAll(".ex-popover .ex-sheet-choices"));

        await GoToAsync(cut, "C2");
        await PressItemAsync(cut, "Fill Colour");
        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0x0070C0)), FormatAt(cut, "C2").Fill);
    }

    [Fact] // ADR-0100: the Number Format lists Excel's categories, Accounting and Fraction disabled with the reason
    public async Task The_number_format_lists_excels_categories()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");
        Assert.Contains("General", Item(cut, "Number Format").TextContent);

        await PressItemAsync(cut, "Number Format");
        var choices = cut.FindAll(".ex-popover .ex-sheet-choice");
        Assert.Equal(
            ["General", "Number", "Currency", "Accounting", "Date", "Time", "Percentage", "Fraction", "Scientific", "Text", "More Number Formats…"],
            choices.Select(c => c.QuerySelector(".ex-sheet-choice-label")!.TextContent));
        Assert.Equal("true", choices.Single(c => c.TextContent.StartsWith("Accounting")).GetAttribute("aria-disabled"));
        Assert.Equal("true", choices.Single(c => c.TextContent.StartsWith("Fraction")).GetAttribute("aria-disabled"));

        await choices.Single(c => c.TextContent.StartsWith("Percentage")).ClickAsync(new MouseEventArgs());
        Assert.Equal("0.00%", FormatAt(cut, "B2").NumberFormat.Code);
        Assert.Contains("Percentage", Item(cut, "Number Format").TextContent);
    }

    [Fact] // ADR-0100: Comma Style is shown and disabled with the reason, never set as something else
    public void Comma_style_is_disabled_with_the_reason()
    {
        var cut = RenderToolbarSheet();

        var comma = Item(cut, "Comma Style");
        Assert.True(comma.HasAttribute("disabled"));
        Assert.Contains("*", comma.GetAttribute("title"));
    }

    [Fact] // ADR-0100, SH-50: while an edit is open every item, the Consumer's included, cannot be pressed
    public async Task Every_item_is_unavailable_while_an_edit_is_open()
    {
        var page = RenderPage<SheetWithToolbar>();
        var cut = page.FindComponent<SheetComponent>();
        await GoToAsync(cut, "B2");
        Assert.All(cut.FindAll(".ex-sheet-toolbar button"), b => Assert.False(b.HasAttribute("disabled")));

        await PressAsync(cut, "x");
        Assert.True(cut.Instance.IsEditing);

        Assert.All(cut.FindAll(".ex-sheet-toolbar button"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.Contains("being edited", Item(cut, "Approve").GetAttribute("title"));

        await PressAsync(cut, "Escape");
        Assert.All(cut.FindAll(".ex-sheet-toolbar button"), b => Assert.False(b.HasAttribute("disabled")));
    }

    [Fact] // ADR-0100, SH-50: a pointer press does not take DOM focus, so the keyboard stays on the Sheet
    public void A_pointer_press_does_not_take_focus()
    {
        var cut = RenderToolbarSheet();

        Assert.All(cut.FindAll(".ex-sheet-toolbar button"), b =>
        {
            Assert.Equal("-1", b.GetAttribute("tabindex"));
            Assert.True(b.HasAttribute("blazor:onmousedown:preventdefault"), b.OuterHtml);
        });
    }

    [Fact] // ADR-0100, SH-50: the Consumer's own button runs its action
    public async Task The_consumers_button_runs_its_action()
    {
        var page = RenderPage<SheetWithToolbar>();
        var cut = page.FindComponent<SheetComponent>();

        await PressItemAsync(cut, "Approve");

        Assert.Equal(1, page.Instance.Approvals);
    }

    // ---- The toolbar's keys (SH-50) ----

    [Fact] // ADR-0100, SH-50: the toolbar is one tab stop; → moves among the items, skipping separators, and Enter presses one
    public async Task The_arrow_keys_move_and_enter_presses()
    {
        var cut = RenderToolbarSheet();
        await GoToAsync(cut, "B2");
        var toolbar = cut.Find(".ex-sheet-toolbar");

        await toolbar.FocusAsync(new FocusEventArgs());
        Assert.Equal(Item(cut, "Bold").Id, cut.Find(".ex-sheet-toolbar").GetAttribute("aria-activedescendant"));

        foreach (var _ in Enumerable.Range(0, 4)) await cut.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(Item(cut, "Font Colour").Id, cut.Find(".ex-sheet-toolbar").GetAttribute("aria-activedescendant"));

        await cut.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        var reclaimed = ReclaimCount;
        await cut.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.True(FormatAt(cut, "B2").Font.Bold);
        // A command run from the toolbar's keys gives the keyboard back to the Sheet.
        Assert.True(ReclaimCount > reclaimed);
    }

    [Fact] // ADR-0100: Escape on the toolbar gives the keyboard back to the Sheet
    public async Task Escape_gives_the_keyboard_back()
    {
        var cut = RenderToolbarSheet();
        var reclaimed = ReclaimCount;

        await cut.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.True(ReclaimCount > reclaimed);
    }

    // ---- The Chrome draws, the meaning stays (SH-50) ----

    [Fact] // ADR-0100, ADR-0010, SH-50: a substituted Chrome draws every item, and a press means the same
    public async Task A_substituted_chrome_draws_the_items_with_the_same_meaning()
    {
        var chrome = new TextChrome();
        var cut = RenderToolbarSheet(ps => ps.Add(s => s.Chrome, chrome));
        await GoToAsync(cut, "B2");

        Assert.Empty(cut.FindAll(".ex-sheet-toolbar-item"));
        var bold = cut.FindAll(".text-chrome-item").Single(b => b.TextContent == "Bold");
        await bold.ClickAsync(new MouseEventArgs());

        Assert.True(FormatAt(cut, "B2").Font.Bold);
        Assert.Contains(chrome.Drawn, c => c.Name == "Bold" && c.Pressed);
    }

    private static SheetDocument DocumentWith(Action<Sheet> prepare)
    {
        var sheet = new Sheet(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        prepare(sheet);
        return sheet.ToDocument();
    }

    /// <summary>A Chrome of the test's own that draws each item as a plain button naming it.</summary>
    private sealed class TextChrome : ISheetChrome
    {
        public List<ToolbarItemContext> Drawn { get; } = [];

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? ToolbarItem(ToolbarItemContext context)
        {
            Drawn.Add(context);
            return builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "class", "text-chrome-item");
                builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => context.Invoke()));
                builder.AddContent(3, context.Name);
                builder.CloseElement();
            };
        }
    }
}
