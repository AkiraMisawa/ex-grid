namespace ExGrid.Keys;

/// <summary>
/// A key the Consumer declared, pressed on this grid (ADR-0050, item 14). The core claimed it from
/// the browser and does nothing else with it: what it means is the Consumer's, and while an edit is
/// open the Consumer decides whether it means anything at all. ExSheet's formatting keys are
/// refused then, and say why (ADR-0063).
/// </summary>
/// <param name="Key">The key in <see cref="GridKeys.Canonical"/>'s form, exactly as it was declared
/// — <c>Control+b</c>, <c>Control+Shift+$</c>. Command folds into Control where it is the Primary
/// Modifier (ADR-0012).</param>
/// <param name="EditOpen">Whether an edit was open when it was pressed: in the Cell Editor or the
/// Formula Bar, in any of its states. The key changed nothing in the editor either way.</param>
public readonly record struct GridDeclaredKeyPress(string Key, bool EditOpen);
