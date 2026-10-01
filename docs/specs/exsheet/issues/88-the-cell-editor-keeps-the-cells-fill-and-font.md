# 88: The Cell Editor keeps the cell's Fill and Font

Status: ready-for-agent

**What to build:** an Excel fidelity gap ticket 48 found. In Excel, a yellow, bold, red cell stays yellow,
bold and red while it is edited. ExSheet's Cell Editor shows the Paper and the Ink. The core's editor box
takes no appearance.

**Blocked by:** None (can start immediately)

- [ ] **The core's Cell Editor takes the edited cell's appearance** (ADR-0050 item 15): its Fill as the
      ground and its Font as the text. Italic, underline and strikethrough follow if Excel shows them
      while editing. Check the record of the eleventh run, and if it says nothing, note that as a reading.
- [ ] **A Number Format's colour is not the editor's.** The editor shows the Entry, not the formatted
      Value.
- [ ] **ADR-0057's coloured References still read** over a Fill.
- [ ] **No row height changes** and no geometry moves. The editor's box stays where it is.
- [ ] **Tests:** layer 2 for the markup; layer-3 pixels in a spec. CI runs them.
