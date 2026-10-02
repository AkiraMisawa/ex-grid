using ExGrid.Selection;

namespace ExGrid.Keys;

/// <summary>
/// A key the Consumer declared, pressed on this grid (ADR-0050, item 14). The core claimed it from
/// the browser and does nothing else with it: what it means is the Consumer's, and while an edit is
/// open the Consumer decides whether it means anything at all. ExSheet's formatting keys are
/// refused then, and say why (ADR-0071).
/// </summary>
/// <param name="Key">The key in <see cref="GridKeys.Canonical"/>'s form, exactly as it was declared
/// — <c>Control+b</c>, <c>Control+Shift+$</c>. Command folds into Control where it is the Primary
/// Modifier (ADR-0012).</param>
/// <param name="EditOpen">Whether an edit was open when it was pressed: in the Cell Editor or the
/// Formula Bar, in any of its states. The key changed nothing in the editor either way.</param>
/// <param name="Selection">The Selection as the grid holds it at the key — the one the key acts on.
/// <c>SelectionChanged</c> is raised after the render that shows a move, which on a circuit is a
/// round trip later: a key pressed straight after Shift+arrow can arrive before the Consumer has
/// heard the move, and a Consumer acting on the Selection it last heard would act on fewer cells
/// than are selected.</param>
/// <param name="RowSequenceVersion">The Row Sequence Version <paramref name="Selection"/> is written
/// in (ADR-0011), as every other notification that carries positions says it.</param>
public readonly record struct GridDeclaredKeyPress(string Key, bool EditOpen, GridSelection Selection, int RowSequenceVersion);
