# 202: A press into the Formula Bar keeps its caret through a render that lands after it

Status: done

**What to build:** found while pinning ticket 201, on the Server host. The cause is in ExGrid's core,
not ExSheet. `=A1+B2` typed into D10, Enter, a press on D10 and a press into the Formula Bar's text
just after `=A1`, all at once; then F4. Typed slowly, F4 selects `$A$1` (`=$A$1+B2`, ADR-0051's
caret rule). Typed at once, it rewrote `B2`: 1 run of 6 on the Server host, `--project=chrome`.

**Today:** the press into the bar is held behind the press on D10 (ADR-0021's note of 2026-10-01),
and its default stands: DOM focus goes to the bar and the caret lands after `=A1`. The renders of
the Enter's move (D11, an empty bar) and of the press on D10 (the bar showing `=A1+B2` again) land
after it, and each writes the bar's text, which moves the browser's caret to the end. The edit the
bar's focus opened keeps the browser's caret (the opening text equals what the bar showed), so F4
carried a caret at 6 that the user had not placed, and the move the press had made was forgotten
because the text changed under it (`movedByUser`). The bar's text the press was taken against is
the same text in the end, so neither refusing nor keeping the press is obviously right: that is the
decision this ticket needs. The trace is in the comment below.

**Not changed by ticket 201:** `f4-enter-mode.spec.mjs` answer 2 (c) reads the bar's text to place
its press, so it waits for the grid to settle before it presses (ADR-0220, rule 2), which is where
a test reads; this ticket is the product side.

**The fix:** the press into the bar, held among the keys, keeps the text it was made on (the field's
value at the press) and the caret it placed there (read at its release, while the field still holds
that text). At its turn, once the core has answered it, where the bar holds that text and DOM focus,
the listener puts the caret back where it was pressed and notes it as the user's move, reported to
the core as such; F4 then cycles the Reference pressed, and a placement of the core's landing later
leaves it standing, as it leaves any caret the user moved. Where the bar holds another text, the
press placed no caret in it, and nothing is restored. No listener is added, and nothing is measured
or timed.

- [x] Decide what a press into the Formula Bar's text taken before a render that rewrites the bar
      means at its turn: the caret where it was pressed in the text it was pressed on (option (a)).
- [x] **Layer 3:** `hand-back-races.spec.mjs`, "on a 150 ms circuit, a press into the Formula Bar's
      text straight after Enter and a press on the cell keeps its caret, and F4 cycles the Reference
      pressed": before the fix 20 of 20 failed on the Server host (`=A1+$B$2`); after it 20 of 20
      passed there and 10 of 10 on WebAssembly, `--project=chrome`. The 150 ms round trip makes the
      window wide on purpose (ADR-0220, rule 5) and lets the test know the text it presses on; at
      0 ms the same sequence failed 20 of 20 too.
- [x] **Layer 2:** `ReferenceCycleTests`: an edit a press into the bar opens on the text the bar
      shows places no caret over the press's, and the press's caret, told as the user's, is the one
      F4 cycles from.
- [x] `f4-enter-mode.spec.mjs` answer 2 (c) keeps its settled read before pressing the bar: it reads
      where to press from the bar's text, and the Enter's move can leave an empty bar between that
      read and the press (ADR-0220, rule 2), which is a reading, not this race.

## Comments

2026-10-16, claude/settled-races. Core trace of the failing run: the press on D10 answered at
24.934, the bar's focus at 24.938 (before the Enter's render was painted), the held bar press
answered at 24.994, and F4 carried `caret=6-6 moved=False`; in the passing runs the bar's focus came
about 150 ms after the press, and F4 carried `caret=2-2 moved=True`.

*(2026-10-16, decided with the user: option (a).)* Where the bar's text when the press takes its turn is
the text the press was made on, the caret stays where it was pressed, so the outcome is the one a slow
press gives (ADR-0220). Built on its own branch, after tickets 200 and 201.
