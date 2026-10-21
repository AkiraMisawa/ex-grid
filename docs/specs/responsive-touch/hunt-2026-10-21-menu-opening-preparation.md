# A closing-menu regression held the opening's keyboard hand-off

Status: ready-for-agent
Roadmap: internal

Observed on `430cc823` in normal CI
[39521503735, job 118967898037](https://github.com/AkiraMisawa/ex-grid/actions/runs/39521503735/job/118967898037),
Chrome Server, MudBlazor: `popovers.spec.mjs`, "a key typed while a command pressed in a menu
closes it is the grid's" (ADR-0044).

The command ran, the menu closed, and ArrowDown moved the Focus to the expected cell. The
remaining assertion failed: the native key had landed on the grid rather than the popover.
That assertion is necessary to prove the test actually exercises a key held during closing.

The trace shows `data-ex-focus-owed`, without the Settled Mark, immediately after the menu's
visibility assertion. The test then held Server answers before the opening had finished its
keyboard hand-off. Holding answers at that point does not establish the intended starting
state of an open menu holding the keyboard.

On the integration of main `bdea73f4` (`f9a283df`), the unmodified test reproduced the same
failure in all six executions: Built-in and MudBlazor, three repetitions each, Chrome Server,
150 ms round-trip floor and fourfold CPU throttling. Changing only the preparation to await
the existing Settled Mark before holding answers made all six pass under the same settings.
The command press and ArrowDown still run consecutively at machine speed. All original
assertions, including the native destination being the popover, remain unchanged.

The existing test is the regression signal. No product behavior changes, so no Docs Site or
Change Note is needed. Normal CI and the related-input gate remain pending; this record is
not closed by a successful rerun of the unmodified test.
