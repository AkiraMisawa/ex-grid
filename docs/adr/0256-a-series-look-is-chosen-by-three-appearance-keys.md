# A series' look is chosen by three Appearance Keys: colour, line style and marker

*Decided with the user, 2026-10-19 (grilling Q2). The user added line styles, then markers, to the
colour key first proposed.*

On the exposure page each of the EPE, ENE and CVA panels holds Before, After and Diff. If After is
blue in one panel it must be blue in the next, and hiding Before must not turn After another colour.
The proof of concept assigned colours by order, and a hidden series kept its slot only by a special
case.

**Every series carries three Appearance Keys**, and each key has a table of its own:

| Key | Chooses | From |
|---|---|---|
| **Colour key** | a colour slot | the palette's slots, Visual Tokens |
| **Line-style key** | solid, dashed, dotted … | line-style Visual Tokens |
| **Marker key** | circle, square, diamond, triangle … | marker shapes |

- **The same key always takes the same look**, in every panel of a chart, and a key keeps its look
  when another series is hidden, filtered or added.
- **Defaults.** Panels split by Header Group: colour by leaf (Before / After / Diff), solid lines,
  the marker the series' style asks for. One axis for every group: colour by Header Group, line style
  by leaf. Series in rows: colour by Row Key. The Consumer may set any of the three keys, or a slot
  outright.
- **Two channels, so colour is never alone.** A chart readable only by colour fails readers who see
  colour differently and every black-and-white print (WCAG 1.4.1). The proof of concept's table
  legend reads directly as keys: its rows are colours, its columns line styles.
- **A step's ends keep the marker's shape**: filled where the function takes the value, an outline
  where it does not (ADR-0253). Shape and fill are separate, so the two never collide.
- **Ten colour slots by default; a theme may add more.** *(M2 grilling Q9, 2026-10-19.)* When a
  chart has more colour keys than slots — fourteen counterparties on one axis — colours repeat in
  order, and the chart writes a note under it that colours repeat (ADR-0257's notes). Legend and
  tooltip still name every series. Refusing the chart would block an ordinary request; generating
  colours by rotating the hue cannot promise contrast between neighbours or in a dark theme.
- **No colour, line pattern or marker size is a C# parameter** (ADR-0027/0028): the keys name slots,
  and the stylesheet's Visual Tokens say what a slot looks like.

*(2026-10-19, [ADR-0281](./0281-the-mudblazor-wrapper-supplies-the-panel-toolbar-menus-and-palette.md),
decided with the user: under the MudBlazor Wrapper, slots 1 to 3 take the theme's `Primary`, `Secondary`
and `Tertiary`, slots 4 to 10 keep the default palette, and `Error`, `Warning`, `Success` and `Info` are
never a series' colour.)*
