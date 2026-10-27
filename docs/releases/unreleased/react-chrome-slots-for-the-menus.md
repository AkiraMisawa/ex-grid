Product: ExGrid
Kind: Added
ADR: 0350, 0390

ExGrid for React takes a `chrome` prop: the column menu and the Context Menu can be drawn by a design system of your own, as a render function of what the grid hands each menu, while the commands, their order, which are enabled and what every key means stay the grid's. `useMenuItems` gives each item the props that carry its keys, its click and its focus to the grid; a menu not given is drawn by the built-in Chrome. The Script Core takes the same slots as mount functions, `(slot, context) => cleanup`. It is published with 0.3.0.
