Product: ExPivot
Kind: Behaviour
ADR: 0033
Docs: expivot

Blazor's Script `ExPivot` marks the element its script mounts on `aria-busy="true"` and `data-ex-mounting="pivot"` from its first render until the script has attached, which lifts both; a refused script's root carries neither. Assistive technology hears the report as loading instead of as an empty box, and a page or test holding several can tell which are still to come: they attach in no set order (A11Y-20).
