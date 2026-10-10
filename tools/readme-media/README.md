# README media

The README's GIFs are recorded from the Docs Site's Showcases by `record.mjs`, and the figures that
number a product's parts are drawn from pages of the Docs Site by `figures.mjs`. Neither is ever
edited by hand
([ADR-0110](../../docs/adr/0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)).
When the UI changes, record and draw them again.

Each scene in `scenes/` opens one Showcase, waits until it is ready, and plays its gestures: the
same rows every time, because the Showcases' data comes from a fixed seed. The recording draws a
cursor and a badge for each key pressed. Neither touches what the page does. A scene whose page
logs an error is not written.

## Figures

Each figure in `figures/` opens a page made for it under `samples/ExGrid.Docs/Figures`, waits until
it is ready, and numbers the parts it lists: a ring around each part, and its number beside it with
a line to the ring. A figure may first make the state it numbers with a user's gestures, as a scene
does: a Selection, a row marked, a Formula opened with F2. Only the frame's margin moves, to make
room for the numbers. A figure is refused when its page logs an error, when a part is not on the
page exactly once, when an element it shows whole would scroll, and when two numbers would stand
on each other's lines. The PNG is taken at twice the pixels.

The names the numbers stand for, each part's term in `CONTEXT.md` and a note on it, are never drawn
into the picture: they are written beside it as text, so they read at any width a page or the README
shows the picture at, and can be copied. `--write` writes them from the figures' definitions with
the PNGs: into the Docs Site's `Figures/DrawnFigures.g.cs`, which its `DocsFigure` component shows
under the picture, and into each README block between `<!-- figure NAME -->` and
`<!-- /figure NAME -->`. Neither is edited by hand. `--names` writes the names alone: a note
reworded needs no new picture, a part added or moved does.

| Figure | Page | Shown on |
|---|---|---|
| `grid-anatomy` | `/figure/grid-anatomy` | ExGrid's Overview, and the README's ExGrid section |
| `sheet-anatomy` | `/figure/sheet-anatomy` | ExSheet's Overview, and the README's ExSheet section |
| `pivot-anatomy` | `/figure/pivot-anatomy` | ExPivot's Overview, and the README's ExPivot section |

## Run it

You need Node.js, ffmpeg, and the .NET SDK (or Nix).

**Record the Release build, never `dotnet run`.** `dotnet run` serves a Debug build, whose
WebAssembly runs several times slower than the build GitHub Pages serves: the blotter scene took 65
seconds under it, against 12 in Release, and a GIF recorded that way shows a slower product than a
reader gets. Publish the site as `docs.yml` does and serve the files with `serve.mjs`, which serves
them as Pages does.

```sh
# 1. Publish the Docs Site as docs.yml does, and serve it (from this folder)
nix develop ../..#browser -c dotnet publish ../../samples/ExGrid.Docs/ExGrid.Docs.csproj -c Release -o site
node serve.mjs site/wwwroot 5310

# 2. Record (from this folder)
npm ci
node record.mjs --base http://localhost:5310/            # every scene, both Chromes, into out/
node record.mjs --only blotter --chrome mud              # one scene, one Chrome
node record.mjs --write                                  # also copy the GIFs into docs/readme/
node figures.mjs --base http://localhost:5310/           # every figure, into out/
node figures.mjs --write                                 # also the PNGs into the Docs Site, and the names
node figures.mjs --names                                 # the names alone, from the PNGs already there
```

Set `CHROMIUM_PATH` to use an installed Chromium instead of Playwright's own.

A recording is 1280×720, turned into a 960-pixel-wide GIF at 10 frames a second. A figure is as
large as its page's frame and the names around it.
