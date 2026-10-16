# The eighteenth Windows run: the Sheet Toolbar's keys

Questions gathered from `claude/run17cf-toolbar-mud` (the seventeenth run's Part D, the Windows readings of
2026-10-12, R2 (a) and (b), and the user's decisions of 2026-10-12 and 2026-10-13 on keys held while the
keyboard is in flight) for the next Windows run, to be folded into its spec when it is written. Each needs
Windows: Excel, the browser's own menu, or a real IME.

1. **Excel: Escape after Alt, H, F.** ExSheet treats letters that narrow a level as a level of their own,
   as Excel's Format menu behind O was read to be (R2 (a)): Escape shows that level's every letter again.
   That Excel's F of FC is such a level is by generalisation; no run read Alt, H, F, Escape. In Excel 16,
   Alt, H, F, then one Escape: are the Home tab's letters shown again (every one, F among them), or the
   tabs', or none? And a second Escape?
2. **The browser's menu, with the Alt key's release now held by the grid** (SH-52's by-hand part; ADR-0100,
   ADR-0021's seventh entry as widened 2026-10-13). The grid's key gate now hears Alt's `keyup` on the root:
   it holds the release while it holds keys, hands it on in its turn, and keeps a chord's release (or one
   whose press it did not hear) from the Sheet. On `/sheet` with the toolbar shown, in Chrome and in Edge
   on Windows, on the Server host with the latency proxy at 80 ms and on WebAssembly: does the browser's
   own menu (the ⋯ button taking focus, or a menu opening) stay closed, and the KeyTips show as they do
   typed slowly, for each of
   - Alt released alone (the case run 17 read);
   - Enter on an open edit, then Alt, H, 1, typed at once;
   - Alt, Alt, H, 1 typed at once;
   - Alt+↓ with the Alt released before the ↓ (now no KeyTips: the release is a chord's)?
3. **A real IME and the Alt key alone** (ED-30 shares the root's `keyup` listener with the Alt hold). With
   the Microsoft IME (Japanese) on, on `/sheet` with the toolbar shown, in Chrome and Edge: does the IME's
   own toggle (Alt+\` on a US keyboard, or 半角/全角) start the KeyTips? The gate now tells an Alt "alone"
   by the keydowns it hears between the press and the release, and a key the IME takes as a hotkey may
   reach no keydown at all. And does a composition typed straight after Alt, H, 1 (the KeyTips' command
   leaving the keyboard on the Sheet) start in the Focus cell, with nothing of it lost or doubled?
