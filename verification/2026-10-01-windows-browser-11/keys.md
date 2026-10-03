# Windows, eleventh run, Part B: the browsers' own keys

[`docs/specs/exsheet/verify-on-windows-11.md`](../../docs/specs/exsheet/verify-on-windows-11.md),
Part B, cases 27 to 30. These cases ask whether a page receives Excel's formatting keys before Chrome
and Edge take them. That is ADR-0063, "How a user applies it › Keys", and the last of its readings.
Part A (Excel) is in
[`../2026-10-01-windows-excel-11/cell-format.md`](../2026-10-01-windows-excel-11/cell-format.md),
which also summarises this part.

- **Verified commit:** `76d3866c3233fc76b79956f157c2e3f7ee18a14a`, the tip of
  `claude/exsheet-cell-format`, on the branch `claude/exsheet-windows-verify-11`.
- **When:** 2026-10-01. Chrome ran from 02:53:42 to 02:54:11 local time, and Edge from 02:54:20 to
  02:54:49.
- **Nothing was decided.**

## Result

**Both browsers answered every key the same way, and every reading held.**

- **Case 27.** The page received Ctrl+1 to Ctrl+5, and no tab was selected.
- **Case 28.** The page received Ctrl+B, Ctrl+I and Ctrl+U, and no view-source tab opened.
- **Case 29.** The page received every Ctrl+Shift character on both layouts, as listed below.
- **Case 30.** Ctrl+Tab and Ctrl+PageDown each selected the next tab, and the page received neither.

In every row, the div had DOM focus before the key, and keys.html was the selected tab. The browser
opened no tab, no window and no bubble. The page logged no console message.

## Files

| File | Contents |
|---|---|
| `keys.html` | The page the procedure asks for. One focusable `div` has a capture-phase `keydown` listener. For every key with Ctrl, it calls `preventDefault()` and lists `key`, `code`, `ctrlKey`, `shiftKey` and `altKey` on the page (and in `window.__keys`) |
| `other.html` | The six other tabs (`?n=1`, `2`, `4`–`7`), each titled `tab-<n>-verify-11` |
| `keys-probe.mjs` | The probe, which opens the tabs and reads the page and the browser |
| `browser-input.ps1` | The real input and the window reads |
| `records/keys-<browser>.json` | For each key: what was sent, what the page logged, the tab strip before and after, the browser's windows added, the window's title, what was closed afterwards, and the picture's name |
| `shots/<browser>-<case>-<layout>-<key>.png` | The browser's windows 500 ms after each key, each drawn by itself (`PrintWindow`), so the tab strip and the page both show |

## Method

- **The page** was served from WSL with `python3 -m http.server 8811`.
- **The browsers** were Chrome 153.0.8010.54 and Edge 154.0.4258.37, the installed channels.
  - Each was started headed by Playwright 1.62.1 (`launchPersistentContext`, Node 24.14.1 on
    Windows), in a new, empty profile. Playwright's own switches applied, such as `--enable-automation`.
  - Each window had seven tabs, in this order: `other.html?n=1`, `?n=2`, **`keys.html`**, `?n=4` to
    `?n=7`. The tab strip read through UI Automation confirmed the order, with keys.html third and
    selected.
- **Every key and click was real OS input** through `SendInput`: a virtual-key and a scan code for
  each key, the scan code taken from the window's layout.
  - The div was clicked with the real mouse, once at the start. It kept DOM focus for every later key.
  - Before each key, the probe checked that keys.html was the selected tab and that the div was
    `document.activeElement`.
- **Keyboard layouts.** The browser's window was put on English (UK), HKL `0x08090809`, with
  `WM_INPUTLANGCHANGEREQUEST`. For case 29's second half it went to the Japanese layout, HKL
  `0x04110411`, with the IME closed (`open=0`, conversion `0x19`).
  - **This machine's Japanese layout uses the English 101-key arrangement**
    (`LayerDriver JPN = kbd101.dll`).
  - So on it, a character is on the key the US layout has it on. A Japanese 106/109-key arrangement
    would need that setting changed and a restart, and that was not done.
- **"Typed as characters"** means each key was sent as the key that types the character on the
  window's layout, with the Shift that character needs, and with Ctrl (`VkKeyScanEx`).
  - On UK, `#` needs no Shift. So it was sent as Ctrl with `OEM_7` alone.
  - The procedure asks for "Ctrl+Shift with `#`". The record keeps what was sent.
