dotnet add package ExGrid --prerelease
dotnet add package ExSheet --prerelease             # the sheet; ExSheet.Engine alone computes on a server
dotnet add package ExPivot --prerelease             # the pivot table; ExPivot.Engine alone aggregates
dotnet add package ExGrid.Data.Arrow --prerelease   # a Snapshot as Apache Arrow

# In a MudBlazor application, each product's Wrapper as well
dotnet add package ExGrid.MudBlazor --prerelease
dotnet add package ExSheet.MudBlazor --prerelease
dotnet add package ExPivot.MudBlazor --prerelease
