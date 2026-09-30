# 12: The component asks the source

Status: ready-for-agent

**What to build:** `ExPivot` takes a `Source` instead of records (ADR-0058, ADR-0065).

- **While a question is out:** the Field List shows the new layout; the report stays as it was,
  under `IsLoading`; a further change cancels the question; and an answer to a superseded question
  is discarded.
- **A change that needs no new question asks none.**
- **Caps:** a layout that breaks one is refused by name, and the report stays on the layout before.
- **Defer Layout Update:** a checkbox and an Update button at the pane's foot.
- **Value Field Settings…** disables the Aggregations the source does not offer, giving the reason.
- **A refused Source Version** makes ExPivot say that the data has changed.

**Blocked by:** 10

- [ ] PV-25, PV-26 with a source that answers on demand and counts questions
- [ ] PV-28: Defer Layout Update
- [ ] PV-29: the caps, in the component
- [ ] PV-24: the unsupported Aggregations disabled, with the reason
- [ ] PV-23: the component's side
- [ ] PV-2, PV-5, PV-8, PV-9, PV-11, PV-13, PV-15, PV-17 still green

## Comments
