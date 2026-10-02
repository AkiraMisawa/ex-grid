# 120: A second composition keeps the Keyboard Field until it ends

Status: ready-for-agent

**What to build:** ADR-0080, "DOM focus never moves while a composition lasts", and DoD ED-30, which the
sixteenth Windows run found broken on WebAssembly (`verification/2026-10-02-windows-16/report.md`, k6,
k6x, k6y). With the Microsoft Japanese IME on D10 of `/sheet`: `kana`, Space, then `kanji`. The IME
ended the first composition at the `k` and began a second one in the Keyboard Field. 22–24 ms later
(44–45 ms under the MudBlazor Chrome) DOM focus moved to the Cell Editor that the first composition's
text had opened, while the second was composing. The IME carried `ｋ` there without its romaji state, so
D10 ended `かな暗示` (or `かなん時`, `ka` both lost). Excel and the Server host gave `かな感じ`: there the
second composition stayed in the field until it ended, as ADR-0080 intends.

**Blocked by:** None.

- [ ] The cause is named from the run's records (the event order and the times) and the code path that
      moved DOM focus
- [ ] While the Keyboard Field composes, nothing moves DOM focus out of it, by any path, for the built-in
      editor and a Chrome's alike; the editor takes the keyboard when the composition ends, and the
      second composition's text is typed into the edit at its caret, after the first (ADR-0080)
- [ ] No listener is added and no layout is read beyond ADR-0021's seventh entry
- [ ] Layer 2 where C# is involved; Layer 3 in `key-field.spec.mjs` composing through the DevTools
      protocol: a composition committed, a second begun at once before the editor has the keyboard,
      then completed. DOM focus stays in the field until it ends, and D10 holds both texts in order.
      Under both Chromes, on both hosts
- [ ] A real IME in a later Windows run: k6, k6x and k6y read `かな感じ` on WebAssembly
