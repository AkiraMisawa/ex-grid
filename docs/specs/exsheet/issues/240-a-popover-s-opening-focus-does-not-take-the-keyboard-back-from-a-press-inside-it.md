# 240: A popover's opening focus does not take the keyboard back from a press made inside it

Status: done
Roadmap: ExGrid, ExSheet — A press inside a popover that has just opened keeps the keyboard where it was pressed

**Found:** by CI on #140, a pull request that changes records only (run 39031949301, "Layer 3 against
the Server host on chrome, shard 2/4", 2026-10-17). The same code passed at `885bfb98` on `main`.

**Seen:** `format-cells.spec.mjs`, "SH-45/ADR-0044: keys typed while Enter on OK closes Format Cells
are the Sheet's, in order". The test opens Format Cells from `#sheet-format-cells`, checks Percentage,
focuses OK and types Enter, ↓, ↓ at once. Format Cells never closed. The trace shows it typed at
machine speed: the popover was visible at +0 ms, the radio pressed at +33 ms, OK focused at +45 ms,
Enter at +52 ms. The page as it failed has the keyboard on the Number tab, not on OK: the contents'
opening focus (`FormatCellsPanel.OnAfterRenderAsync`, `FocusAsync` on the first tab) landed after the
press and the focus, and Enter was typed on a tab, where it does nothing.

**Today:** a popover's opening focus is taken a round trip after the gesture that opened it
(ADR-0039). ADR-0204 makes it ask "is DOM focus inside this root, or nowhere?" first, so that a grid or
a control the user has pressed in the meantime keeps the keyboard. A control inside the popover itself
is inside the root, so the question lets the focus through and the keyboard is taken back from where
the user pressed. A Consumer's popover (ADR-0050 item 16) is not handed the core's focus function at
all: Format Cells calls `FocusAsync` itself.

**Why it is a defect:** the outcome depends on how fast the user presses after the popover appears
(the spine's sixth principle, ADR-0220 §2). Pressed a moment later, Percentage then OK then Enter
applies the format; pressed at once, the keyboard is pulled to the first tab and Enter does nothing.

**Decided** with the user on 2026-10-17 (ADR-0204's note of that day): yes to both. Was asked: an opening focus, at its turn, is refused when DOM focus is already
inside the popover it is for, as ADR-0204 refuses one when the keyboard has left the grid; and whether
a Consumer's popover is handed the core's focus function for it, as a Wrapper's popover is.

- [x] Decide, and record it in ADR-0204.
- [x] Fix it in the product, under every Chrome.
- [ ] A layer-3 test presses inside a popover before its opening focus lands, on the Server host with
      latency injected, and the keyboard stays where it was pressed; 20 runs on each host.
- [ ] `format-cells.spec.mjs`'s SH-45 test passes 20 runs on each host.
