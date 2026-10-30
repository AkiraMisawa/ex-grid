namespace ExGrid.Columns;

/// <summary>
/// What a press on a Header Group's toggle, or Alt+Shift+Down or Up from a cell under the
/// group, notifies (ADR-0400 section 5). The Consumer answers by handing back the columns and
/// the group's new <see cref="HeaderGroup.Expanded"/>; the grid adds and removes no column.
/// </summary>
/// <param name="Id">The group's <see cref="HeaderGroup.Id"/>.</param>
/// <param name="Expanded">The state asked for: the opposite of the one the group showed when
/// the input was taken, so two inputs before the answer ask the same.</param>
/// <param name="FocusColumn">The Focus's column when the input was taken, by name, or
/// <see langword="null"/> where there was no Focus.</param>
public sealed record HeaderGroupToggle(string Id, bool Expanded, string? FocusColumn);
