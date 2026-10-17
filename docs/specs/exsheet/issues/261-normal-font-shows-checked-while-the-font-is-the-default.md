# 261: Normal font shows checked while the Font is the default, under MudSheetChrome

Status: done
Roadmap: ExSheet — Normal font shows checked while the Font is the default, under every Chrome

**Found:** on 2026-10-17, in a sweep for ticket 260's pattern (a Wrapper's control whose own state
and the parameter it is given can part).

**Seen:** under `MudSheetChrome`, Format Cells' Font tab. Normal font is checked while every part of the
Font is the default (ADR-0071, 2026-10-02). A click unchecks it. The handler sets nothing on an uncheck,
so the Draft is still the default Font and the parameter comes back `true` again, unchanged. MudCheckBox
keeps the state it took from the click when its parameter has not changed, so the box shows a default
Font as not Normal. The built-in Chrome prevents the uncheck (`FormatCellsPanel.razor`,
`@onclick:preventDefault`), so it did not have the defect.

**Why it is a defect:** the dialog shows something that is not so (the spine's first principle), and
the two Chromes disagree (the fourth).

**Fixed:** the box is read-only while it is checked. MudBlazor then prevents the click's default, as the
built-in Chrome does.

- [x] Fix it in the product.
- [x] A component test: the box is read-only while the Font is the default, and not once a part of the
      Font is changed (`Normal_font_is_read_only_while_the_font_is_the_default`; red before the fix).
- [ ] A browser exercises the click (not done when it was fixed: the container had no Chrome or Edge).
