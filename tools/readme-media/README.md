# README media

The README's GIFs are recorded from the Docs Site's Showcases by `record.mjs`, never edited by hand
([ADR-0110](../../docs/adr/0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)).
When the UI changes, record them again.

Each scene in `scenes/` opens one Showcase, waits until it is ready, and plays its gestures: the
same rows every time, because the Showcases' data comes from a fixed seed. The recording draws a
cursor and a badge for each key pressed. Neither touches what the page does. A scene whose page
logs an error is not written.

## Run it

You need Node.js, ffmpeg, and the .NET SDK (or Nix).

```sh
# 1. Start the Docs Site
nix develop -c dotnet run --project samples/ExGrid.Docs --urls http://localhost:5310

# 2. Record (from this folder)
npm ci
node record.mjs --base http://localhost:5310/            # every scene, both Chromes, into out/
node record.mjs --only blotter --chrome mud              # one scene, one Chrome
node record.mjs --write                                  # also copy the GIFs into docs/readme/
```

Set `CHROMIUM_PATH` to use an installed Chromium instead of Playwright's own.

A recording is 1280×720, turned into a 960-pixel-wide GIF at 12 frames a second.
