# The eighteenth Windows run: ExPivot's General and its captions

Questions gathered from `claude/run17cf-pivot-general` (the seventeenth run's Part E, items 4 and 16, and the readings
of 2026-10-12, R3, with the English (US) record of that day) for the next Windows run, to be folded into its spec when
it is written. An answer that disagrees with what ExPivot does comes back to the user.

Each is asked of Excel's PivotTable on Windows, in the English (United States) edition unless it says otherwise, the
cells and captions read through COM and from a picture.

1. **A negative value in General.** Average of `-1`, `-1` and `0` over three records (`-0.666666666666667`). Is it
   `-0.666666667` (the sign not one of the eleven characters, as ExPivot reads it) or `-0.66666667`? And under the
   regional format sv-SE, is the sign `-` or U+2212 `−`? (ExPivot writes `-` in every culture, by generalisation from
   ExSheet's General.)
2. **The status bar over a General pivot cell.** Select the StdDev cell holding √2 alone. Is Sum `1.414213562`,
   `1.414213562373`, or `1.4142135623731`? (ExPivot and ExSheet write the status bar's figures `G15`, ADR-0130.)
3. **The number in the Japanese edition.** A Count and a Count Numbers of one field. Is the second
   `データの個数 / 値2`, as the English edition numbers it?
4. **Value Field Settings' Custom Name beside a Count.** On a PivotTable with Count of Amount and Sum of Amount, choose
   Count Numbers for the Sum. Does the box show `Count of Amount` or `Count of Amount2`? (ExPivot's box shows the bare
   default, and OK numbers it.)
5. **A Custom Name another Value Field's default would take.** Type `Count of Amount` as the Custom Name of a Sum, then
   add a Count of Amount. Is the new one `Count of Amount2`? (ExPivot: yes, by its own rule.)
6. **The Japanese edition's Aggregation list after 数値の個数.** R3 (d) read it as far as `数値の個数, …`; the names of
   StdDev, StdDevp, Var and Varp were not read.
7. **A field's own number format under sv-SE, fi-FI or nb-NO.** A Value Field in `#,##0.00` over `-1234.567`: is its
   minus sign `-` or U+2212 `−`? (ExPivot writes what .NET's format strings write, U+2212 under those cultures; only
   General's sign follows ExSheet's. Noted in the branch's proposal as not asked.)
8. **The rest of ExPivot's General that no run read** (left at 15 significant digits until a run reads them,
   ADR-0060's note of 2026-10-12): Show Details' sheet over records whose field has no number format (√2 as a record's
   value), and the sample in Value Field Settings' Number Format with General chosen.
