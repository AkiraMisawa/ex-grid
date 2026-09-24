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
