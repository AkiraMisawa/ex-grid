# The eighteenth Windows run: widening over `####`

Questions gathered from `claude/run17gh-widening` (the seventeenth run's h7 and h8 and the readings of
2026-10-12, R6) for the next Windows run, to be folded into its spec when it is written. An answer that
disagrees with what ExSheet does comes back to the user.

Each case is a new workbook, by R6's procedure (`excel/r6.py`), under the regional format en-US, as
R6 ran (h7 ran under en-GB). Zoom 100%, as h7 states its cases. Unless it says otherwise, A1 holds
123456789.12 in `0.00` at the standard width (8.09), the format and then the number set through COM,
so A1 shows `########`. Then one gesture.

After the gesture, and after every step that sets something through COM, read through COM:
- A1's `Text`;
- column A's `ColumnWidth` and `UseStandardWidth`;
- A1's `Font.Bold`, `Font.Italic`, `Font.Underline`, `Font.Strikethrough` and `Font.Color`.

Take a picture of A1:C3. "Widens" means `UseStandardWidth` false and A1 showing the number. Where a
COM step before the gesture widened the column, set `UseStandardWidth = True` through COM and check
`########` before the gesture.

1. **Format Cells' Font tab, Strikethrough alone** (Effects), then OK.
   - ExSheet widens: a reading.
   - If Excel does not, the Font tab widens for some parts and not others, which needs the user.
2. **The Font tab's Normal font box.** A1 first gets a red font through COM (`Font.Color = 255`).
   - Read the width after that step: whether a colour set through COM widens is question 6's.
     If it widened, set `UseStandardWidth = True` and check `########`.
   - Then Ctrl+1, the Font tab, the Normal font box checked, OK.
   - ExSheet widens: a reading.
3. **The Font tab shown and OK pressed with nothing changed.**
   - ExSheet sets nothing and widens nothing. R6 saw the Fill and Alignment tabs do the same.
   - If Excel widens here, OK on the Font tab widens whatever is touched, which needs the user.
4. **The Alignment tab's Horizontal: Center**, then OK.
   - ExSheet widens nothing: a reading. R6 asked only the vertical.
5. **The Home tab's Font Color button**, pressed on its face. A new Excel's face is red. Then its
   arrow, and Blue from the list, in a workbook of its own.
   - ExSheet's Font Colour widens nothing, face or list, as `SetCellFormatAsync` does not for a
     colour: a reading.
   - If Excel's widens, the user decides whether the face follows the button or the method.
6. **Through COM, one property each**, in a workbook each: `Range.Font.Italic = True`,
   `Range.Font.Strikethrough = True`, `Range.Font.Color = 255`.
   - R6 asked Bold and Underline. This completes `Range.Font` beside the decision on
     `SetCellFormatAsync`.
   - ExSheet's `SetCellFormatAsync`: italic and a colour widen nothing, and strikethrough widens: a
     reading. If Excel's differs, the user decides whether the method follows.
7. **Ctrl+U taking underline off.** A1 is underlined first through COM (`Font.Underline = 2`).
   - R6 saw that COM step widen. Set `UseStandardWidth = True` and check `########` before the key.
   - Then Ctrl+U. ExSheet widens: a reading.
   - The same for Ctrl+5 taking strikethrough off, with `Font.Strikethrough = True` first.
8. **Ctrl+4** over `########`.
   - ExSheet widens, as for Ctrl+U: a reading.
9. **Ctrl+I, then Ctrl+U, in one workbook**: the width after each.
   - ExSheet: no change after Ctrl+I, widened after Ctrl+U.
10. **Over two ranges**: A1 and C1 each hold the number. Select A1, Ctrl+click C1, then the Font tab
    with Bold, OK.
    - ExSheet widens both columns, in one undo step: a reading.
    - Then the same over column A selected from its Heading.
11. **The Border keys and the Border tab over `########`**, each in a workbook of its own.
    - Ctrl+Shift+& (an outline).
    - Ctrl+Shift+_ (no borders), over A1 whose four edges were first set thin through COM.
    - Format Cells' Border tab, Outline, OK. The fourteenth run's case 9 was over a General number
      showing `1234568`, not `####`.
    - ExSheet widens nothing for any of the three: a reading.
    - If Excel widens for any, the user decides whether Borders widen as that gesture does.
