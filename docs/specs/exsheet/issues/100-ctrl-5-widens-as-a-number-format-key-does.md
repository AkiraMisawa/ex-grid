# 100: Ctrl+5 widens as a Number Format key does

Status: ready-for-agent

**What to build:** the fourteenth Windows run's case 7. With A1 showing `########` at the standard
width, Ctrl+5 (strikethrough) widened column A to fit, as a Number Format key does. Ctrl+B and the Fill
did not, in any of three orders. ADR-0071, "What the fourteenth Windows run settled", Widening.

**Blocked by:** None (can start immediately)

- [ ] **Ctrl+5 widens a column exactly as a Number Format key does** (ticket 58's rule): at the
      standard width, or left by an entry or a key; never a column the user sized.
- [ ] **Ctrl+B, Ctrl+I, Ctrl+U, a Fill, a Border and Format Cells' Font tab still never widen.** The run
      asked only Ctrl+B and the Fill; the rest stay as Ctrl+B is (ADR-0071).
- [ ] **Removing strikethrough with Ctrl+5 widens the same way** if the text no longer fits. *(A
      reading: the run pressed it only to set.)*
- [ ] **One undo step** takes back the strikethrough and the width together.
- [ ] **Layer 1/2.**

## Comments
