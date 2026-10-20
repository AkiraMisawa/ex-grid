# A surface shows its colour bar, and its colours may diverge from a centre

*Decided while the user was away, 2026-10-20, under their standing instruction to pick a reasonable
default, record it in an ADR and carry on; the user asked for a diverging colour bar for the first
version ("決めずに残ってる3つともやろうか"). It completes the colouring of
[ADR-0274](0274-a-3d-surface-takes-a-grid-and-is-turned-zoomed-and-sliced.md); the user may revisit it.*

Until now a surface's faces were coloured in nine bands from its lowest value to its highest, and
nothing on the page said which colour was which value. A colour the reader cannot read back is a
value shown plausibly and unreadably at once. And a surface of P&L, of a Greek, of a basis or of a
ratio has a value that means "nothing": 0, or 1. Coloured from low to high, a book losing 1 beside a
scenario gaining 10 paints the loss in the deepest band, as alarming as a loss of 10 would be. That
is spine rule 1's quiet error: the picture is plausible and says the wrong thing.

## Decisions

- **Every surface carries its colour bar.** Beside the plot, at its right, the nine bands from low to
  high with their values written at round numbers along it. It is part of the drawing, so it is in
  the SVG and the PNG (ADR-0282), and it does not turn with the surface. .NET decides its run and its
  ticks; the 3D entry draws them (ADR-0255).
- **`SurfaceDefinition.Centre` makes the colours diverge.** It is the data's term — "the value that
  means nothing" — so it is on the Chart Definition's side, not the component's (ADR-0251). Values
  below it take one hue and values above another.
- **Alike on both sides.** The bands run from `Centre − d` to `Centre + d`, where `d` is the farther
  of the lowest and the highest value from the centre. A value as far below the centre as another is
  above it takes a band as deep. The middle band holds the centre and is neutral. *Rejected:* each
  side stretched to its own extreme (the usual "two-slope" norm). It makes the −1 of the example as
  deep as the +10, which is the error the centre exists to remove.
- **The bar of a diverging surface runs over that symmetric span** and always labels the centre, so
  the reader sees where "nothing" lies even when no value reaches one end.
- **Its colours are Visual Tokens**, `--ex-chart-diverge-0` to `-8` (ADR-0027): by default red below
  the centre, a near-white neutral, and blue above (ColorBrewer's RdBu). Red below keeps finance's
  "red is a loss". Above it is blue, not the green TradingView and Bloomberg use for a rise
  (from memory, not checked for their heat maps), because red against green is the pair the most
  common colour blindness cannot tell apart, and here the colour carries the sign. A Consumer or a
  Theme that wants green sets the tokens. The surface's own nine colours (`--ex-chart-heat-*`) are
  unchanged.
- **On a logarithmic vertical scale the colours diverge from the centre's logarithm** (ADR-0312), so a
  ratio of 4 and a ratio of ¼ take bands as deep about a centre of 1. A centre at or below zero cannot
  lie on that scale, so the scale stays proportional, and a Note says so.
- **The summary says it:** "Coloured by distance from 0, below and above" (ADR-0275).

## Rejected

- **A colour bar drawn by Blazor beside the box.** It would be missing from the files, which are the
  drawing (ADR-0282), and a saved surface without its key is unreadable.
- **A Wrapper or component parameter for the centre.** The same data would read differently on two
  pages. The centre is a fact about the values.
