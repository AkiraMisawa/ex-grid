Product: ExGrid
Kind: Added
ADR: 0240

A paste now says it is under way from its start until it lands or is refused: the root carries `aria-busy`, a bar across its top edge and a progress pointer paint it (`--ex-pasting-bar`, `--ex-pasting-bar-height`, `--ex-pasting-cursor`), and the live region says so once. Escape pressed meanwhile cancels the paste: before its Edit Intent is raised it is refused with the new reason `PasteRefusalReason.Cancelled`, and after, it stands and the grid says so. A Consumer that switches over `PasteRefusalReason` without a default case gains one value to handle.
