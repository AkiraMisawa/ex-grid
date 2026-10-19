# Past the plot's pixels, a series is thinned to its minimum and maximum per pixel

*Decided with the user, 2026-10-19. The user: yes, and the tooltip says the range (2026-10-19).*

10,959 daily dates on an 816-pixel plot is thirteen dates a pixel: the extra points add nothing a
reader can see and cost 61 MB a redraw.

- **When a series has more points than the plot has device pixels across**, each pixel column keeps
  the series' first, last, minimum and maximum point in it. The drawn shape is the same as the full
  series' at that width: every peak and trough stays.
- **The CSV and the files carry every point**, never the thinned ones.
- **The tooltip at a thinned pixel says the range**: its dates from–to, the minimum and maximum, and
  how many dates the pixel holds. It never shows one of them as if it were the only one.
- **Thinning is redone when the width or the date range changes**, from the full series.
- **A step is thinned by its levels**, so an open and a filled end are never drawn at a point the
  function does not reach.
