Product: ExBoard
Kind: Added
ADR: 0370

`@ex-grid/board` reads a Board in TypeScript, refusing what ExBoard's C# reader refuses, at the same path and in the same words, and binds it to Snapshots: a grid built from its input's shape, links that narrow the views they reach, and outputs raised only by a confirm.
