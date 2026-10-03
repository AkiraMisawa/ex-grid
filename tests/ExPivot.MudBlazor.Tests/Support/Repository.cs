using Xunit;

namespace ExPivot.MudBlazor.Tests.Support;

/// <summary>Files of the repository, found from its root rather than guessed from the output
/// path's depth, and refused if they are not there — a scan of something missing would pass by
/// finding nothing.</summary>
internal static class Repository
{
    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        return directory!.FullName;
    }

    internal static string Read(params string[] path)
    {
        var file = Path.Combine([Root(), .. path]);
        Assert.True(File.Exists(file), $"{file} is not there");
        return File.ReadAllText(file);
    }
}
