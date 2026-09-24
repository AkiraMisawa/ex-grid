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

## Running on Windows itself

VZ-14 is only discharged by browsers on a real Windows desktop set to 125%, with the OS
doing the scaling. From a WSL2 checkout, only the runner and the browsers need to be on
Windows. The DemoHost can stay in WSL: Windows reaches `localhost:5299` through WSL's
localhost forwarding, and `reuseExistingServer` takes the running host.

1. In WSL: `nix develop -c dotnet run --project samples/ExGrid.DemoHost --urls http://localhost:5299`
2. Copy this directory to a Windows path, `node_modules` included but without
   `node_modules/.bin`. Playwright is plain JavaScript, so the Linux install runs on
   Windows unchanged. A UNC path to the WSL checkout does not work, because `cmd` refuses
   a UNC working directory.
3. On Windows, with any Node on `PATH` (the official zip, unpacked, is enough):
   `node node_modules\@playwright\test\cli.js test`

The observational numbers land in `..\..\verification\<day>-windows` relative to the
copy. Move them into the repository's `verification/`.

**Changing the display scale needs a sign-out on some machines, and a sign-out stops
WSL.** Record what has run before you sign out. After you sign in again, Chrome's first
launch can be the one that applies a waiting update. It exits with code 0, and the first
test fails at 0 ms with `browserType.launch: Target page, context or browser has been
closed`. That is not a result, but keep the log and say so rather than letting a rerun
hide it.

## What it asserts

- `scrollbar.spec.mjs` — the Scrollbar Gutter: the Focus is never behind a bar, at
  DPR 1 / 1.25 / 2, with the platform's bars and with forced classic bars. Those scales
  are CDP's. Playwright's default viewport pins every page at DPR 1 whatever the OS is
  set to, so none of those tests sees the OS scale. The VZ-14 block turns the emulation
  off (`viewport: null`) and refuses to pass unless it is on Windows, at a fractional
  DPR, with bars that occupy layout. Anywhere other than Windows it is skipped by name.
- `features.spec.mjs` — the interaction surface on `/features`, with real keys and the
  real clipboard: the editor's two states (ED-2/3/4), both clipboard formats and the
  refusals (CP-1/3/4/5/6/10/14, PST-1), the keys the grid must not take (KB-15), one
  tab stop (A11Y-4, KB-12), instance independence (DOM-4), header-click sorting
  (SR-1), popovers (UX-11). And on `/cells`, entering a cell by key (ADR-0037): Space
  into a cell with several actions, the arrows choosing and Space firing once, Enter
  never firing (KB-20/21/22); Space putting the caret in a Template's own field and
  Escape bringing the keyboard back (KB-23/24); a held Space firing once (KB-26);
  Shift+Tab from after the grid landing on the root with buttons on the page
  (A11Y-17); the chosen action outlined under forced colors too (UX-14). And the
  popovers' keyboard (ADR-0039): `Alt+↓` opening the Focus column's menu (KB-28), every
  popover taking DOM focus by key or pointer (KB-29), the menu keys — arrows over the
  enabled items with wrap, Home/End, Enter/Space run, Tab closes (KB-30) — the panel's
  Tab wrapping inside it and Enter in its value field applying what OK applies (KB-31),
  and every close handing the keyboard back to the root (KB-32).
- `stripes.spec.mjs` — Row Stripes on `/stripes` (ADR-0038), read as painted colours
  from a screenshot rather than as computed styles: a pinned and a scrollable cell of
  one striped row paint the same ground, and the stripe moves with its row (UX-15); a
  group or total row's ground and a Cell State's paint over the stripe, the roles still
  count in the parity, the overlays paint above it, and forced colours paint none
  (UX-16).
- `presentation.spec.mjs` — the presentation contract, measured: inline Geometry
  Tokens beat the supported override routes (UX-2), painted geometry equals declared
  (UX-3/ST-3), Visual Tokens recolour from an ancestor (UX-5), nothing under the
  Viewport animates (UX-6), forced colors keep every state tellable (UX-7), the dark
  scheme stays readable (UX-8), the LTR island inside an RTL page (DIR-2/3), the
  editor's box is the cell's (ED-9), the runaway auto-scroll stops (SL-14/15), the
  Blazor error UI never shows (CON-5).
- `virtualisation.spec.mjs` — the large-data criteria at the Definition of Done's own
  scenario, `/wide` at 10⁶ rows × 100 columns (§12): the far corner reached and painted
  (BIG-1), the same element count at 10³, 10⁵ and 10⁶ rows (VZ-1, BIG-2, DOM-1 —
  `/wide?rows=N`), the first and last rows painting their own data there and back
  (BIG-5), Ctrl+A over 10⁸ cells as one rectangle with the next key answered (BIG-3),
  and at most one interop call per scroll frame, counted over CDP by breakpoints that
  never pause (PF-1).
- `memory.spec.mjs` — on `/lifecycle`, a grid mounted and disposed fifty times leaves the
  browser's node and listener counts where they were (MEM-2), and a dispose takes the
  module's five listeners off the root (MEM-4). The ten-minute soak (MEM-5, with the
  managed heap for MEM-6) runs only with `EXGRID_SOAK=1`:
  ```sh
  EXGRID_SOAK=1 npx playwright test memory.spec.mjs
  ```
- `observational.spec.mjs` — the numbers that are recorded, never gated, into
  `metrics.json`: mount to first row at 10⁶ (BIG-7), the DOM with horizontal
  virtualisation on and off (DOM-5), the settle repaint and the frame intervals at both
  settings (PF-6, and BIG-6 as its "on" half), and a selection drag's cost per step
  (PF-7). Each one asserts only that it measured something.
- `mud.spec.mjs` — the Wrapper contract with a real Wrapper, `ExGrid.MudBlazor` on
  `/mud` (ADR-0030): painted geometry equals declared under the Wrapper's stylesheet
  and Roboto (UX-3), nothing under the Viewport animates (UX-6), the Focus outline
  keeps 3:1 against the surface in both palettes (UX-9), a theme switch re-renders no
  row (RR-1), the hover band follows the pointer in the hovered instance only (UX-13,
  ADR-0021's fifth entry), the Wrapper's editor fits the core's box and commits
  (ED-4), the loading bar shows only while loading, and the paper's corners never
  clip (UX-11's premise).

Every spec takes `test` from `fixtures.mjs`, which listens to every page from before its
first navigation and fails a test on a console `error` or an uncaught page error
(CON-1/2), on a warning from ExGrid's own code (CON-3), and on an unhandled exception at
any level of the WebAssembly host's log, which is the browser console (CON-6). What a
test's console said is kept in `console.json` beside `metrics.json`, third-party
warnings included, for `results.md` to list. The dev server's own output is piped into
the run's output, which Step 4 of §22 tees into `layer3.log`.

## What it deliberately does not assert

Timing. Milliseconds never gate (Definition of Done §1); the structural invariants
above are what produce them. The observational numbers go to
`verification/<date>-<platform>/metrics.json`, one key per browser project: the
directory carries the platform because a structural count is comparable across machines
and a timing is not. A headless run in a container renders in software, and its numbers
belong to no trend — do not file them.

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
