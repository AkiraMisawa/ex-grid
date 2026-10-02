# 120: A second composition keeps the Keyboard Field until it ends

Status: done — but for the last box, which a later Windows run answers

**What to build:** ADR-0080, "DOM focus never moves while a composition lasts", and DoD ED-30, which the
sixteenth Windows run found broken on WebAssembly (`verification/2026-10-02-windows-16/report.md`, k6,
k6x, k6y). With the Microsoft Japanese IME on D10 of `/sheet`: `kana`, Space, then `kanji`. The IME
ended the first composition at the `k` and began a second one in the Keyboard Field. 22–24 ms later
(44–45 ms under the MudBlazor Chrome) DOM focus moved to the Cell Editor that the first composition's
text had opened, while the second was composing. The IME carried `ｋ` there without its romaji state, so
D10 ended `かな暗示` (or `かなん時`, `ka` both lost). Excel and the Server host gave `かな感じ`: there the
second composition stayed in the field until it ended, as ADR-0080 intends.

**Blocked by:** None.

- [x] The cause is named from the run's records (the event order and the times) and the code path that
      moved DOM focus
- [x] While the Keyboard Field composes, nothing moves DOM focus out of it, by any path, for the built-in
      editor and a Chrome's alike; the editor takes the keyboard when the composition ends, and the
      second composition's text is typed into the edit at its caret, after the first (ADR-0080)
- [x] No listener is added and no layout is read beyond ADR-0021's seventh entry
- [x] Layer 2 where C# is involved; Layer 3 in `key-field.spec.mjs` composing through the DevTools
      protocol: a composition committed, a second begun at once before the editor has the keyboard,
      then completed. DOM focus stays in the field until it ends, and D10 holds both texts in order.
      Under both Chromes, on both hosts
- [ ] A real IME in a later Windows run: k6, k6x and k6y read `かな感じ` on WebAssembly

## Comments

2026-10-02, built on `agent/ps-120` (33e322b).

- **The cause.** All twelve WebAssembly records of k6, k6x and k6y read in the same order:
  1. The field's `compositionend` `かな`.
  2. DOM focus leaves the field for the Cell Editor 22–24 ms later on `/sheet`, and 44–47 ms later
     under the MudBlazor Chrome.
  3. The second `compositionstart` comes 2–3 ms after that, in the editor. No start reached the field
     in between.

  An example is sheet-wasm-chrome k6x: the end at 60812, the focus move at 60834, the start at 60837.
  All six Server records have the second start in the field in the same millisecond as the end. So
  the IME sends its end and its next start together, and Chrome reports the start only after the
  end's listeners, and everything they ran, have returned.

  The path was this. `onKeyFieldCompositionEnd` cleared the composing flag and carried the text. The
  drain then called `OnKeyFieldTextAsync`. On WebAssembly the edit opened and rendered within that
  same run, and the editor (the built-in one's after-render, or Mud's `takeFocus`) asked `focusEditor`
  for the keyboard. The flag was false by then, so the field gave up the keyboard. No other path moves
  focus: both Chromes reach the editor through `focusEditor` only.
- **A second instance, not seen by the run.** `carryKeyFieldText` granted a request already waiting at
  a composition's end, synchronously, on either host. So a key that ended a second composition and
  began a third would move the keyboard between them.
- **The fix**, in `ex-grid.js` only:
  - A composition's end starts a zero-delay timer, and `focusEditor` waits while the field composes
    or that timer is pending.
  - The timer grants the waiting request unless the field is composing again. In that case the
    request waits for that composition's end.
  - `dispose` clears the timer. No listener is added and no layout is read; the hop is the one
    `holdBehindPress` already makes.
  - It relies on Chrome handling the IME's waiting input before a zero-delay timer. That was reasoned
    from Chromium's scheduler, not measured; a real IME is the last box.
- **Tests.**
  - Layer 2, `ShippedStylesheetTests`: the ADR-0080 test that had pinned the synchronous grant
    follows the refinement. A new ED-30 test pins the end starting the timer before anything else,
    nothing granted at the end, one grant site and one timer, and the timer cleared on dispose. Both
    fail on the old module.
  - Layer 3, `key-field.spec.mjs`: k6 and a third clause, through `Input.insertText` and
    `Input.imeSetComposition` sent together, under both Chromes and on both hosts.
  - Layers 1 and 2: 829 + 2135 + 92 + 1237 (1 skipped, as before) + 395.

