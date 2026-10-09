# Arrow keys scroll the page on an organisation's PC: the page was a remote browser isolation mirror

Date: 2026-10-09. Based on `4a17e3a`. No file under `src/` was changed.

This records an investigation made with the user. On the user's work PC, pressing an arrow key in a
grid on the Docs Site moved the Focus and also scrolled the page. Nobody could reproduce that
anywhere else. The cause turned out to be the network the PC sits on, not the grid. This README
decides nothing: no criterion of the Definition of Done and no `CONTEXT.md` entry is changed.
ADR-0012 carries a dated note pointing here, because this investigation changes the explanation that
ADR gave for the white Viewport of 2026-10-08. The proposals are in the last section.

The organisation, its internal hosts and applications, and the product it uses for isolation are
not named. They are not needed to reproduce the finding, and they are not this repository's to
publish.

## The results in brief

| | Finding |
|---|---|
| **What the user saw** | On the work PC, an arrow key in a grid on the Docs Site moved the Focus and, at the same moment, scrolled the page. It was most visible where the grid fills little of the page. Now and then the page did not scroll |
| **Where it happened** | Only on that PC: Edge and Chrome both, reached over a remote desktop connection. Not on the user's own Mac, nor anywhere this project runs its browsers |
| **The cause** | The PC's browser does not run the Docs Site. It shows a **mirror** of a page running in a cloud browser, run by the organisation's remote browser isolation (RBI) service. The grid's script runs only in the cloud browser. A key reaches the mirror first. Nothing there calls `preventDefault()`, so the local browser scrolls the page by it. The key is also forwarded to the cloud browser, where the real grid moves the Focus |
| **Can the grid prevent it?** | No. The grid's script never runs in the browser that scrolls. CSS is the only part of the page that reaches the mirror, and no CSS tried keeps an arrow key's scroll off the page (below) |
| **What avoids it** | A site the RBI service does not isolate. On the same PC, an internal site served from the organisation's own host ran its scripts locally. Hosting ExGrid on such a host, or on `localhost`, should avoid this. Not yet checked with ExGrid itself |
| **The white Viewport of 2026-10-08** | Was seen on the same PC and the same site, so it was behind the same mirror. ADR-0012 put it down to Citrix's display path. The mirror now explains its symptoms at least as well. Not settled; how to settle it is below |

## The path a key took

```
the user's device ──remote desktop──▶ the work PC ──▶ Edge or Chrome, showing a mirror ──▶ the RBI service's cloud browser
                                                       (no grid script runs here)          (the Docs Site runs here)
```

## What was checked, in order

Each step on the work PC was done by the user and shared as a screenshot.

1. **Ruled out first, here.** Layer-3-style probes ran on Playwright's bundled Chromium (build 1194,
   Linux, headless, 1280×800). They covered 37 grids on 20 pages of the Docs Site, the three
   Showcases among them, and the grids of 22 DemoHost pages. After a click on a cell, these keys
   were pressed: the arrows, presses held at an edge, PageUp and PageDown, Ctrl+Home and Ctrl+End
   on every grid, and Home and End on the ExGrid pages. With the grid in the middle of the window,
   the page scrolled on none of these keys. On a subset of pages, the grid was also placed partly
   below the window's bottom and partly above its top. That subset also took a burst with no wait
   between presses, typing then Enter, typing then Tab, and F2. With the CPU slowed six times, the
   renderer's accessibility forced on, and device pixel ratios of 1.25, 1.5 and 1.75, the arrows
   still scrolled nothing. The one page scroll seen came from opening an editor ("Found on the way",
   item 2), never from an arrow key.
2. **Accessibility software.** This was a hypothesis: software reading the screen asks the browser
   to bring the active descendant into view, and that scrolls the page. Ruled out. On the work PC,
   `edge://accessibility` and `chrome://accessibility` showed "Web accessibility" off and no UIA
   client. The user's Mac, where nothing happened, had more turned on.
3. **A trace on the work PC.** A snippet pasted into the Console logged each `keydown` as it reached
   `window` in the bubble phase, with `defaultPrevented`, along with every page scroll and any
   `focus()` without `preventScroll` or `scrollIntoView()` call. Every arrow key arrived
   `prevented=false` with target `ex-grid`. The page then scrolled about 40 px in four or five
   steps within roughly 130 ms, the browser's own smooth arrow-key scroll. When the grid takes a
   key it calls both `preventDefault()` and `stopPropagation()`, so a key it took never reaches a
   listener on `window` at all. The grid had not seen these keys.
4. **Patched natives or an extension.** Ruled out. `EventTarget.prototype.addEventListener`,
   `Event.prototype.preventDefault` and `Event.prototype.stopPropagation` were native. No extension
   was installed, and an InPrivate window behaved the same.
5. **Is the grid's code on this page at all?** On the Docs Site on the work PC, `typeof Blazor` and
   `typeof DotNet` were `"undefined"`, and no `ex-grid` resource had been loaded. DevTools' Sources
   panel listed only the RBI service's own scripts and frames, from hosts of the service. On the
   user's Mac the same URL listed `_content/ExGrid/ex-grid.min.js` and the rest of the site.
6. **An internal site on the same PC.** A Blazor WebAssembly application hosted on the organisation's
   own host listed its own `_framework/` files in Sources. On it, `typeof Blazor` and `typeof DotNet`
   were `"object"`. Internal sites are not isolated.

The trace in step 3, as it was pasted (it changes the page for as long as the tab is open):

