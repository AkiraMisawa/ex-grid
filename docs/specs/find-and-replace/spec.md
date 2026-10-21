# Find in every product, Replace, and the Match Mark

Status: ready-for-agent

Decided in [ADR-0332](../../adr/0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md)
(2026-10-20 and 2026-10-21). This spec gathers it into work and decides nothing the ADR does not.

## Problem Statement

ExGrid finds but cannot replace, ExPivot cannot find at all, and none of them shows which cells in view
match, so a user steps through matches one by one without seeing where the others are.

## Solution

Find in ExGrid, ExSheet and ExPivot; Replace and Replace All (Ctrl+H) in ExGrid and ExSheet where cells
can be written; every match in view marked as the user scrolls; ExSheet's Within: Sheet or Workbook.

## User Stories

1. As a user, I want every matching cell on screen marked while I search, and the current one apart, so that I see where the matches are.
2. As a user, I want matches I scroll to marked as they appear.
3. As a user of an editable grid, I want Ctrl+H to replace the current match or every match at once.
4. As a user, I want Replace All refused whole, naming the cells, when any match cannot be written, so that nothing is half-replaced.
5. As a user, I want Replace All undone in one Ctrl+Z, and told how many cells it replaced.
6. As a user of a pivot report, I want Ctrl+F to find a label or a value as shown.
7. As a user of a Sheet, I want to search the Sheet or the whole Workbook, and a match on another Sheet shown and selected.
8. As an application developer, I want Replace asked of me like Find, with `GridSource.From` doing it for me.
9. As an application developer answering Find myself, I want to pass a match function to get the marks, and none when I don't.

## Implementation Decisions

As ADR-0332 §1–§5. Every new gesture is an input in the Input Order (ADR-0240). The mark is a class and
a Visual Token; nothing per cell reaches JavaScript.

## Testing Decisions

Layer 1 for `GridSource.From`'s Replace and the reference match rule; layer 2 for the marks, the
request, refusals and undo; layer 3 for Ctrl+H, the marks while scrolling and Within, at machine speed on
both hosts and both Chromes. Test names carry ADR-0332.

## Out of Scope

Find All (Excel's list of every match); search by format, comments or columns.
