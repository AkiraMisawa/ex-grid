Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#editing-while-values-change
ADR: 0142

One commit at a time: while your `OnEdit` handler runs, the editor stands, and a press made meanwhile waits for your answer and is then answered against what it left, so one commit raises one Edit Intent (#70).
