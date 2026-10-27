Product: ExGrid
Kind: Added
ADR: 0370, 0390

ExGrid for React's built-in filter panel now says each of its words by an id — its buttons, the value list's, the operators' names and why a typed value does not read — so `commandLabel` replaces them as it replaces the menus', and a filter panel slot is handed the same words through `context.label`. `japaneseGridWords` is the Japanese, Excel's where Excel has the word, for every word the built-in Chrome shows, handed to `commandLabel` in one line; the words never follow `locale`. It is published with 0.3.0.
