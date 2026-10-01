# 79: A prototype: Japanese typed onto a selected cell composes

Status: ready-for-agent

**What to build:** a prototype, not for merging, so that the user can decide (ADR-0010, the note of
2026-10-01). The fifteenth Windows run found that on a selected cell with no edit open the IME cannot
start: DOM focus is on the root, which is not editable, Chrome and Edge give it no input context, and
`kana` types Latin text. Excel composes from the first key. With F2 first, every IME reading held.

The idea to try: while a cell is selected, the keyboard is held by a text field of the grid's own, not
seen, inside the root. Keys it does not take reach the capture-phase listener on the root as today. A
composition that starts in it opens the edit in the selected cell, and the composing text and the
IME's state are carried into the Cell Editor (or the edit is drawn over that field), so the user sees
the composition in the cell, as in Excel.

**Blocked by:** None. Build on its own branch from the base; do not merge.

- [ ] A prototype on a branch of its own. It changes no ADR, CONTEXT.md or DoD row; what it would
      change is listed in its Comments as a proposal
- [ ] Measured, not argued: layers 1 and 2 in full, and layer 3 for the keyboard (KB-*), the
      clipboard (CP-*), the editor's focus (ED-26, ED-28, ADR-0021's notes), multiple instances
      (ADR-0018) and Find, on both hosts. Every failure is listed, with whether the prototype or the
      test is wrong
- [ ] What a real IME does with it is for a Windows run: write its procedure as a proposal, numbered
      from `docs/agents/numbering.md` only once the user reserves the run
- [ ] The Comments say which way the design could go (a hidden field carried into the Cell Editor;
      the Cell Editor itself kept open and unseen; a field that becomes the editor), what each costs
      against ADR-0010, ADR-0018 and ADR-0021, and what the prototype chose

## Comments
