# Row inspectors in the DemoHost, and a page index

Status: ready-for-agent

A DemoHost feature, not a grid feature. It rests on decisions already recorded:
[ADR-0020](../../adr/0020-action-and-template-columns.md) ("what a detail action opens happens
outside the grid"), [ADR-0043](../../adr/0043-row-marks-belong-to-identity-and-are-held-by-the-consumer.md)
(Row Marks, and the race that can only fail loudly),
[ADR-0003](../../adr/0003-cells-are-plain-markup-by-default-not-components.md) (Row Identity),
[ADR-0007](../../adr/0007-edits-are-an-overlay-owned-by-the-consumer.md) (`ReplaceRow`),
[ADR-0011](../../adr/0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md),
[ADR-0018](../../adr/0018-multiple-instances-must-be-independent.md) §5 (a shared store, a Grid
Source per user) and [ADR-0021](../../adr/0021-javascript-is-allowlisted-not-minimised.md). One
question is deliberately left open for the implementation to answer by measurement; see
"Further Notes". Where this spec and an ADR disagree, the ADR wins.

**Naming.** What these pages open is called an **inspector** here — a demo word, not a glossary
term. "Row window" is avoided because **Window** is the slice of rows the Consumer pushes, and
"detail dialog" because **Detail** is a Row Kind. An inspector is not a **Popover** either: the
grid's Popovers are its own UI under the instance root; an inspector is the Consumer's, outside
it.

## Problem Statement

A developer evaluating ExGrid wants to put an action on each row that opens that row's details —
typically in a `MudDialog` — and then wonders:

- If I open details for several rows, do several dialogs open? Can they float and be dragged
  about, so I can compare rows side by side?
- What happens to the keyboard? After the dialog opens, do my keys go to the dialog or to the
  grid? After it closes, can I carry on with the arrows?
- If I tick several rows with Row Marks, how do I open all of their details, and what stops me
  from opening ten thousand?
- If the action changes the row — approves it, or records a note against it — and the server
  changes the same row while the dialog is open, what does the dialog show, and what happens when
  I press the button?

Nothing in the DemoHost answers any of these. The only dialog demo holds a fixed grid, the only
Action Column demo has no dialog at all, and no test checks where DOM focus lands after an action
fires by mouse click. The grid reclaims focus to its root after every action handler returns,
which was written for "click a button, then carry on with the arrows" and never checked against
a handler that opens something that wants the focus itself. That combination can go quietly
wrong: an inspector that looks usable while the keys go to the grid.

Separately, the DemoHost is hard to find one's way around. It has fourteen pages, each with a
hand-written navigation bar listing a different subset, and one page is linked from nowhere.

## Solution

Three additions to the DemoHost, each a model a Consumer can copy.

**A page index at `/`.** Every page, grouped by theme, with a one-line description. The Row
Identity demo that lives at `/` today moves to its own route. Every page carries the same
navigation bar — a link back to the index and the page's own name — drawn from the same single
list, so a page added to the list appears in the index and names itself, and a page missing from
it is caught by a test.

**Page A — opening inspectors.** A MudBlazor page with one grid over in-memory positions:

- An Action Column whose action opens that row's inspector. Each press opens one inspector for
  one row; the grid reports the press and does nothing else.
- A Mark Column, and a toolbar button "Open inspectors (N)" that opens one inspector per marked
  row. The label counts marks outside the current filter too and says so. Above a cap of 10 it
  refuses, with a message naming the count, and opens nothing.
- A switch between two kinds of inspector:
  - **Modal** — a `MudDialog` opened through the dialog service; one at a time, the grid behind
    it unreachable.
  - **Floating** — non-modal panels drawn by the page, several at once, draggable by their title
    bar, brought to the front on press.
- Opening an inspector for a row that already has one brings the existing one to the front
  instead of opening a second.

**Page B — changing a row from an inspector.** The same inspector, over a store that can change
underneath it:

- Two actions inside the inspector: **Approve** (changes the row's status; the row is replaced)
  and **Add note** (appends an entry to the row's history).
- When the row is replaced elsewhere while the inspector is open, the inspector keeps showing
  what it showed and raises a banner — "Changed elsewhere (new notional: …) [Reload]" — with its
  actions disabled until reloaded. When the row is deleted elsewhere, the banner says so and the
  actions stay disabled.
- Every action carries the row version the inspector was showing. The store refuses an action
  against a version that is no longer current, and the inspector shows the refusal. The banner
  informs; the version check guarantees.
- When the inspector's own action succeeds, it adopts the replacement it was handed as its new
  base, and raises no banner for its own change.
- A note records the version it was written against; the history marks notes written against an
  earlier version than the one shown.
- Changes "from elsewhere" come from three places: a page button that changes the row and
  notifies at once; a page button that changes the row and **delays the notification**, so the
  race the banner cannot close can be produced on demand; and, on the Server host, another tab —
  the store is one per process, as on `/shared`.

## User Stories

### The page index

1. As a developer opening the DemoHost, I want the first page to list every demo page, so that I
   can find what I came to look at.
2. As a developer, I want each entry to say in one line what the page demonstrates, so that I do
   not have to open every page to find out.
3. As a developer, I want the pages grouped by theme, so that related demos sit together.
4. As a developer, I want every page's navigation bar to link back to the index, so that I am
   never stranded on a page.
5. As a developer, I want the navigation bar to be the same on every page, and to name the page
   I am on, so that I learn it once and always know where I am.
6. As a maintainer adding a page, I want to add it to one list, so that it appears in the index
   and in every navigation bar at once.
7. As a maintainer, I want a page that exists but is missing from the list to be caught by a test,
   so that no page can become unreachable again.
8. As a developer, I want the Row Identity demo that is at `/` today to remain reachable at its
   own address, so that nothing is lost by the index taking its place.

### Page A — opening inspectors

9. As a Consumer developer, I want to see an Action Column whose action opens that row's
   inspector, so that I can copy the pattern.
10. As a user, I want pressing the action on a row to open that row's inspector, so that I can
    read its details.
11. As a user, I want to press Space on the action cell to open the inspector, as with any
    single-action cell (ADR-0020), so that the keyboard route works too.
12. As a user, I want Enter never to open an inspector, so that holding Enter to move down a
    column does not open one per row (ADR-0020).
13. As a user, I want pressing the action on a second row to open a second inspector when
    inspectors are floating, so that I can compare two rows.
14. As a user, I want pressing the action on a row whose inspector is already open to bring that
    inspector to the front, so that I do not end up with two views of one row.
15. As a user, I want to drag a floating inspector by its title bar, so that I can place it beside
    the rows I am comparing it with.
16. As a user, I want pressing on a floating inspector to bring it to the front, so that
    overlapping inspectors stay usable.
17. As a user, I want to close each inspector on its own, so that I can put away what I have
    finished with.
18. As a user of the modal inspector, I want the grid behind it to be unreachable while it is
    open, so that I do not act on the grid by accident.
19. As a keyboard user, I want the keyboard to be in the inspector once it has opened, so that my
    keys go to what I am looking at.
20. As a keyboard user, I want the keyboard back in the grid when a modal inspector closes, so that
    I can carry on with the arrows.
21. As a keyboard user of floating inspectors, I want the keyboard to stay where I put it, so that
    one inspector closing or opening does not pull it out of another.
22. As a user, I want to tick several rows and open all their inspectors with one button, so that
    I do not have to press each row's action.
23. As a user, I want the button to show how many inspectors it will open, including marks outside
    the current filter, so that I am not surprised by what appears.
24. As a user who has marked more rows than the cap, I want the button to refuse and say how many
    are marked and what the cap is, so that I do not get ten thousand panels — or, worse, an
    arbitrary first ten.
25. As a user, I want marks to survive a sort, so that the rows I open are the rows I ticked
    (ADR-0043).
26. As a Consumer developer, I want to see that the list of marked rows comes from the marks the
    Consumer holds, so that I learn the grid does not hold them.
27. As a developer, I want to switch the page between modal and floating inspectors, so that I can
    compare the two.
28. As a developer on either host (WebAssembly or Server), I want the page to behave the same, so
    that what I learn carries across.

### Page B — changing a row from an inspector

29. As a user, I want to approve a row from its inspector, so that the action sits with the
    details I checked.
30. As a user, I want the grid to show the approved row's new status straight away, so that the
    grid and the inspector agree.
31. As a user, I want the inspector to show the result of my own action without a warning, so that
    my own change is not reported to me as someone else's.
32. As a user, I want the inspector to keep following the row when approval moves it in the grid's
    sort order, so that sorting does not break what I am looking at.
33. As a user, I want a banner in the inspector when the row changes elsewhere, so that I know
    what I am reading is out of date.
34. As a user, I want the banner to show the new value, so that I can judge whether it matters
    before reloading.
35. As a user, I want the inspector's values not to change under my eyes until I reload, so that
    I do not act on a number that changed while I was reading.
36. As a user, I want the actions disabled while the banner stands, so that I cannot act on what
    I know to be stale.
37. As a user, I want to reload the inspector to the current row, so that I can carry on.
38. As a user, I want the inspector to tell me when the row has been deleted, and to disable its
    actions, so that I do not act on a row that no longer exists.
39. As a user who presses Approve in the instant between a change elsewhere and its banner, I want
    the approval refused and told why, so that an approval decided on the old value is never
    applied to the new one.
40. As a user, I want to add a note to a row from its inspector, so that I can record what I
    checked.
41. As a user reading a row's history, I want notes written against an earlier version marked as
    such, so that "confirmed at 1,000" is not read as a statement about 2,000.
42. As a developer, I want a button that changes the row elsewhere and notifies at once, so that I
    can see the banner in one tab.
43. As a developer, I want a button that changes the row elsewhere and delays the notification, so
    that I can see the version check refuse what the banner could not prevent.
44. As a developer on the Server host, I want a change made in another tab to raise the banner in
    this one, so that I see the real shape of the problem.
45. As a developer on the WebAssembly host, I want the page to explain that there is nobody to
    share the store with, as `/shared` does, so that the difference is not a surprise.
46. As a Consumer developer, I want the inspector to track its row through replacements using the
    Consumer's own knowledge of the row, so that I learn Row Identity is the grid's test for
    sameness and not a key the grid hands out.
47. As a Consumer developer, I want to see the version check done by the store, not the grid, so
    that I learn where that guarantee has to live.

### Throughout

48. As a maintainer, I want these pages to add no JavaScript, so that the demo does not model a
    use that ADR-0021 would refuse in the product.
49. As a maintainer, I want the console to stay clean on every page, so that a complaint from the
    browser is treated as a failure.
50. As a maintainer, I want the grid's invariants to hold on these pages — rows skip their render,
    nothing per-cell reaches JavaScript — so that the demo does not teach a pattern that breaks
    them.

## Implementation Decisions

- **Everything lives in the demo pages library**, served by both hosts. No change to `src/` is
  planned; the one possible change is gated on a decision (see "Further Notes").
- **The page list is one piece of data**: route, title, one-line description, group. The index
  page renders it grouped; a shared navigation component on every page renders the link back to
  the index and the page's own title from it. The bar deliberately does not list every page:
  fourteen links would wrap to a second line and move every grid below it, and the layout tests
  on those pages would be measuring a different page. The Row
  Identity demo moves from `/` to `/identity`. The existing per-page navigation bars are
  replaced; nothing else on those pages changes.
- **Page A** has its own MudBlazor providers (theme, popover, dialog), as `/mud-app` does. The
  WR-7 page is left untouched.
- **The inspector is one component shared by both pages**, taking the row it shows and the
  inspector's mode. Modal inspectors are opened through the dialog service with the row as a
  parameter. The page's handler returns as soon as the dialog is shown — the way most Consumers
  will write it. The floating inspector is the same content in a page-drawn panel.
- **Floating inspectors are page state**: an ordered list of open inspectors, each with its row's
  key and its position. Order is stacking order; pressing one moves it to the end. Opening one for
  a key already in the list brings that one to the front.
- **Dragging uses Blazor pointer events only.** While a drag is in progress a transparent layer
  covers the page and takes the moves, so the pointer can leave the title bar. No JavaScript, no
  pointer capture. On the Server host every move is a round trip; that is accepted for a demo and
  stated on the page.
- **The Row Mark route** reads the marks the Consumer holds (`MarkedRows` on the in-memory
  source) and the counts, including the out-of-filter count. The cap is 10. Above it, the button
  refuses with a message naming the count and the cap, and opens nothing. Opening the first ten is
  rejected: it is the quietly wrong outcome (spine 1, spine 5).
- **Page B's store** is one per process, following `SharedTradeStore`: it holds rows with a
  Consumer-side key and a version, announces replacement and deletion, and each circuit builds its
  own Grid Source over it and applies changes on its own dispatcher. Its operations:
  - approve(key, version) → the replacement, or a refusal naming the current version;
  - add note(key, version, text) → the replacement, recording the version the note was written
    against;
  - change elsewhere(key, notify now | notify later) and delete elsewhere(key), for the page's
    buttons.

  A delayed notification is released by a second button, so the race stays under the user's (and
  the test's) control rather than a timer's.
- **The inspector in page B holds its base** (the row it shows and that row's version) and a
  pending state: none, changed (with the new row) or deleted. A notification for its key from
  anyone else moves it to changed or deleted. Its own successful action replaces the base with the
  returned row and leaves the pending state alone. Reload adopts the pending row as the base and
  clears the state. Actions are disabled while the state is not none.
- **Approve changes a sortable status column**, so an approval can move the row under the current
  sort. The grid drops the selection when the order moves (ADR-0011); the inspector, which follows
  the key, is unaffected.
- **Every new id and class in these pages is page-scoped** (a `demo-` style), never `ex-`, which
  belongs to the product (ADR-0018).

## Testing Decisions

- **One seam: layer 3, the DemoHost in real browsers.** No test project references the demo
  pages, and none is added; the behaviour under test is what a user sees in a browser. Every test
  names the ADR it pins in its title, as the existing browser tests do.
- **A good test here asserts what the user sees and where the keyboard is:** which inspectors are
  open and in what order, what the banner and the refusal say, which element `toBeFocused` — never
  page state or the order of internal calls.
- **The page index:** every route in the list opens with a clean console; every page's navigation
  bar links back to the index and names the page with the index's title; `/identity` shows the Row Identity demo. A route served
  by the router but missing from the list fails a test. Prior art: none for navigation; the
  console-clean pattern of the existing MudBlazor app test.
- **Page A — focus after an action fires**, for modal and floating, by click and by Space: after
  the inspector opens, focus is inside it; after a modal closes, focus is on the grid root. The
  handler's two styles (return once shown, await the result) are both exercised. Prior art: the
  Action Column tests KB-20/21 and KB-27 in the features spec.
- **Page A — the rest:** a second press on the same row brings its inspector to the front and
  opens no other; two rows give two floating inspectors; a drag moves an inspector; the Mark route
  opens one inspector per mark, counts out-of-filter marks in its label, and above the cap refuses
  with the count and opens nothing; marks survive a sort. Prior art: the marks spec.
- **Page B:** the immediate-notify button raises the banner with the new value and disables the
  actions, Reload clears it; delete raises the deleted banner; the inspector's own Approve updates
  both grid and inspector with no banner; **the delayed-notify button, then Approve, is refused**
  and the refusal is shown; a note written before a change is marked as written against an earlier
  version. On the Server host, a second browser context changes the row and the first shows the
  banner. Prior art: the two-context shared-store test in the circuit spec.
- **The invariants hold:** the new pages add no script, and the console stays clean throughout
  every test above.
- **If the focus test shows the grid taking focus back from an inspector**, it stays failing until
  the decision below is made and implemented. It is not weakened to pass.

## Out of Scope

- Any change to the grid's behaviour made without an ADR — in particular, to the focus reclaim
  after an action.
- A draggable, non-modal dialog as a product feature of `ExGrid` or `ExGrid.MudBlazor`. The
  floating inspector is page code.
- Inspectors following live values. Rejected: values changing under the reader's eyes.
- Live-updating the inspector's history, merging concurrent notes, or any conflict resolution
  beyond refusing and asking the user to reload.
- Persisting inspectors, their positions or their notes across page loads.
- Resizing floating inspectors, docking, or keyboard moving of them.
- A glossary entry for "inspector". It names demo code only.

## Further Notes

- **The open question: does the grid take the keyboard back from an inspector?** After every
  action handler returns, the grid focuses its own root, so that a clicked action button does not
  keep the keys. With a handler that shows a dialog and returns, the expected order is: the dialog
  takes focus, then the grid takes it back — an inspector that looks usable while the keys go to
  the grid. The page A focus test answers it. If it reproduces, **ADR-0020 and ADR-0037 are
  amended before anything is implemented**, choosing between:
  - reclaiming only when the grid's own button still holds focus — which needs a read of the
    active element, and so a new entry on the ADR-0021 allowlist; or
  - never letting the action button take focus on a press, and dropping the reclaim — no script,
    but where focus lands after a modal closes becomes the design system's choice, which the same
    test then has to check.

  The user decides between them with the result in hand. The ticket that would implement either
  one is written as `needs-info` until then. *(Answered 2026-09-26: it reproduces — see
  Comments.)*
- **The race cannot be closed by the banner.** Between a change elsewhere and its notification,
  the user can press Approve. Only the store's version check prevents the approval from landing on
  a value nobody saw — the same shape as ADR-0043's deleted-row race, which the grid cannot close
  either and which must fail loudly.
- **Row Identity is not a key.** The grid tells the inspector nothing that would let it follow a
  row across a replacement; the store's key does. That is the Consumer's knowledge, and the demo
  shows it as such.
- A ticket that turns out to need a new decision stops and records it first (CLAUDE.md,
  "Implement").

## Comments

**2026-09-26 — page index implemented** (the first of the three additions), through
`/implement`. `DemoPageList` is the one list; `/` renders it, `DemoNav` renders the way back
and the page's name on every page, and the Row Identity demo moved to `/identity`. The
`/mud-app` blotter keeps its application's own drawer, which already links to the index.
`navigation.spec.mjs` passes against both hosts — on the bundled Chromium, headless, in a
container without Chrome or Edge, which is not the layer-3 run ADR-0026 asks for; CI's run is
the one that counts. The navigation bar was narrowed while implementing, from "every page" to
"the index and this page" (see Implementation Decisions).

**2026-09-26 — pages A and B implemented; the open question answered, and it reproduces.**
`/inspectors` and `/inspector-edits` are built as specified, with `inspectors.spec.mjs` and
`inspector-edits.spec.mjs`. Run on the bundled Chromium, headless, against both hosts (not the
ADR-0026 run; CI's is the one that counts):

| Where the keyboard is after the inspector opens | WebAssembly | Server |
|---|---|---|
| Modal, by click, either handler style | inside the dialog | inside the dialog |
| Modal, by Space, either handler style | inside the dialog | inside the dialog |
| Modal closed | the grid's root | the grid's root |
| Floating, by click | inside the inspector | inside the inspector |
| **Floating, by Space** | **the grid's root** — three runs out of three | inside the inspector |

So the grid does take the keyboard back from what the Consumer opened, in one path: the
Space path, on WebAssembly, when the Consumer focuses its own element synchronously after
the handler. The modal escapes only because MudBlazor focuses its dialog later; the Server
host escapes only by the order its messages happen to arrive in. That test is left failing,
as this spec requires, until the decision in "Further Notes" is made.

One fact the options above did not know: **half of the second option already exists.**
ADR-0037 prevents the action buttons' `mousedown` default, so a press never focuses a grid
button, and the keyboard stays wherever it was. The reclaim after the handler is therefore
no longer what keeps a clicked button from holding the keys; what it still does is take the
keyboard to the root when it was somewhere else — which is exactly this failure.

Refined while implementing, without a new decision: a note is version-checked like an
approval (a note decided on a stale view is refused, not recorded); appending a note does
not change the trade's values, so it does not move the version, and a note appended
elsewhere is taken into an open inspector without a banner. In modal mode the marked rows
open one after another, and Escape ends the queue.
