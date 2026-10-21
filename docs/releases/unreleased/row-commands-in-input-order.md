Product: ExGrid
Kind: Behaviour
ADR: 0240, 0160, 0142, 0043
Docs: exgrid/columns#mark--template-and-action-columns

Row Action and row Mark clicks now wait for earlier input, including asynchronous Consumer callbacks, and recovery of a discarded Blazor click uses the same input number. While waiting, a command follows its row only while continuously present in the Window; leaving it causes a visible refusal even if the row returns, and Consumers handling `OnMarkRefused` should handle the new `RowLeftTheWindow` reason (Source replacement still reports `SourceChanged`).
