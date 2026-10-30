Product: ExPivot
Kind: Fixed
ADR: 0370

Blazor's ExPivot over the Script Core (`ExPivot.Components.ExPivot`) asks its source once for a Refresh: the source's own change notice, raised while the Refresh is under way, no longer asks a second time. A question the C# source fails is shown in the source's own words, as the Razor component shows it, rather than as the bridge's "There was an exception invoking …". Both hold for a pivot in Blazor's ExBoard over the Script Core too, which crosses its C# sources the same way. And a placed field's menu stays open when the keyboard comes back to the entry's button, as a component library's menu hands it back: the entry's button, handed in by `control`, is inside its menu wherever the Chrome's anchor stands, beside the button as MudPivotChrome draws it or around it as the built-in pane does, in the TypeScript pivot and every Wrapper over it.
