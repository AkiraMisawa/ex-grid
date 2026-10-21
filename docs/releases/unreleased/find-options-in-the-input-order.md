Product: ExGrid
Kind: Breaking
ADR: 0240

A tick of the find panel's Match case or Match entire cell contents is now a click taken in its place in the Input Order, under the built-in Chrome and `MudGridChrome` alike: a step clicked straight after a tick searches with the option ticked, and one clicked before it with the option as it stood, at machine speed and over a round trip. `FindContext.MatchCaseChanged` and `WholeCellChanged` are now `Func<bool, Task>` where they were `Action<bool>`, so a Chrome of your own that draws the find panel returns the task from its option's change handler (`ValueChanged="Context.MatchCaseChanged"` binds as it did), and puts `data-ex-claims="click"` on the checkbox itself, not on a label around it.
