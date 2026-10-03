# Several grids on one page must not interfere with each other

Placing several ExGrid instances on the same page is ordinary usage. **Everything that touches
something global is scoped to the root element of the instance.**

## Most of it is independent already, thanks to push

The grid holds almost no state
([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md) /
[ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)). The Window, the sort and the
filter are all Consumer-side, and what the grid holds is Selection, Focus, scroll position and
uncommitted editor text — **all of it transient, per instance**.

The danger concentrates in **four places that touch something global**.

## 1. Scope the key capture to the root (most important)

[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) established that the core sees keys
first, in the capture phase. **Attached to `document`, every grid on the page receives every
keystroke.**

```
❌ document.addEventListener('keydown', handler, true)
     → Ctrl+C in grid A and grid B also tries to copy

✅ gridRootElement.addEventListener('keydown', handler, true)
     → fires only while focus is inside that root
```

**Make the root focusable with `tabindex` and attach the listener there.** Which grid is active is
then answered by the browser's focus.
*(Refined 2026-10-02, [ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md).)* On a grid that edits, DOM focus with no edit open is on the
grid's own Keyboard Field, inside the root, and the field is the tab stop. The listener stays on the
root and hears the field's keys first. Which grid is active is answered by
`root.contains(document.activeElement)`. Each grid has its own field; nothing is shared.

Ctrl+C ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)), Ctrl+A
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) and the
Enter cycling ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)) **all ride on this**,
so getting it wrong breaks all of them.

*(Confirmed in a browser while implementing: two grids on one page, real keys through
`Input.dispatchKeyEvent`. Only the focused one moves, and a grid that has just released
its focus with Escape ignores the arrows entirely — the browser scrolls it instead, which
is what an untaken key should do.)*

## 2. Prefix the CSS class names

The CSS in `spikes/render-bench` uses `.r` (row), `.c` (cell), `.sel` (selection), `.window` and
`.scroller`. **That is only acceptable because the spike is disposable; in product code it is
untenable** and will collide with the host application's CSS. Prefix them, as `.ex-row` and
`.ex-cell`.

CSS variables (`--ex-*`) are **defined on the root element**. Put them on `:root` and there is one
theme per page, with no way to give instances different appearances.

## 3. Make the JavaScript a module returning per-instance handles

The spike's `window.bench = { _fps: null, ... }` is a single global, and a second grid calling it
breaks the first. **Make it a module and return a handle per root.**

## 4. Do not let popovers escape the root

The filter panel and column menu float above the grid, but the root is an `overflow: auto` scroll
container, so placing them inside clips them. **Use the Popover API and CSS Anchor Positioning**
(available because [ADR-0017](./0017-target-chromium-browsers-only.md) narrowed the target to
Chrome and Edge). Clipping and stacking order become the browser's problem, there is no need to
portal to `document.body`, and **coordinates and z-index do not tangle across instances**.

*(Replaced on 2026-09-24 by [ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md), for the
reasons recorded there and in ADR-0017. The heading survives the change: a popover stands under
its own root, outside the scroll container, and never extends past its grid's box. So
coordinates and stacking still never tangle across instances, and no ancestor that shows the
grid can cut it.)*

## 5. A bundled Grid Source belongs to one circuit *(added 2026-09-25)*

The four places above are global **to a page**. A Blazor Server application adds a wider
global: the process, shared by every user's circuit. The grid's own statics are either
immutable or safe across threads by construction; the danger is what a Consumer shares.

Two grids on one page sharing one Grid Source is left as it was: it surfaces plainly
(consequences below — both grids show the same Window, sorted the same way, in front of the
same user). **The same object registered once for a whole Server application is a different
failure.** A bundled Grid Source holds the Sort, the Filter and the Window as mutable state
with no lock, and `GridSource.Fetch` captures the synchronisation context of whoever built
it. Shared across circuits, one user's sort reorders another user's grid for a reason that
user cannot see, their selection is dropped under them
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)), a
render in one circuit reads a Window another circuit's thread is replacing, and a fetch's
answer is posted into the wrong circuit. It looks like data, not like a fault.

**Decision: a bundled Grid Source (`GridSource.From`, `GridSource.Fetch`) is bound to the
dispatcher of the first grid that attaches it, and refuses by name — an exception naming the
cause — to be attached by a grid on a different dispatcher while that binding is live.** A
dispatcher is one renderer, which on Server is one circuit, so the same-page case never
trips it. A Consumer's own `IGridSource` implementation is not checked: the grid cannot tell
a source written to be shared (thread-safe, broadcasting one Sort to everyone on purpose)
from one that was not, and a Consumer who wrote one owns that promise.

**Showing the same data to several users is supported, one layer down.** The grid holds no
data ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)), so what is
shared is the Consumer's store: a singleton holding the rows and raising changes, with a Grid
Source per circuit fed from it, each applying the change on its own circuit's dispatcher.
Each user keeps their own Sort, Filter, selection and scroll. A shared *view* — one user's
Sort mirrored to the rest — is the same shape: the Sort and Filter are serialisable
([ADR-0002](./0002-filters-are-a-serializable-model-not-linq-expressions.md)), so the
Consumer broadcasts the model and each circuit's source applies it. Sharing selection or
scroll position is not offered at all: those are per-instance state, and no Grid Source
carries them.

Rejected: **the grid checking every `IGridSource`** — it would refuse the one sharing that is
correct, a source built to be shared. And **documentation only**, as the Fluxor case below is
handled — the Fluxor case shows itself on one screen; this one shows itself as another user's
data changing, which nobody present can trace.

## 6. An open edit stands when the keyboard leaves the grid *(decided with the user, 2026-09-29)*

