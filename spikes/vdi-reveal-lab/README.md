# VDI reveal lab (branch `lab/vdi-reveal`, never merged)

Under the company's Citrix VDI, Ctrl+Down leaves the grid white until the next scroll; at home it
does not. The notes from there (2026-10-07) say the scroller never moved: Alt+Tab does not bring the
rows back, a small scroll paints the rows near the top while the Focus is on the last row, and
shrinking the window (which reveals again) moves it. So a reveal's scroll write is lost when a key
asks for it, not merely painted late.

This branch is cut from `v0.1.0-beta.2`, the published version, so mode A is exactly what was seen
there. It adds five ways of painting a reveal, chosen per page by `?reveal=`, and a trace,
`?trace=1`, that records each scroll write the grid asks for, when it is made, where the scroller
stands right after it and later, and every scroll event, in a panel on the page. Without either
query the page is the published one. Nothing here is a decision: whatever the trace shows goes into
an ADR and a proper change on `main`.

| Mode | How the jump is painted |
|---|---|
| A | As shipped: the new rows and the scroll offset in the same frame |
| B | The new rows in one frame, the offset written in the next |
| C | As A, then the offset nudged by 1px and back over the next two frames |
| D | As A, then the same nudge 300 ms later |
| E | As A, then the scroller repainted (opacity 0.999) for one frame, no scroll |

What changed, all marked `LAB ONLY`:

- `src/ExGrid/Assets/ex-grid.js` (and its minified copy, from `tools/assets`): the modes, read from
  `location.search` at each reveal, and the trace panel. The trace reads no layout before a write.
- `samples/ExGrid.Docs/Showcases/Blotter.razor`: the mode at the end of the status line.
- `samples/ExGrid.Docs/Pages/Lab.razor`: `/lab`, the links and the steps to take in each mode.

## Build and check locally

```sh
nix develop -c dotnet publish samples/ExGrid.Docs/ExGrid.Docs.csproj -c Release -o <publish> -p:DocsVersion=lab-vdi-reveal
spikes/vdi-reveal-lab/prepare-site.sh <publish> ex-grid-lab <site>     # what docs.yml does for Pages
node spikes/vdi-reveal-lab/pages-server.mjs <site> ex-grid-lab 8823   # serves it as Pages would, 404.html included
node spikes/vdi-reveal-lab/lab-verify.mjs http://127.0.0.1:8823/ex-grid-lab/   # frame-by-frame record per mode
node spikes/vdi-reveal-lab/lab-keys.mjs http://127.0.0.1:8823/ex-grid-lab/     # Ctrl+Up and PageDown per mode
```

The two probes load `playwright-core` from the main checkout's `tests/ExGrid.Browser/node_modules`
and drive the installed Chrome headed. Stop the server with `fuser -k 8823/tcp`.

Checked on 2026-10-07 (Chrome 152, Linux): every mode lands on row 25,000 with the Focus on screen
after Ctrl+Down, Ctrl+Up and PageDown, with no console error; B shows one frame with no rows in
view before the offset is written, by design.

## Publish (not done yet; needs the owner's go-ahead)

Either onto the Docs Site itself, for the length of the test. The Pages source stays "GitHub
Actions"; only the `github-pages` environment, which deploys from `main` and `v*` tags alone, is
told to accept this branch for a while:

```sh
git push origin lab/vdi-reveal                       # ci.yml does not run for a branch push
# Settings > Environments > github-pages > Deployment branches and tags: add lab/vdi-reveal
gh workflow run docs.yml --ref lab/vdi-reveal        # the site becomes this build
# ... after the test:
gh workflow run docs.yml --ref v0.1.0-beta.2         # the published site again
# remove the environment rule; git push origin --delete lab/vdi-reveal
```

Never tag this branch `v*`: `release.yml` would publish it to nuget.org.

Or a separate public repository, so the Docs Site and the packages are untouched:

```sh
gh repo create AkiraMisawa/ex-grid-lab --public --description "Throwaway: ExGrid VDI reveal lab"
cd <site> && find . \( -name '*.br' -o -name '*.gz' \) -delete    # Pages does not serve them
git init -b main && git add -A && git commit -m "VDI reveal lab build"
git remote add origin https://github.com/AkiraMisawa/ex-grid-lab.git && git push -u origin main
gh api -X POST repos/AkiraMisawa/ex-grid-lab/pages -f 'source[branch]=main' -f 'source[path]=/'
```

Then open `https://akiramisawa.github.io/ex-grid-lab/lab`. Delete the repository when done. (On the Docs Site the page is `https://akiramisawa.github.io/ex-grid/lab`, and the build must be prepared with `ex-grid` as the repository name.)
