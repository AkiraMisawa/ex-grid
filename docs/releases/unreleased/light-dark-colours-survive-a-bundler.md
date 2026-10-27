Product: ExGrid
Kind: Fixed
ADR: 0018

When a bundler lowered `ex-grid.css`'s `light-dark()` for older browsers, as Vite does by default, the reference colours and the Formula Bar's ground were lost (the inherited text colour, no ground) on a page whose bundled CSS declared no `color-scheme`. They now paint light, as the browser does when nothing is declared. A page that sets its scheme outside the bundle keeps `light-dark()` with Vite's `build.cssTarget`, as the appearance page says.
