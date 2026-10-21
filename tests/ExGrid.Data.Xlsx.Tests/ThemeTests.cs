using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// Theme colours resolved to the RGB Excel paints (ADR-0330 §8): a style's theme slot through the workbook's own theme, the
/// first four slots in the order styles number them, the indexed palette and its replacement, and the tint as ECMA-376
/// §18.8.19 applies it, checked against the values Excel shows for its own palette.
/// </summary>
public class ThemeTests
{
    private const string Theme = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Custom">
          <a:themeElements>
            <a:clrScheme name="Custom">
              <a:dk1><a:sysClr val="windowText" lastClr="111111"/></a:dk1>
              <a:lt1><a:sysClr val="window" lastClr="FEFEFE"/></a:lt1>
              <a:dk2><a:srgbClr val="1F497D"/></a:dk2>
              <a:lt2><a:srgbClr val="EEECE1"/></a:lt2>
              <a:accent1><a:srgbClr val="4F81BD"/></a:accent1>
              <a:accent2><a:srgbClr val="C0504D"/></a:accent2>
              <a:accent3><a:srgbClr val="9BBB59"/></a:accent3>
              <a:accent4><a:srgbClr val="8064A2"/></a:accent4>
              <a:accent5><a:srgbClr val="4BACC6"/></a:accent5>
              <a:accent6><a:srgbClr val="F79646"/></a:accent6>
              <a:hlink><a:srgbClr val="0000FF"/></a:hlink>
              <a:folHlink><a:srgbClr val="800080"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="Office"><a:majorFont><a:latin typeface="Cambria"/></a:majorFont><a:minorFont><a:latin typeface="Calibri"/></a:minorFont></a:fontScheme>
          </a:themeElements>
        </a:theme>
        """;

    [Fact] // ADR-0330 §8: the theme part's colour scheme is read in its order, a system colour by the value it was saved with
    public async Task The_theme_part_is_read()
    {
        using var reader = await OpenAsync(Book(Sheet(""), theme: Theme));

        Assert.True(reader.HasTheme);
        Assert.Equal(
            [0xFF111111u, 0xFFFEFEFE, 0xFF1F497D, 0xFFEEECE1, 0xFF4F81BD, 0xFFC0504D, 0xFF9BBB59, 0xFF8064A2, 0xFF4BACC6, 0xFFF79646, 0xFF0000FF, 0xFF800080],
            reader.Theme.SchemeColors);
    }

    [Fact] // ADR-0330 §8: a style numbers the first four slots light 1, dark 1, light 2, dark 2 — Background 1 is 0, Text 1 is 1
    public void A_style_numbers_the_first_four_slots_background_first()
    {
        var theme = XlsxTheme.Office;

        Assert.Equal(0xFFFFFFFFu, theme.Resolve(XlsxColor.FromTheme(0)));
        Assert.Equal(0xFF000000u, theme.Resolve(XlsxColor.FromTheme(1)));
        Assert.Equal(0xFFE7E6E6u, theme.Resolve(XlsxColor.FromTheme(2)));
        Assert.Equal(0xFF44546Au, theme.Resolve(XlsxColor.FromTheme(3)));
        Assert.Equal(0xFF4472C4u, theme.Resolve(XlsxColor.FromTheme(4)));
        Assert.Equal(0xFF954F72u, theme.Resolve(XlsxColor.FromTheme(11)));
        Assert.Null(theme.Resolve(XlsxColor.FromTheme(12)));
    }

    // The tints Excel writes for its palette's shades, and the colours its own Format Cells shows for them, for the black
    // and white of every Office theme: "White, Background 1, Darker 5%" is F2F2F2, "Black, Text 1, Lighter 50%" 7F7F7F.
    public static TheoryData<int, double, uint> ExcelShades => new()
    {
        { 0, -0.0499893185216834, 0xFFF2F2F2 },
        { 0, -0.149998474074526, 0xFFD9D9D9 },
        { 0, -0.249977111117893, 0xFFBFBFBF },
        { 0, -0.349986266670736, 0xFFA6A6A6 },
        { 0, -0.499984740745262, 0xFF808080 },
        { 1, 0.499984740745262, 0xFF7F7F7F },
        { 1, 0.349986266670736, 0xFF595959 },
        { 1, 0.249977111117893, 0xFF404040 },
        { 1, 0.149998474074526, 0xFF262626 },
        { 1, 0.0499893185216834, 0xFF0D0D0D },
    };

    [Theory] // ADR-0330 §8: a theme slot with a tint resolves to the colour Excel shows for that shade of its palette
    [MemberData(nameof(ExcelShades))]
    public void A_tint_resolves_as_Excel_paints_it(int slot, double tint, uint argb)
    {
        Assert.Equal(argb, XlsxTheme.Office.Resolve(XlsxColor.FromTheme(slot, tint)));
    }

    [Fact] // ADR-0330 §8: the tint follows ECMA-376 §18.8.19 — luminance scaled toward black or white, hue and saturation kept
    public void A_tint_moves_only_the_luminance()
    {
        Assert.Equal(0xFF4472C4u, XlsxTheme.ApplyTint(0xFF4472C4, 0));
        Assert.Equal(0xFFFFFFFFu, XlsxTheme.ApplyTint(0xFF4472C4, 1));
        Assert.Equal(0xFF000000u, XlsxTheme.ApplyTint(0xFF4472C4, -1));
        // Darkening by half halves the luminance and keeps the hue: 4472C4 (L 51.8%) to L 25.9%.
        Assert.Equal(0xFF203864u, XlsxTheme.ApplyTint(0xFF4472C4, -0.5));
        // Alpha is kept.
        Assert.Equal(0x80FFFFFFu, XlsxTheme.ApplyTint(0x80000000, 1));
    }

    [Fact] // ADR-0330 §8: an indexed colour resolves through the default palette, or the one the styles part writes over it; automatic resolves to none
    public void Indexed_and_automatic_colours_resolve()
    {
        var theme = XlsxTheme.Office;

        Assert.Equal(0xFFFF0000u, theme.Resolve(XlsxColor.FromIndexed(10)));
        Assert.Equal(0xFF333333u, theme.Resolve(XlsxColor.FromIndexed(63)));
        Assert.Null(theme.Resolve(XlsxColor.FromIndexed(64)));
        Assert.Equal(0xFF123456u, theme.Resolve(XlsxColor.FromIndexed(1), [0xFF000000, 0xFF123456]));
        Assert.Null(theme.Resolve(XlsxColor.Automatic));
        Assert.Equal(0xFFC00000u, theme.Resolve(XlsxColor.FromArgb(0xFFC00000)));
    }

    [Fact] // ADR-0330 §8: a workbook's own theme resolves its fills, read from its styles part with their tints and its indexed palette
    public async Task A_workbooks_styles_resolve_through_its_own_theme()
    {
        const string styles = $"""
            <styleSheet xmlns="{Main}">
              <fonts count="1"><font><sz val="11"/><color theme="1"/><name val="Calibri"/></font></fonts>
              <fills count="3">
                <fill><patternFill patternType="none"/></fill>
                <fill><patternFill patternType="gray125"/></fill>
                <fill><patternFill patternType="solid"><fgColor theme="4" tint="0.79998168889431442"/><bgColor indexed="64"/></patternFill></fill>
              </fills>
              <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
              <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/><xf numFmtId="0" fontId="0" fillId="2" borderId="0" applyFill="1"/></cellXfs>
              <colors><indexedColors><rgbColor rgb="FF010203"/><rgbColor rgb="FF040506"/></indexedColors></colors>
            </styleSheet>
            """;
        using var reader = await OpenAsync(Book(Sheet(""), styles: styles, theme: Theme));

        var fill = reader.Styles.Fills[2];

        Assert.Equal(XlsxFill.Solid(XlsxColor.FromTheme(4, 0.79998168889431442)) with { Background = XlsxColor.FromIndexed(64) }, fill);
        Assert.Equal(XlsxTheme.ApplyTint(0xFF4F81BD, 0.79998168889431442), reader.Theme.Resolve(fill.Foreground!.Value, reader.Styles.IndexedColors));
        Assert.Equal(0xFF111111u, reader.Theme.Resolve(reader.Styles.Fonts[0].Color!.Value));
        Assert.Equal([0xFF010203u, 0xFF040506u], reader.Styles.IndexedColors);
    }
}
