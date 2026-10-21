Product: ExGrid
Kind: Added
ADR: 0240
Docs: exgrid/appearance

`HandKeyboardToAsync(control, preventScroll)` hands the keyboard to a control of yours as `HandKeyboardToAsync(control)` does, with the control taking it where it stands: nothing scrolls to bring it into view. ExSheet's Sheet Tabs use it, so the arrow keys on the tab list move along the tabs without scrolling the page, as they did before the tabs joined the Input Order.
