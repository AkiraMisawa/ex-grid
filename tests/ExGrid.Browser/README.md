# Layer 3 — the browser suite

Playwright against the installed Chrome and Edge (ADR-0026), driving the DemoHost it
starts itself:

```sh
nix develop .#browser -c npx playwright test          # both projects: chrome and msedge
npx playwright test --project=chrome                  # one browser, where the other is absent
```

Headed by default, deliberately: headless macOS keeps overlay scrollbars whatever the
CSS asks, and the scrollbar suite would pass while proving nothing. `EXGRID_HEADLESS=1`
exists for machines without a display — safe on Windows and Linux, where native
scrollbars occupy layout anyway. **Run this suite on Windows or Linux at least once per
verification**: VZ-10's real clause is about platforms whose scrollbars take space.

A machine without Edge fails the `msedge` project by name — the honest outcome
(ADR-0017 requires both browsers; passing on one does not satisfy it).

## Installing the browsers

The flake ships none on purpose, and `flake.nix` says why: `channel: 'chrome'` means the
real Google Chrome the machine has, found by its own well-known path — on Linux
`/opt/google/chrome/chrome`, which is where Playwright reports it missing — and a nix
store build is not that. Both browsers are a machine prerequisite, installed once,
outside nix.

On Ubuntu-family Linux, including WSL2 (these need root, so run them yourself):

```sh
# Google Chrome
curl -fsSL https://dl.google.com/linux/linux_signing_key.pub \
  | sudo gpg --dearmor -o /usr/share/keyrings/google-chrome.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/google-chrome.gpg] https://dl.google.com/linux/chrome/deb/ stable main" \
  | sudo tee /etc/apt/sources.list.d/google-chrome.list
sudo apt update && sudo apt install -y google-chrome-stable

# Microsoft Edge
curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
  | sudo gpg --dearmor -o /usr/share/keyrings/microsoft-edge.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-edge.gpg] https://packages.microsoft.com/repos/edge stable main" \
  | sudo tee /etc/apt/sources.list.d/microsoft-edge.list
sudo apt update && sudo apt install -y microsoft-edge-stable
```

Under WSL2 the headed default should stand as it is: WSLg supplies the display (`/mnt/wslg`,
with the X socket at `/tmp/.X11-unix/X0`), and WSL2 is one of the platforms whose scrollbars
occupy layout, so `EXGRID_HEADLESS=1` would disarm the suite rather than help it. If a headed
launch reports that it cannot open a display, check that `DISPLAY` is set to `:0` — a
non-interactive shell does not always inherit it.

## What it asserts

- `scrollbar.spec.mjs` — the Scrollbar Gutter: the Focus is never behind a bar, at
  DPR 1 / 1.25 / 2, with the platform's bars and with forced classic bars.
- `features.spec.mjs` — the interaction surface on `/features`, with real keys and the
  real clipboard: the editor's two states (ED-2/3/4), both clipboard formats and the
  refusals (CP-1/3/4/5/6/10/14, PST-1), the keys the grid must not take (KB-15), one
  tab stop (A11Y-4, KB-12), instance independence (DOM-4), header-click sorting
  (SR-1), popovers (UX-11). And on `/cells`, entering a cell by key (ADR-0037): Space
  into a cell with several actions, the arrows choosing and Space firing once, Enter
  never firing (KB-20/21/22); Space putting the caret in a Template's own field and
  Escape bringing the keyboard back (KB-23/24); a held Space firing once (KB-26);
  Shift+Tab from after the grid landing on the root with buttons on the page
  (A11Y-17); the chosen action outlined under forced colors too (UX-14).
- `presentation.spec.mjs` — the presentation contract, measured: inline Geometry
  Tokens beat the supported override routes (UX-2), painted geometry equals declared
  (UX-3/ST-3), Visual Tokens recolour from an ancestor (UX-5), nothing under the
  Viewport animates (UX-6), forced colors keep every state tellable (UX-7), the dark
  scheme stays readable (UX-8), the LTR island inside an RTL page (DIR-2/3), the
  editor's box is the cell's (ED-9), the runaway auto-scroll stops (SL-14/15), the
  Blazor error UI never shows (CON-5).
- `virtualisation.spec.mjs` — the far corner at 100,000 rows, element-count stability
  across scrolling, and the overlay's per-rectangle economy on a 10⁷-cell selection.
- `mud.spec.mjs` — the Wrapper contract with a real Wrapper, `ExGrid.MudBlazor` on
  `/mud` (ADR-0030): painted geometry equals declared under the Wrapper's stylesheet
  and Roboto (UX-3), nothing under the Viewport animates (UX-6), the Focus outline
  keeps 3:1 against the surface in both palettes (UX-9), a theme switch re-renders no
  row (RR-1), the hover band follows the pointer in the hovered instance only (UX-13,
  ADR-0021's fifth entry), the Wrapper's editor fits the core's box and commits
  (ED-4), the loading bar shows only while loading, and the paper's corners never
  clip (UX-11's premise).

Every spec fails any test on a console `error` or an uncaught page error (CON-1/2).

## What it deliberately does not assert

Timing. Milliseconds never gate (Definition of Done §1); the structural invariants
above are what produce them. The CDP-metrics instrumentation (MEM-2/5, PF-6/7,
BIG-6/7, DOM-5) is still to be written, and is listed as `blocked`, not passed, in
`verification/<date>-<platform>/results.md`. The observational numbers this suite does
write go to `verification/<date>-<platform>/metrics.json`, one key per browser project:
the directory carries the platform because a structural count is comparable across
machines and a timing is not.

## What Chrome owes this suite

**Whatever renders a refusal has to be a live region.** The grid raises a refusal as an
enum and holds no sentence for it, so it cannot announce this one — the announcement is
Chrome's, and a refusal rendered into a plain element is silent to a reader who cannot
see it while ADR-0035 promises the write is "always reported" (A11Y-16). The same goes
for a discarded edit, which nobody even provoked (ED-21).

Two traps live in that, and the DemoHost has hit both:

- A live region announces on **mutation**. Writing the same sentence twice says nothing
  the second time — and the second refusal is the one the user needs, having just tried
  again. The reference Chrome appends an occurrence count so the repeat is a change.
- The confirmations beside it (`#edit-status`, `#paste-status`) are deliberately **not**
  live regions. They report what did happen, and announcing every successful edit would
  bury the one message that matters.

## Traps this suite has already hit

- Cells and header cells are `pointer-events: none` by design — the Viewport is the
  delegated target (ADR-0004). Playwright's actionability check must be bypassed with
  `{ force: true }`; the browser then hit-tests through to the Viewport, exactly as a
  user's click does.
- Restart the DemoHost after any rebuild: a stale host serves a stale app, and the
  first symptom is an unrelated-looking 404 or a page without the newest feature.
- Two checkouts must not share a port. The host is started on whatever port
  `EXGRID_BASE_URL` names (default 5299), and an already-running host is reused — so a
  second checkout running on the default would be testing the *other* checkout's
  code and passing. A parallel agent's worktree runs with its own, e.g.
  `EXGRID_BASE_URL=http://localhost:5399` (AGENTS.md, "Working in parallel").
- Chrome normalises clipboard HTML on read (adds a meta and a tbody): assert on the
  cells, not the exact fragment.
- A chord is two keydowns — the modifier first. A `{ once: true }` listener waiting for
  the key is spent on the modifier; and a `page.evaluate` that registers a listener must
  be awaited before the key is sent, or it races the key through a different channel.
  KB-15 failed half the time on Edge for exactly this, and it looked like the browser
  swallowing the shortcut.
