# 160: KeyTips, and Ctrl+F1 to hide and show the Sheet Toolbar

Status: done

**What to build:** the keyboard part of
[ADR-0100](../../../adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md): Excel's
KeyTips over the Sheet Toolbar, and Ctrl+F1.

**Blocked by:** 54

- [x] **Ctrl+F1** raises `ShowToolbarChanged` with the other value, only where the Consumer binds it.
      Unbound, the key is not claimed (SH-51).
- [x] **KeyTips start** when Alt (Option on macOS, read by `code`) is released alone, or with F10.
      This works only while the Sheet holds the keyboard and its toolbar is shown. While an edit is open the keys are claimed and start nothing
      (SH-52).
  - The existing capture-phase `keydown` claims Alt alone and F10.
  - A Blazor `keyup` handler on the root hears the release. It bubbles there from the Keyboard
    Field (ADR-0080).
  - No script is added (ADR-0021).
  - Alt with any other key stays a chord. Alt+↓ and Alt+Enter keep their meaning.
- [x] **Levels:**
  - one letter per Toolbar Row, H for the formatting row;
  - then the items' letters, which are Excel's Home tab letters;
  - Strikethrough's own letter is 4.
- [x] **A Consumer's letters** are declared on `ToolbarRow` and on its items, and never assigned.
      Two letters colliding at one level, ExSheet's own included, are refused when rendered, naming
      both.
- [x] **Escape** goes back one level, and from the top it ends. An unmatched key ends KeyTips doing
      nothing. A run item leaves the keyboard on the Sheet. The Chrome draws the letters, under both
      Chromes.
- [ ] **Readings for the next Windows run**, which reserves its number in `docs/agents/numbering.md`:
  - every Home tab KeyTip of the current Excel, and which letters are free;
  - whether `preventDefault` on Alt's `keydown` keeps Chrome's and Edge's menu from opening on
    release;
  - whether Ctrl+F1 and F10 reach the page.

  If Alt cannot be kept from the browser, KeyTips start from F10 alone. ADR-0100 records why.

## Comments

*(2026-10-02, built.)* KeyTips and Ctrl+F1 work as ADR-0100 decides. ADR-0100's "Found while
implementing the keys" records the details.

- **The core.** `Alt+Alt` can be declared (ADR-0050 item 14's note).
- **ExSheet.**
  - It declares `Alt+Alt` and `F10` while the toolbar is shown, and
    `Control+F1` while the toggle is bound.
  - It hears the Alt key's release from a `keyup` on its own element.
  - The toolbar takes the keyboard for the KeyTips.
- **Tests.**
  - Layer 2: `SheetToolbarKeyTipTests`, 10 tests.
  - Layer 3: `sheet-toolbar.spec.mjs`, both Chromes, run locally on Chromium under xvfb.
- **The Windows readings stay open.** That box is a Windows run's to tick; nothing here can.
- **Left as it is.** Alt released before the ↓ of Alt+↓ shows the KeyTips, which Escape takes
  away. The grid stops the keys it claims at keydown, so only the release of such a chord is seen.