```js
(() => {
  const t0 = performance.now(), ms = () => `${(performance.now() - t0).toFixed(0)}ms`;
  const name = (t) => t === document.body ? 'BODY' : (t?.className || t?.tagName || String(t));
  addEventListener('keydown', (e) => console.log(`${ms()} keydown ${e.key} keyCode=${e.keyCode} composing=${e.isComposing} prevented=${e.defaultPrevented} target=${name(e.target)}`), false);
  let last = scrollY;
  addEventListener('scroll', (e) => { if (e.target === document) { console.log(`%c${ms()} PAGE scroll ${last|0} -> ${scrollY|0} active=${name(document.activeElement)}`, 'color:red'); last = scrollY; } }, true);
  const f = HTMLElement.prototype.focus;
  HTMLElement.prototype.focus = function (o) { if (!(o && o.preventScroll)) console.log(`${ms()} focus() without preventScroll on ${name(this)}`); return f.call(this, o); };
  const s = Element.prototype.scrollIntoView;
  Element.prototype.scrollIntoView = function (o) { console.log(`${ms()} scrollIntoView on ${name(this)}`); return s.call(this, o); };
  console.log('diag on');
})();
```

The check in steps 5 and 6:

```js
({ blazor: typeof Blazor, dotnet: typeof DotNet, gridJs: performance.getEntriesByType('resource').filter(e => /ex-grid/.test(e.name)).map(e => e.name), href: location.href })
```

## Why "most visible where the grid fills little of the page", and "now and then not"

- The mirror's page scrolls by the browser's step for an arrow key, whatever the grid does. A page
  with room to scroll shows it, and a short grid leaves the page more room.
- Why it sometimes did not scroll was not observed. The RBI service pushes the cloud browser's
  state to the mirror, its scroll positions presumably among it. A push landing just after the
  local scroll would undo it. This is a guess.

## No CSS keeps the scroll off the page

CSS reaches the mirror; script does not. A page made like the mirror was built: no script, a
focused `tabindex` root holding a scroller, with page above and below. On it, three arrow presses
and a PageDown were tried with the root as it is today, and with `overscroll-behavior: contain`,
`overflow: auto` plus `overscroll-behavior: contain`, `overflow: hidden`, and `overflow: clip` on
the root. That was done with a grid of 200 rows and of 5. The page moved the same 732 px in all ten
cases (Playwright's Chromium, build 1194).

An arrow key the page does not take scrolls the focused element's nearest scrollable ancestor. The
root is not one, so the key goes to the page. Making the root itself scroll would move the grid's
rows under the key instead, which is the mix the user first reported. Taking the key in the mirror
needs a script there, and none of the page's scripts runs there.

## What this means for the white Viewport of 2026-10-08 (ADR-0012, VZ-18)

That PC, that site, the same Ctrl+↓, Ctrl+↑ and PageDown. ADR-0012's section of 2026-10-08 records
the symptoms and puts them down to Citrix's display path with hardware acceleration off. Its records
read differently once the mirror is known:

| Recorded on 2026-10-08 | Read against the mirror |
|---|---|
| The DOM, the offset and the painted slice were right; the screen was white | The trace reads the page the grid runs in, the cloud browser's. The screen shows the mirror. The two can disagree |
| Price updates inside the slice were not drawn either | The mirror stopped taking updates for a while |
| Under ArrowUp the rows appeared one by one, in their right places | A mirror applying the cloud page's changes as they arrive would show exactly this |
| A scroll by the user, a resize or any later move of the offset painted everything | A scroll is an event the mirror re-synchronises on |
| One pixel away and back, after the reveal, painted the rows | It raises scroll events in the cloud browser after the reveal, and the mirror re-synchronises |
| Nothing matched at home reproduced it | No match had the mirror in the path |

Neither explanation is proven. Two layers stand between the screen and the grid there: the remote
desktop and the mirror. One run sorts them. Serve the Docs Site from the PC itself
(`dotnet run --project samples/ExGrid.Docs --urls http://localhost:5411`, which the RBI service
should not isolate; the check above says whether it does). Then press Ctrl+↓, Ctrl+↑ and PageDown on
`/showcase/blotter` with a build in which the repaint is off:

- **Not white:** the mirror was the cause, and the remote desktop was not.
- **White:** the remote desktop's display path is the cause, as ADR-0012 says.

## Found on the way, in ordinary browsers

Step 1's probes found two things in browsers with no mirror. Neither is about the work PC, and
neither is decided yet.

1. **The page does not follow the Focus.** Where a grid stands partly below the window, the arrow
   keys move the Focus out of sight. The grid scrolls only its own scroller, and the page stays
   where it is. ADR-0012's reveal keeps the Focus in the scroller's view, and nothing keeps it in
   the window's.
2. **Opening an editor scrolls the page, and a move does not.** `focusEditor` in
   `src/ExGrid/Assets/ex-grid.js` calls `field.focus()` without `preventScroll`. This is deliberate,
   the comment says, so that an editor opened below the fold is brought on screen. An edit opened on
   a cell below the window therefore scrolled the page by 442 px on `/exsheet`, while the arrow keys
   that got there had scrolled nothing. Adding `preventScroll` would open the editor out of sight,
   which is worse. Settling item 1 settles this one with it.

## Proposals

1. **Hosting.** An organisation behind an RBI service hosts ExGrid applications on a host the service
   does not isolate, or asks for its host to be excepted. Whether the Docs Site says so as a known
   limitation is the user's call.
2. **The repaint.** Keep it until the run above says which layer needed it. A switch that turns it on
   only in such environments cannot be made automatic. From inside the page, the grid cannot tell
   that it is being mirrored or reached over a remote desktop, and guessing from a renderer string or
   a vendor's markers would make an outcome depend on a guess. A switch would be a Consumer
   parameter, and adding one is an ADR change.
3. **Items 1 and 2 of "Found on the way"**: one rule for when the page follows the Focus. An ADR
   change to ADR-0012's reveal.