- **After each key** the probe waited 500 ms, then recorded:
  - the page's new log entries;
  - the selected tab, through the tab strip's UI Automation `TabItem` selection;
  - the tabs and windows that had appeared;
  - the window's title;
  - a picture.
- **Closing what the browser opened.** No key opened anything, so nothing had to be closed. After the
  two keys that switched tabs (case 30), keys.html was selected again with Playwright's
  `bringToFront()`.

## Cases 27, 28 and 30

The page's log also lists the modifiers' own `keydown` events (`Control`, `Shift`), each with
`ctrlKey: true`. They are left out here. Edge's answers were the same as Chrome's in every row.

| # | Key sent | Page received (Chrome, Edge) | Browser acted | Reading |
|---|---|---|---|---|
| 27 | Ctrl + VK `1` | `key=1 code=Digit1` | nothing; keys.html still selected | the page receives each; no tab switch |
| 27 | Ctrl + VK `2` | `key=2 code=Digit2` | nothing | the same |
| 27 | Ctrl + VK `3` | `key=3 code=Digit3` | nothing | the same |
| 27 | Ctrl + VK `4` | `key=4 code=Digit4` | nothing | the same |
| 27 | Ctrl + VK `5` | `key=5 code=Digit5` | nothing | the same |
| 28 | Ctrl + VK `B` | `key=b code=KeyB` | nothing | the page receives each; Ctrl+U opens no source |
| 28 | Ctrl + VK `I` | `key=i code=KeyI` | nothing | the same |
| 28 | Ctrl + VK `U` | `key=u code=KeyU` | **no view-source tab**; nothing else | the same |
| 30 | Ctrl + Tab | nothing (only `Control`) | **tab-4-verify-11 selected** | the browser switches tabs; the page receives neither |
| 30 | Ctrl + PageDown | nothing (only `Control`) | **tab-4-verify-11 selected** | the same |

## Case 29: Ctrl+Shift with each character

Every key was received, by Chrome and Edge alike, with `ctrlKey=true` and `altKey=false`. The browser
did nothing. The record lists each key as `vkKeyScan` and what was sent; "Shift" below is the
`shiftKey` the page logged.

| Character | UK: sent | UK: `key` / `code` / Shift | Japanese (101): sent | Japanese: `key` / `code` / Shift |
|---|---|---|---|---|
| `~` | Ctrl+Shift+`OEM_7` (scan `0x2b`) | `~` / `Backslash` / true | Ctrl+Shift+`OEM_3` (scan `0x29`) | `~` / `Backquote` / true |
| `!` | Ctrl+Shift+`1` | `!` / `Digit1` / true | Ctrl+Shift+`1` | `!` / `Digit1` / true |
| `@` | Ctrl+Shift+`OEM_3` (scan `0x28`) | `@` / `Quote` / true | Ctrl+Shift+`2` | `@` / `Digit2` / true |
| `#` | Ctrl+`OEM_7` (no Shift) | `#` / `Backslash` / **false** | Ctrl+Shift+`3` | `#` / `Digit3` / true |
| `$` | Ctrl+Shift+`4` | `$` / `Digit4` / true | Ctrl+Shift+`4` | `$` / `Digit4` / true |
| `%` | Ctrl+Shift+`5` | `%` / `Digit5` / true | Ctrl+Shift+`5` | `%` / `Digit5` / true |
| `^` | Ctrl+Shift+`6` | `^` / `Digit6` / true | Ctrl+Shift+`6` | `^` / `Digit6` / true |
| `&` | Ctrl+Shift+`7` | `&` / `Digit7` / true | Ctrl+Shift+`7` | `&` / `Digit7` / true |
| `_` | Ctrl+Shift+`OEM_MINUS` | `_` / `Minus` / true | Ctrl+Shift+`OEM_MINUS` | `_` / `Minus` / true |

With Ctrl held, both browsers reported `key` as the character the layout types: `~`, `@`, `#` and
the rest, not `` ` ``, `'` or `2`. They reported `code` as the physical key's US name. On UK, the
`#~` key is `Backslash` and the `'@` key is `Quote`.

## Afterwards

- The two browsers Playwright started closed with their profiles, and the profile folders were
  deleted.
- The user's own Chrome, which was running throughout, was not touched.
- The static server was stopped.
- The browser windows' layout had been put back to English (UK) before case 30.
