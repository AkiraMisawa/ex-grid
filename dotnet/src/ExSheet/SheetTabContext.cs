namespace ExSheet;

/// <summary>
/// What a Sheet Tab shows (ADR-0331 §1, §4), for a Chrome that draws its face
/// (<see cref="ISheetChrome.SheetTab"/>). ExSheet keeps the tab itself — the element that takes the
/// keyboard, says it is a tab to assistive technology, and hears the press, the double click, the
/// context menu and the drag — and the Chrome draws what stands inside it, which takes no keyboard and
/// hears nothing.
/// </summary>
/// <param name="Name">The Sheet's name, which the face shows as its only text.</param>
/// <param name="Selected">Whether the Sheet is the one shown.</param>
public sealed record SheetTabContext(string Name, bool Selected);
