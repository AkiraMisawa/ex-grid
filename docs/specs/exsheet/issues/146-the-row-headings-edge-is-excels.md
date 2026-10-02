# 146: The Row Headings' edge is Excel's

Status: done

**What to build:** on a Sheet, the Headings' rule is `#ABABAB` over white, as Excel's (Part C): the
heading rule's grey at the alpha that gives it, so it follows the page's scheme.

**Blocked by:** None (can start immediately)

- [x] **`#ABABAB` at the Row Headings' edge** on a light page; a Wrapper's own token still wins.

## Comments

2026-10-02, claude/exsheet-part-c. `ex-sheet.css` changes only the core's default, so a Theme's or a
Wrapper's heading or header rule still wins. Layer 3: `part-c.spec.mjs`.
