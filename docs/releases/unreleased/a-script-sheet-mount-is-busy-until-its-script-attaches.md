Product: ExSheet
Kind: Behaviour
ADR: 0033
Docs: exsheet

Blazor's Script `ExSheet` marks the element its script mounts on `aria-busy="true"` and `data-ex-mounting="sheet"` from its first render until the script has attached, which lifts both; a refused script's root carries neither. Assistive technology hears the Sheet as loading instead of as an empty box, and a page or test holding several Sheets can tell which are still to come: they attach in no set order (A11Y-20).
