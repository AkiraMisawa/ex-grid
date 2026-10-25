Product: ExPivot
Kind: Behaviour
ADR: 0153

`@ex-grid/pivot-engine`'s `LocalPivotReportSource`, over a `SnapshotPivotSource`, now brings a Change Batch into the report from what it touched: the leaves it moved, the totals above them and the rows that show them, as the C# source does (ADR-0153). A Window after one record changed among a hundred thousand takes about 30 ms where it took about 250 ms, and equals the report a fresh source makes. A Window cancelled after its batches began to be folded in now completes, because that work is carried through; a cancellation is heard before it, and while a new layout is laid out.
