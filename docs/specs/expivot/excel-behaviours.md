# Excel's behaviours, read for ExPivot and not yet observed

Status: ready-for-human

ExPivot's first version answers as Excel's PivotTable is documented to answer (ADR-0060/0061). None
of it has yet been run beside Excel. Each line below is a **reading**: what ExPivot does now, which
the implementation and its tests pin. A run on a Windows machine with Excel settles each one — as
ExSheet's `verify-on-windows.md` runs settled its readings — and a reading the run contradicts
changes the ADR first, then the code, then the test. Until then, §29's "as Excel" means "as read
here".

The same run drives Excel as `docs/specs/exsheet/excel-behaviours.md` describes: COM where the
object model has the gesture's meaning (`PivotTable.AddFields`, `PivotField.Orientation`,
`PivotItem.Visible`, `PivotField.ShowDetail`, `PivotTable.RowAxisLayout`, reading
`TableRange1`), real keys and the mouse where it does not, and the user only as a last resort. It
builds the same records in a worksheet, the same layout in both, and compares the report cell by
cell.

## The report (ADR-0060)

1. **Item order.** Ascending: numbers, then dates, then text (compared under the culture, ignoring
   case), then FALSE and TRUE, then errors; `(blank)` last. Descending reverses the whole order —
   within each group and of the groups — and `(blank)` stays last. *ExPivot keeps dates apart from
   numbers only because a Pivot Field is declared with one type; Excel's dates are numbers.*
2. **Text Items ignore case**, and the first spelling in the records labels the Item.
3. **A missing value is `(blank)`**, is an Item like any other, and is counted by Count.
4. **Count** counts every non-empty value, text included; **Count Numbers** counts numbers only.
5. **Average, StdDev, Var** over no numbers are `#DIV/0!`; StdDev and Var over one number are
   `#DIV/0!`; StdDevp and Varp over one number are 0.
6. **Product over no numbers.** ExPivot shows an empty cell where there are no values, and Product
   over values none of which is a number shows 0. *Unverified in both halves.*
7. **An empty cell stays empty**, never 0, unless "For empty cells show" is set — which ExPivot
   does not offer.
8. **A subtotal and a grand total are aggregated from the records**, not from the rows above them:
   an Average's total is the average of every record, and a Count's grand total counts records.
9. **Compact form**: the one label column headed "Row Labels", each level indented one step, the
   subtotal on the group's own row when subtotals are at the top. **Outline**: one label column per
   row field, headed by the field's caption. **Tabular**: the same columns, the subtotal row at the
   bottom, captioned "East Total".
10. **Subtotals default to the top** in Compact and Outline; in Tabular they are always at the
    bottom, where Excel greys the choice out.
11. **A collapsed Item** shows its subtotal on its own row, its children gone.
12. **The column Items** are header bands over the value columns, a column field's total captioned
    "FALSE Total", and "Grand Total" on the right.
13. **Σ Values**: appears in Columns when the second Value Field is placed; stands innermost; the
    headers read "Sum of Amount" under each column Item.
14. **Captions**: "Sum of Amount", "Count of Region", and a second Value Field of the same field
    and Aggregation "Sum of Amount2". A caption equal to a field's caption or another Value Field's
    is refused with "PivotTable field name already exists." — compared ignoring case.
15. **Show Values As** "% of Grand Total", "% of Column Total", "% of Row Total" in the format
    `0.00%`, and a total's share of itself 100.00%.
16. **General** writes at most 15 significant digits, as a cell does.
17. **Hidden Items are left out of the totals** (Excel's "Include filtered items in totals" off,
    its default for a non-OLAP pivot).
18. **Sort by value**: a field's Items ordered by the Value Field's value at each Item's total
    across the other axis, as shown; an Item whose value is empty or an error comes last, ties go
    in ascending label order, and `(blank)` is ordered by its value like any other Item.

## The Field List (ADR-0061)

19. **Ticking a field** puts a number field at the end of Values as a Sum and anything else at the
    end of Rows. *Excel decides by the data — a column with any text or blank is counted — where
    ExPivot decides by the declared type.* A field dropped on Values from the list is a Sum when it
    is a number and a Count otherwise.
20. **Unticking** removes the field from every Area, each of its Value Fields included.
21. **A field dragged from the list of fields** onto Filters, Rows or Columns stands there,
    moving out of whichever of them it stood in, with its settings (its Hidden Items, its sort, its
    subtotals); onto Values it becomes a new Value Field and stays wherever else it stands. **An
    entry dragged** from Filters, Rows or Columns onto Values leaves its Area; a Value Field dragged
    out of Values leaves Values.
22. **Σ Values** moves only between Rows and Columns, and leaves when the second-to-last Value Field
    does.
23. **A field's menu** lists Move Up, Move Down, Move to Beginning, Move to End, Move to Report
    Filter, Move to Row Labels, Move to Column Labels, Move to Values, Remove Field and Field
    Settings… (Value Field Settings… in Values), and in ExPivot also Sort, Filter… and Expand /
    Collapse Entire Field, which Excel keeps in the report's own menus. A command that would change
    nothing is disabled.
24. **The report filter band** shows `(All)`, the one Item shown, or `(Multiple Items)`.
25. **A double click** on a value cell shows its records (Excel puts them on a new sheet; ExPivot
    hands them to the application); on an outer Item's label it expands or collapses it; on the
    innermost field's label ExPivot does nothing, where Excel asks which field to show the detail
    by — an Excel command the first version leaves out.

## Added by the grilling (ADR-0060, ADR-0061)

26. **Date parts** are labelled `2026`, `Qtr3` and `Sep` in the English edition and `2026年`,
    `第3四半期` and `9月` in the Japanese one, and ordered by the calendar.
27. **The Japanese edition's words**: `行ラベル`, `列ラベル`, `総計`, `集計`, `合計 / 金額`,
    `データの個数 / 地域`, `(空白)`, `(すべて)`, `(複数のアイテム)`, `値`, and the pane's
    `ピボットテーブルのフィールド`, each read against Excel's own screens.
28. **The Design tab's Layout choices** carry these names: Subtotals (Do Not Show Subtotals, Show all
    Subtotals at Bottom of Group, Show all Subtotals at Top of Group), Grand Totals (Off for Rows and
    Columns, On for Rows and Columns, On for Rows Only, On for Columns Only), and Report Layout (Show
    in Compact Form, Show in Outline Form, Show in Tabular Form, Repeat All Item Labels, Do Not Repeat
    Item Labels). The choices that would change nothing are greyed out, as in Tabular's Subtotals
    position.

## Comments
