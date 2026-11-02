// The stylesheet each Script Core package carries to npm: the very file its product's NuGet package serves,
// minified, with its source map (ADR-0123), so a React Consumer and a Blazor Consumer read the same rules and the
// Script Core's stylesheet notice is hidden by the stamp it was built with (ADR-0370 section 5's note of
// 2026-11-02). The source stays in dotnet/src/<Package>/Assets/ and tools/assets/minify.mjs writes the files named
// here; the build copies them into the package's directory, where the package exports the first as ./styles.css.

/** Each package directory under packages/ that carries a stylesheet, and the minified file it carries. */
export const stylesheets = Object.freeze({
    core: 'dotnet/src/ExGrid/wwwroot/ex-grid.min.css',
    sheet: 'dotnet/src/ExSheet/wwwroot/ex-sheet.min.css',
    pivot: 'dotnet/src/ExPivot/wwwroot/ex-pivot.min.css',
    chart: 'dotnet/src/ExChart/wwwroot/ex-chart.min.css',
});