Found on ExSheet's demo page: `=` typed into a cell, then a click on the positions grid beside it.
The positions grid took DOM focus, as it should. The Sheet's edit stayed open with `=` in it, and
nothing could reach it again. Escape went to the positions grid. A click back on the Sheet's rows
pointed (`=B2`) while the keyboard stayed with the positions grid, so the next Escape went there
too.

- **Nothing is committed or discarded because DOM focus left the root**, whether it went to
  another grid, to a control of the Consumer's, or to nothing. The edit waits, as Excel's does when
  the user switches to another window and comes back.
  - Committing on leaving was rejected. A Formula half typed (`=`, `=SUM(`) is refused by its
    column's verdict ([ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md))
    and would stay open anyway, so leaving would commit some texts and not others. On a Server
    circuit the commit would also race the click that took the focus, such as a Consumer's Undo
    button.
  - Discarding on leaving was rejected. A stray click would throw a long Formula away without a
    word.
- **A key belongs to the grid that has the keyboard** (section 1). Escape pressed in the other grid
  is that grid's, and does not cancel this one's edit. Two grids may each hold an open edit.
- **A press on the grid's own rows or headings brings the keyboard back first, and then means
  what it would have meant.** Where pointing is declared, a press on the rows keeps DOM focus in
  the editor ([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md)), and
  that is what left the keyboard in the other grid. Now the capture-phase `mousedown` on the root
  sees that an edit stands here while DOM focus is outside the root, and puts the keyboard back into
  the editor surface that last held it before the press is handled. The press then points, or
  commits and moves, exactly as if the keyboard had never left, and the hand-back after a commit
  finds the keyboard inside the root.
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) records the line of script.
  - Handing the keyboard back from C# after the press was rejected. On a circuit it lands a round
    trip later, and the keys typed in between would go to the grid the user had just left.
- A click into the editor's own text, the Formula Bar or the Name Box already brought the keyboard
  back, and is unchanged.
- **An edit whose keyboard is elsewhere is told apart** *(decided with the user the same day)*.
  While DOM focus is outside the grid's root, the Cell Editor's outline is drawn 1px wide instead of
  its full width, and it is drawn at full width again when the keyboard returns. The consequence
  below ("distinguish the focused state visually") asks for this once two grids can each hold an
  open edit. The stylesheet does it with `:focus-within` on the root. The colour stays
  `--ex-editor-outline`'s, and no script is involved.
- **A press back within one round trip of the key that opens the edit is not brought back**
  *(accepted with the user the same day)*. Such a press is held behind that key (ADR-0021's
  `mousedown` note of 2026-09-27), and a held press suppresses its default, so DOM focus stays
  where it was and the keys typed next go there. It takes a key, a press elsewhere and a press back,
  all inside one round trip, and only on a circuit. It is recorded here rather than built around.
- **An edit that opens takes the keyboard only while the keyboard is still this grid's**
  *(decided with the user, 2026-09-30)*. The editor surface is focused after the render that paints
  it, a round trip later on a circuit. If the user has meanwhile pressed another grid or a control
  on the page, the edit does not take the keyboard back from there. It is left standing, as above,
  and a press on the rows brings the keyboard to it. Keys typed before the keyboard left are still
  this edit's, and go into it in order. [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)
  records the condition, which is read in script, and why a Chrome's editor asks the core for its
  focus rather than taking it.
  - Found by CI, not by a user: ED-26's test on `/sheets` failed once on the Server host, and the
    same race failed 9 runs in 40 on the base with 40 ms injected.

## 7. A Pointing Scope joins only what the Consumer put in it *(decided with the user, 2026-09-29 and 2026-09-30)*

[ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md) lets a Formula point at
another grid on the page. At a press, that grid keeps DOM focus off itself and hands the press to
the Sheet that points. That is one instance acting on another's behalf, and it is not the coupling
this ADR rules out:

- **The Consumer declares it**, naming each Sheet and grid in a Pointing Scope. Nothing on a page is
  joined implicitly, and a grid in no Scope is untouched.
- **The key capture stays on each root** (section 1). The keyboard stays with the Sheet that points.
  The grid pointed at receives no key.
- **The script shares no state between instances** (section 3). A grid that is pointed at learns from
  its own render which root to tell of a press, and it tells that root by one DOM event
  ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md), note of 2026-09-30).
- **When the keyboard leaves the Sheet that points, the Scope stops pointing**, and section 6 applies
  unchanged. A registered grid with an open edit of its own is never pointed at, so a press on it
  brings the keyboard to its edit, as section 6 says.

## Consequences

- **The selection overlay and the cell editor live in the same coordinate space as the root**
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md) /
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)). Computed in page coordinates
  they would be displaced per instance.
- **The selection count and focused-cell value displays must say which grid they belong to**
  ([ADR-0014](./0014-paste-shape-rules-and-selection-count.md) /
  [ADR-0016](./0016-column-width-and-overflow.md)). Inside the grid that is obvious; if the
  Consumer puts them in an application-wide status bar, **the Consumer resolves which grid is
  meant**. The core only produces the values and does not decide where they go.
- **A DI registration for Chrome (`AddExGridMudBlazor()`) is shared by every instance**, but an
  individual grid can override it. What is registered is read, never mutated by an instance.
- **With Fluxor and several grids, the Consumer must separate the Features.** Two grids reading
  the same `State.Value.Window` show the same thing. **Because the interface is push this surfaces
  plainly as the Consumer's problem** — it does not take the form of the grid hiding state that
  then leaks.
- **A grid that does not have focus does not respond to keys.** The selection stays visible but is
  not operated on. When working across several grids the user must be able to tell which one is
  live, so **distinguish the focused state visually.**
