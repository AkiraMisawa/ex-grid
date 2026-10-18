# 01: One ordered input path per grid, so machine-speed input cannot overtake itself

Status: needs-triage
Roadmap: ExGrid, ExSheet, ExPivot — Keys, presses and edits sent at machine speed, by a script or a fast hand, mean what they mean sent slowly, on Blazor Server and WebAssembly alike.

**Why:** the races found on 2026-10-16 and 2026-10-17 were each fixed where they were found, one
entry point at a time:

- ExSheet tickets 220 to 222 (ADR-0230): a key typed at once after a change of state, judged
  against a state the grid had already left.
- ExSheet ticket 240 (ADR-0204's addendum): a popover's opening focus taking the keyboard back from a
  press made inside it.
- ExSheet tickets 260 and 261: a dialog's next render taking back what the user had just chosen.
- PR #148: on the Server host, a key reaching the grid through the capture-phase listener ahead of a
  Blazor input event sent before it, now dropped as `InputOvertaken`.

Each fix is right, and each is local. A key arrives through JavaScript interop, a press or an edit
through a Blazor event, a focus through a render; on the Server host they cross the circuit on
separate paths, and on WebAssembly a handler that awaits lets the next event in. Every new entry
point is a new chance for the same bug, and it is found only when a test happens to drive it fast.
The spine's sixth rule (an outcome never depends on timing) holds today only where someone has
already looked.

**What to do:** make the order structural rather than per handler.

1. Take an inventory of every path by which input reaches an instance of ExGrid, ExSheet and
   ExPivot, on both hosts: the capture-phase keys, Blazor events, the Keyboard Field (ADR-0080),
   held text (ADR-0210), focus moved by a render or a popover, and the Wrappers' MudBlazor dialogs.
   Say for each how its order against the others is kept today, or that it is not.
2. Decide, in an ADR, how input to one instance is put in one order and how a gesture carries what
   it was taken against. ADR-0220 and ADR-0230 are the decisions this one generalises.
3. Build it, and move the local fixes above onto it.

**Blocked by:** the ADR in step 2.

- [ ] The inventory of input paths, on Server and on WebAssembly
- [ ] The ADR
- [ ] Input to each instance taken in one order, with the local fixes moved onto it

## Comments

2026-10-17: Akira chose this as the headline of the version after 0.1.0 (0.2.0). ExCharts comes
after it, built on the one path from its start; its research and spike are on the branches
`research/excharts` and `spike/exposure-chart`, whose tooltip and 3D rotation take input through
JavaScript of their own.
