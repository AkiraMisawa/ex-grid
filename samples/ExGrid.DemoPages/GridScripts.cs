using ExGrid.Data;

namespace ExGrid.DemoPages;

/// <summary>The pages' functions for <c>ExGrid.Script.ExGrid</c> that the browser asks by position (ADR-0440), exported
/// from <c>wwwroot/grid-scripts.js</c>.</summary>
public static class GridScripts
{
    private const string Module = "./_content/ExGrid.DemoPages/grid-scripts.js";

    /// <summary>A Row Heading numbering rows from 1.</summary>
    public static readonly ScriptFunction HeadingNumber = ScriptFunction.Of(Module, "headingNumber");
}
