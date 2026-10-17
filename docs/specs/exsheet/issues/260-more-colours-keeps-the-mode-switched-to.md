# 260: More Colours keeps the mode the user switched to

Status: done
Roadmap: ExSheet — More Colours keeps the mode switched to, however soon after its opening the switch is pressed

**Found:** by CI on #146, a pull request that changes contributor guidance only (run 39063380869,
"Layer 3 against the Server host on chrome, shard 2/4", 2026-10-17).

**Seen:** `format-cells-mud.spec.mjs`, "ADR-0039/ADR-0071: More Colours retains RGB and HSL controls
and reopens in HEX". The trace shows it driven at machine speed: More Colours clicked, HEX visible
15 ms later, the picker's switch clicked 47 ms after that. `R` never appeared within 60 s, and the
page as it failed shows the picker open in HEX.

**Cause:** Format Cells passed `ColorPickerMode = HEX` to the picker as a parameter. MudBlazor's
switch sets the same property, and every render of the dialog sets the parameter again. When the
dialog rendered after the switch, the mode went back to HEX. Which render that was on CI is not in the
trace; any will do, and on a circuit the opening's own work arrives a round trip after it. Pressed a moment later, the switch worked.

**Why it is a defect:** the outcome depends on how fast the user presses after the picker appears
(the spine's sixth principle, ADR-0220 §2).

**Fixed:** HEX is set on every opening (`MudMoreColours.OpenFromButtonAsync`, as before) and at
construction, and never as a parameter, so no later render of the dialog sets it.

- [x] Fix it in the product.
- [x] A component test switches the mode and renders the dialog again, and the mode stays
      (`More_colours_keeps_the_mode_switched_to_when_the_dialog_renders_again`; red before the fix).
- [ ] The layer-3 test above passes in CI on both hosts and in both browsers.
