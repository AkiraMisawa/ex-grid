// Thin host: serves the standalone WASM client's static assets and collects
// benchmark results posted back from the browser. Deliberately mirrors the
// shape poke settled on (poke ADR-0003) so the numbers come from a realistic
// deployment, not a dev-server special case.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

var resultsDir = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "results"));
Directory.CreateDirectory(resultsDir);

app.MapPost("/api/results", async (HttpRequest req) =>
{
    using var reader = new StreamReader(req.Body);
    var json = await reader.ReadToEndAsync();

    var name = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}.json";
    var path = Path.Combine(resultsDir, name);
    await File.WriteAllTextAsync(path, json);

    Console.WriteLine($"[bench] wrote {path} ({json.Length} bytes)");
    return Results.Ok(new { file = name });
});

app.MapFallbackToFile("index.html");

Console.WriteLine($"[bench] results -> {resultsDir}");
app.Run();
