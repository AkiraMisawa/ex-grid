# The rows once checked by hand run on CI's own Windows and macOS desktops

*(2026-11-02. The user asked that the Definition of Done's rows checked by hand be checked by a machine
wherever one can; this ADR is how, and what cannot be. It amends
[ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md),
whose rows it takes out of the record index one by one, and
[ADR-0041](./0041-ci-runs-every-layer-and-layer-three-gates-on-linux.md), whose layer 3 gated on Linux
alone.)*

ADR-0208 keeps a check by hand true for as long as the code it covers is unchanged. That made a release
wait for a person at a desktop only when the code moved. The move of the whole family to TypeScript
(ADR-0370) moved all of it: on 2026-11-02 every row checked by hand had lapsed, and `0.2.0`'s release
candidate waited on a Windows desktop and a Mac.

Most of those rows are not about a person. They ask what the browser does on a real desktop: the OS's
display scale reaching the page, the browser's own shortcuts and menu meeting a key before the page
does, a real browser zoom told to the grid. A hosted runner has a desktop.

## What the hosted runners were measured to do

On `windows-latest`, 2026-11-02, by probe runs on this ADR's branch:

- **The session is interactive**, with a display at 1024x768, where Windows offers no scale above 100%.
  After `Set-DisplayResolution -Width 1920 -Height 1080 -Force` it offers up to 175%.
