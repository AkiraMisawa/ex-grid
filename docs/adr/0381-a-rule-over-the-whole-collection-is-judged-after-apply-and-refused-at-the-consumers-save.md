# A rule over the whole collection is judged after apply, and refused at the Consumer's save

*Decided with the user on 2026-10-26, beside [ADR-0380](./0380-a-verdict-or-a-refusal-may-be-answered-later-and-the-next-input-waits-for-it.md),
from the user's question in the same thread. It was proposed there, and the user approved it with
ADR-0380, saying that the pull request carrying both may merge once reviewed and green.*

## Context

Some rules hold over many rows at once, not over one. The user's example: a trade grid lists trades that
each carry a CSA and a DiscountCurve. The business requires one DiscountCurve per CSA. Changing one trade's
curve so that its CSA now has two breaks the rule. The rule is broken by the whole CSA, not by that trade
alone. The user wants to see which rows and which column breach, and why.

What exists:

- **ADR-0034**: a verdict judges one commit, and the bundled `GridRuleset` adds row rules. A row rule is a
  `Func<TRow, IEnumerable<RuleViolation>>`, so it sees one row. `Evaluate(rows, columns)` re-judges after
  apply and paints `Error` with a message per cell. Its map is keyed by **row instance**.
- **ADR-0140**: a Row Key names a row across versions. The grid uses it for writes and for repainting a
  changed row in place.
- **Neither AG Grid nor Apex Grid has a rule over the collection.** AG Grid's widest rule is one row's
  (`getFullRowEditValidationErrors`). Apex Grid's is the staged row in row mode. Both leave a set rule to
  `cellClassRules` and code of the Consumer's own.

A set rule differs from a row rule in one way that decides its tier: **reaching a valid state often goes
through invalid ones.** Moving a CSA from one curve to another trade by trade breaks the rule at the
first edit and restores it at the last. A Reject at the editor would trap the user before the second edit.
A Ctrl+Enter fill over the CSA's trades would also be judged once, against the editor's row alone
(ADR-0034). That row, judged against the rest still on the old curve, is in breach, so a Reject would block
the very operation that fixes it all at once.

## Decision

### 1. A set rule is Flag-tier, and its Reject is at the save

A rule over the collection never holds the Cell Editor. The edit lands. The rule is judged over the
collection after apply, and every cell it names wears `Error` with its message. **The refusal belongs to
the Consumer's own boundary**: the save, the submit, the send to a server. It refuses while a breach
stands, as a database's deferred constraint is checked at commit and not at each statement. A rule that one
row can break and fix on its own stays a verdict, and may Reject (ADR-0380 section 4).

### 2. `GridRuleset` gains a set rule, keyed by Row Key

- **`GridRuleset.Set(rule)`**, where a rule is `Func<IReadOnlyList<TRow>, IEnumerable<Breach>>` (TypeScript:
  `(rows) => Iterable<Breach>`). A **Breach** is one rule's complaint over the collection: its rule's name, its
  message, and the cells it involves as (Row Key, column) pairs.
- **The ruleset is evaluated over the Consumer's whole collection**, not the Window. A breach between a row
  on screen and one far below is still a breach. It is evaluated by the Consumer after each apply, as
  `Evaluate` is today. The cost is the Consumer's rule, typically one pass grouping by a key. The ruleset
  does not re-run unchanged groups; that optimisation is the rule's own.
- **A ruleset that holds a set rule is made with a Row Key function**, the same function the Consumer's
  Source declares (ADR-0140): `new GridRuleset<TRow>(rowKey)`. Its map is then keyed by Row Key, and its
  state and message lookups read the key of the row they are asked about. An instance key cannot name a
  row outside the Window, and a Breach names rows wherever they are. `Set` on a ruleset made without one
  throws, naming the reason.
- **Every involved cell carries the Breach's message, and the message names the others**: for example,
  "CSA ABC has two discount curves: USD-SOFR on 12 trades, USD-FF on 1 (T-0412)". The words are the
  Consumer's. A row is named by what the Consumer shows (a trade id), never by its position, which a sort
  changes.

### 3. The grid offers the breaches as a list a user can walk

- **The ruleset answers the breaches it holds**, in an order that does not depend on the Window: count,
  each Breach, and its cells.
- **The grid gains a step to the next and the previous error cell**, Error being the Cell State a Breach
  and a Flag both paint. The grid knows only the cells it paints, so **the step is asked of the Consumer**,
  as Find is ([ADR-0055](./0055-find-is-asked-of-the-consumer-like-sort-and-filter.md)): a seam `findError(from, direction)`
  answered with the position of the next error cell, or none. The bundled ruleset implements it. It looks up
  each Row Key's position in the Consumer's ordered collection, which it is handed with the rows it
  evaluates. A Consumer without the ruleset answers from its own errors. A grid without the seam offers no
  step. It never steps through only the cells it has painted, which would skip every error outside the Window
  without saying so.
- **The key and its words are Chrome's**: the built-in Chrome's English, replaceable through its label
  function (ADR-0203), and the MUI and MudBlazor Wrappers' own. The count shows in the status line beside
  the Selection Summary when the Consumer turns it on.

### 4. Paste, fill and program code reach the same marks

A set rule is judged after apply, so it does not matter how the change came: a typed edit, a paste, a fill,
a live update from a server (ADR-0141), or code. One implementation, as ADR-0034 asked of the ruleset.

## Considered options

- **Reject a set rule at the editor**, using ADR-0380's asynchronous verdict to ask the collection. It is
  possible, since the Consumer holds the collection, and right for a rule one row can satisfy alone. For a
  rule over a group it traps the user mid-migration and blocks the fill that would fix it. *Refused as the
  default; left to the Consumer for a rule it knows one edit can always satisfy.*
- **The grid holds the collection's rules and evaluates them itself.** The grid neither holds nor
  executes (spine rule 3), and it does not have the rows outside the Window. *Refused.*
- **Mark only the edited cell.** That is cheaper, but the other side of the breach is invisible. A user
  fixing it from the other side would see nothing change. *Refused.*
- **A problems panel drawn by the grid**, like an IDE's. It is a second surface to keep in step with the
  cells. The status count and the next/previous step reach every breach with the grid's existing
  surfaces. A Consumer that wants a panel builds it from the ruleset's list. *Recommended against for now.*

## Consequences

- `CONTEXT.md` gains **Breach**.
- ADR-0034's note of 2026-10-26 says that a rule over the collection is Flag-tier, and that it is refused
  at the Consumer's save.
- `GridRuleset` gains `Set` and Row Key keying, in C#, and the TypeScript ruleset is ported with it. A
  ruleset without a Row Key keeps today's instance keying for column and row rules.
- The Definition of Done gains ED-36 to ED-38: the set rule's marks on every involved cell inside and
  outside the Window, its message, and the next/previous error step reaching an unpainted row.
- The Docs Site's Editing page shows the trade example: a CSA moved to a new curve trade by trade, its
  breach marked on every trade and cleared by the last edit, and a save refused while it stands.
