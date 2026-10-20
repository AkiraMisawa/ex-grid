# 02: The Number Format box reads `General` after keys typed at once

Status: done
Roadmap: ExSheet — Keys typed at once in More Number Formats… and the KeyTips leave the toolbar as keys typed slowly do.

**Seen:** `sheet-toolbar.spec.mjs`, under both Chromes, Server host, Chrome and Edge, shard 2/4, in both
runs:

- :423, "More Number Formats… opens Format Cells with the keyboard in it, and Escape typed straight
  after the choice closes it, as typed slowly": `#sheet-focus-number-format` expected `""`, received
  `"General"` (line 460).
- :854, Alt, H, N, then End and Enter typed at once: the same reading, expected `""`, received
  `"General"` (line 873).

**Why it is a defect:** each test compares keys typed at once with the same keys typed slowly, and the
outcome differs (the spine's sixth principle, ADR-0220).

- [x] Find which key's answer leaves `General` in the box.
- [x] Fix it in the product.
- [x] Both tests pass 20 runs each under the widened conditions.

**Found (2026-10-20):** the reading, not the product. Both tests read the box's text straight after
pressing C2, as what Escape must leave there. On the Server host the page hears C2's format a round
trip after the press, so read sooner the text was still the page's empty first one, and the box's
`General` at the end was right. Both tests now read it once the circuit is quiet.
