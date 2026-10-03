# Delete raises a Clear Intent, not a paste of nothing

*(Renumbered 2026-09-28 when `main` was merged with the ExSheet branch, which had taken 0046–0053. It was ADR-0046 on `main`.)*

In Excel, Delete on a selection empties every selected cell. ExGrid did not claim the key at all:
on a grid with editable columns it reached the page and did nothing. The gesture is too ordinary
to leave out of a component that claims Excel's operability, and it is a write, so it has to pass
the same gate every other write passes
([ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)).

**Decision: Delete on the grid raises one `GridClearIntent` covering the whole selection — the
target ranges and the Row Sequence Version, and no value.** It is judged by the paste gate and
refused whole when that gate refuses. It is **not** a paste of an empty 1×1 block.

## Why not a paste of `""`

A paste of an empty string would have cost nothing to build — Ctrl+Enter already raises a paste
intent with a 1×1 source, and the gate, the tiling and the notification exist. It was rejected
because it says the wrong thing.

- **A paste carries text for the Consumer to parse.** An empty string is text. On a text column
  it is a value; on an amount column it is a parse failure, or a zero, depending on how the
  Consumer's parser treats nothing. Excel's Delete leaves a Blank — no value — which is neither.
- **So the same key would mean three things** depending on the column type and on a parser the
  grid cannot see, and the likeliest of them on an amount column — a quiet zero — is the spine's
  first rule broken: a plausible number where there should be none.
- **A dedicated intent says "no value" by construction.** There is no field for a value, so there
  is nothing to misread. The Consumer maps it onto its own notion of Blank
  ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)'s null),
  per column, where the knowledge is.

## What it shares with paste, and what it does not

- **The gate on the operation is shared.** The call is `PlanPaste` over a 1×1 shape, so the
  order of refusals is the one ADR-0035 fixed: `EmptySelection` → `TargetNotEditable` → shape
  (which a 1×1 source always passes). A selection covering even one non-editable column refuses
  the whole clear — clearing the editable part would leave the status area's count saying one
  thing and the data another. The refusal is reported through `OnPasteRefused`, as a fill's
  already is: one channel for "a write was refused", whose name is older than its scope.
- **There is no verdict.** A clear is not judged on value
  ([ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)): there is
  no value, and no editor is open to hold. A Consumer that forbids a Blank in some column says so
  when it applies the intent — the Consumer is the last word on every write (ADR-0007).
- **Rows outside the Window are included,** as they are for a paste
  ([ADR-0014](./0014-paste-shape-rules-and-selection-count.md)): the intent is positional, and the
  version is how the Consumer tells whether the order moved under it
  ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
- **One intent for the whole selection,** so one Ctrl+Z (ADR-0007).

## Where the key is claimed

- **Only on a grid with an editable column**, like the printable keys that open the editor
  (ADR-0010/0020). A display-only grid lets Delete through to the page.
- **Not while editing.** Inside the Cell Editor, Delete deletes a character, as it always has.
- **With no Focus, Delete only places it** — the first-key rule of ADR-0012, the same as Space's.
  A key that names "the selection" has nothing to name until there is one.

## Consequences

- **A new public type, `GridClearIntent`, and a notification, `OnClear`.** Without a listener
  the grid still judges the operation, so a refusal is still reported; an approved clear with no
  listener goes nowhere, as a paste with no `OnPaste` does.
- **Backspace is not this.** In Excel Backspace empties one cell *and opens the editor on it*; it
  is an editor entry, recorded with the fill keys in
  [ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md).
- **`CONTEXT.md` gains Clear Intent.**
