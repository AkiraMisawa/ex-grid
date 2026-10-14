// The two layer-2 probes of §6, as run: each was appended for the sweep to the test class named in
// its comment, in tests/ExGrid.MudBlazor.Tests, and removed after it. They assert nothing; each writes
// what it reads to a file named for the MudBlazor version the test process loaded. <out> stands for the
// scratch directory they wrote to.

// In MudFilterPanelTests: the Operator's focusable element and its inputs, role and name.
[Fact]
public void Zz_probe_operator_aria()
{
    var cut = RenderPanel(Context(ColumnType.Text, FilterUiMode.Condition));
    var op = cut.Find(".mud-ex-grid-filter-operator");
    var v = typeof(global::MudBlazor.MudSelect<int>).Assembly.GetName().Version;
    var lines = new List<string>();
    foreach (var e in op.QuerySelectorAll("[tabindex='0']"))
        lines.Add("focusable " + e.TagName + " role=" + e.GetAttribute("role") + " aria-label=" + e.GetAttribute("aria-label"));
    foreach (var e in op.QuerySelectorAll("input"))
        lines.Add("input type=" + e.GetAttribute("type") + " role=" + e.GetAttribute("role") + " aria-label=" + e.GetAttribute("aria-label"));
    System.IO.File.WriteAllLines("<out>/operator-" + v + ".txt", lines);
}

// In MudNumberOperandTests: the refused operand's whole markup, after a text that reads two ways.
[Fact]
public async Task Zz_probe_operand_markup()
{
    using var _ = new CultureScope("de-DE");
    var cut = RenderPanel(null);
    await cut.Find(".mud-ex-grid-filter-operand input").InputAsync(new ChangeEventArgs { Value = "1.234" });
    await cut.Find("form").SubmitAsync();
    System.IO.File.WriteAllText("<out>/operand-" + typeof(global::MudBlazor.MudTextField<string>).Assembly.GetName().Version + ".html",
        cut.Find(".mud-ex-grid-filter-operand").OuterHtml);
}

// In MudNumberOperandTests, run alone at 9.5.0: whether Apply stays unavailable and nothing is applied.
[Fact]
public async Task Zz_probe_operand_apply()
{
    using var _ = new CultureScope("de-DE");
    var cut = RenderPanel(null);
    await cut.Find(".mud-ex-grid-filter-operand input").InputAsync(new ChangeEventArgs { Value = "1.234" });
    await cut.Find("form").SubmitAsync();
    System.IO.File.WriteAllText("<out>/apply-" + typeof(global::MudBlazor.MudTextField<string>).Assembly.GetName().Version + ".txt",
        "apply disabled=" + cut.Find(".mud-ex-grid-filter-apply").HasAttribute("disabled") + " applied=" + _applied.Count
        + " operand text=" + cut.Find(".mud-ex-grid-filter-operand").TextContent);
}