- **The display scale is set live**, without a sign-out, by `DisplayConfigSetDeviceInfo` (the call
  Settings' own scale list makes, relative to the recommended step). Chrome and Edge started after it
  report `devicePixelRatio` 1.25 and 1.5.
- **Keys sent through Windows' input queue** (`SendInput`, a virtual key with its scan code) reach the
  browser before the page, as a keyboard's do: Ctrl+1 switched the tab unless the page called
  `preventDefault`; Alt released alone and F10 opened the browser's menu unless prevented, which showed as
  the next key's `keydown` never reaching the page; Ctrl+= zoomed the page and fired the `matchMedia`
  change.
- **A key Playwright presses does none of that.** It is dispatched to the page alone, so a test of a
  browser shortcut passes whether or not the browser would have taken the key. That is why these rows
  were by hand.

On `macos-latest`, the runner has a desktop and Chrome, and Playwright's `ControlOrMeta` presses Command
there, which is all KB-3 and FN-12e on macOS ask of a Mac.

## The decision

### The rows CI runs

Two jobs run on the hosted desktops (`.github/workflows/ci.yml`), `windows-desktop` and `macos-desktop`.
They run in the long run: weekly, on a dispatch that asks for them, and in the long run `release.yml` calls
for a release candidate and for a version without a suffix (ADR-0205). A red job fails that call, so each
row gates every release candidate and every version without a suffix, at its own commit. They also run on a
pull request that changes their own machinery (`os-input.mjs`, `windows/`, `windows-desktop.spec.mjs`,
`ci.yml`), so a change to how they send keys is judged before it merges.

| Row | Job | What runs |
|---|---|---|
| VZ-14 | `windows-desktop` | `scrollbar.spec.mjs`'s VZ-14 block at a display scale of 125%, then 150%, Chrome and Edge, the React host |
| VZ-16 | `windows-desktop` | `windows-desktop.spec.mjs`: at a display scale of 150%, the browser's zoom raised by its own keys to 150% on `/wide`, each step told to the grid, then `/sheet?case=11` at 2.25 read in the compositor's pixels |
| FN-12e (Windows) | `windows-desktop` | `sizing.spec.mjs`'s and `part-c.spec.mjs`'s FN-12e tests, Chrome and Edge |
| SH-42 | `windows-desktop` | `windows-desktop.spec.mjs`: Ctrl+1 to Ctrl+5 through the OS on `/sheet`, in a window of five tabs, each key naming a tab other than the Sheet's |
| SH-51 | `windows-desktop` | `windows-desktop.spec.mjs`: Ctrl+F1 through the OS |
| SH-52 | `windows-desktop` | `windows-desktop.spec.mjs`: Alt released alone, and F10, through the OS, then a row's letter and an item's, under both Chromes (the built-in on the React host, MudBlazor's on the WebAssembly host); and with an edit open, Alt and F10 start nothing |
| FN-12e (macOS) | `macos-desktop` | the same two FN-12e tests, Chrome and Edge |
| KB-3 (macOS) | `macos-desktop` | every test that fills with `ControlOrMeta+Enter`, the editor's key path included, and the two Apple-only caret tests, on the React host and the Server host |

**A key the browser could take goes through the OS.** `tests/browser/os-input.mjs` brings the page's tab
and window to the front, refuses to send when the window in front is not the page's, and sends the
chords in one `SendInput` call followed by F24, which nothing binds. It then waits for the page to see F24.
A key the browser took for itself left F24 to the tab it switched to, or to the menu it opened, so the
wait fails naming the chords. This is the reading ADR-0220 asks for: the result does not depend on when
the test looks, because F24 reaches the page only after every key before it has been handled.

**A key the page leaves to the browser can be acted on late.** The browser opens its menu for Alt
released alone once the page has answered the key, and keys sent in the same call can land on the page
before that: on this ADR's branch, 2026-11-02, a page that prevented nothing saw Alt, F23 and Escape in
Chrome, and Alt and F23 in Edge, whose menu then took the Escape. Nor does the page learn of it:
`document.hasFocus()` stayed true while the menu held the keyboard. So `osKeys` sends a second F24 in a
call of its own once the first has landed, without bringing the page to the front (which would hand it the
keyboard back), and waits for it too; a menu opened late takes it. This narrows what a single call could
miss, and is not a proof: a menu opened later than that round trip would go unseen, until the row's next
keys, which such a menu would take.

**The method is held by a negative control.** `windows-desktop.spec.mjs`'s first test sends keys the
browser is known to take, on plain pages that prevent nothing: Ctrl+2 switches to the second tab, which
sees only the F24 after it; and once the page has seen Alt released alone, F23 and Escape, each in a call
of its own, are the menu's, so the page sees only the F24 sent after them. Were the keys reaching the page
first, as Playwright's do, that test would fail, so a pass of the rows above is not a pass that would have
come anyway.

**A row CI runs leaves the record index.** Its row goes from the Definition of Done's Step 4 table and from
`verification/by-hand.json`, so the release check no longer asks for a record of it. What held it, a
record whose paths have not changed, is replaced by the run itself at the commit being released, which is
stronger: the record held while the paths it named were unchanged, and the run holds whatever changed. FN-12e and KB-3 on macOS, made Known limitations of `0.2.0` on 2026-10-25 because no Mac would run
them, are run, and are no longer Known limitations of it.

### What stays by hand, and why

- **ED-30, a real IME.** The runner installs the Japanese language and the Microsoft IME (its capabilities
  take about half an hour to add). Whether a composition it starts from keys sent through the OS reaches
  Chrome and Edge as a person's does is the next measurement. Until it is made, ED-30 stays by hand, and
  this ADR is amended when it is.
- **VZ-18, the remote-isolation PC: withdrawn, not kept by hand.** The user answered the question the
  Definition of Done's §21 held open since 2026-10-09: what that PC showed was its remote browser isolation
  mirror, not Citrix, and the user asked that it no longer be tested. Its check by hand leaves the
  Definition of Done; VZ-18's mechanism stays held by `virtualisation.spec.mjs`, which still gates, and the
  repaint stays, harmless elsewhere.
- **WR-10, the Wrappers' MudBlazor floor run.** It runs nothing a hosted Linux runner cannot. Moving it
  into CI is a change of its own, not made here. *(2026-11-02, later the same day, as the user's request of
  the morning asked: made. See "WR-10 moves into CI" below.)*

### WR-10 moves into CI

*(2026-11-02, a note added the same day, under the user's request that the rows checked by hand be checked
by a machine wherever one can.)* The floor run of the Definition of Done's Step 4, and the Wrappers' layer-2
runs across their declared range beside it, are three jobs of `ci.yml`: `mudblazor-floor-hosts` publishes the
two Blazor hosts and the demo API server with every sample's MudBlazor pinned to the floor the Wrappers declare,
and reads back the version each host loads; `mudblazor-floor` runs the tests under a Wrapper against them, on
both hosts, in each browser project, sharded as the full run is; `mudblazor-range` runs each Wrapper's layer-2
suite at every stable version nuget.org's registration lists within the range that day. They run when the
desktop rows do, in the long run among them, and on a pull request that changes a Wrapper's project.

So WR-10 leaves the record index as the desktop rows did, and with it what ADR-0205's notes of 2026-10-12 built
for a run a person made once: the floor record held by its paths, the layer-2 records of versions published
after it, each a run of its own in `laterRecords`, and the release check's reading of nuget.org's list against
the versions the records named. A run at the commit being released, with the versions listed that day, asks
for each of those by running it. The release tool's `--mudblazor-index` and the code that read the list went
with them. The procedure is the one a person ran; what it typed, the floor `9.6.0` and the version list, it
now reads, from the Wrappers' projects and from nuget.org.

### What a runner is not

A hosted runner is Windows Server with the desktop experience, not Windows 11; its keyboard is the input
queue, not a device; it has no GPU of its own. What these rows ask, the OS's scale reaching the window,
the browser's accelerators and menu meeting a key first, a zoom reaching `devicePixelRatio`, is the
browser's and the OS's window manager's, which the runner has. A defect that needs a real device or a
graphics driver is out of their reach, as it was out of a by-hand run's on one machine.

## Considered options

- **Keep the rows by hand, and run them at each release candidate.** Half a day of a person per
  release, ADR-0208's reason for holding records by their paths, and the reason the user asked for this.
- **A self-hosted runner on the user's Windows desktop.** It would run on Windows 11 and a real keyboard,
  but only while the desktop is on and signed in, and a job would take the user's screen. Rejected: a
  release would wait for the desktop.
- **Chrome DevTools Protocol's emulation in place of the OS.** CDP's `Emulation.setDeviceMetricsOverride`
  scales the page without the OS, and its key events reach the page alone. The Definition of Done says of
  VZ-14 and VZ-16 that emulation does not discharge them. Rejected for the same reason.
