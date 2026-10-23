Product: ExPivot
Kind: Added
ADR: 0240
Docs: expivot/layout#drawing-a-part-yourself

Every Pivot Chrome context — the Field List, the menus, Filter…, Field Settings…, Value Field Settings…, the report filter band, the Pivot Toolbar, the Details tabs and dialog, and the Stale Report's notice — carries `InOrder`, the pivot's Input Order, and `TakeKeyboard`, ExPivot's way for a control to take the keyboard. A Chrome of your own marks a control's events with `data-ex-claims` and hands its work to `InOrder` so it is handled in its place among the report's keys and presses, and takes the keyboard through `TakeKeyboard` rather than its own `FocusAsync`.
