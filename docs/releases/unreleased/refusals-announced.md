Product: ExGrid
Kind: Behaviour
Docs: exgrid/clipboard#what-a-refusal-sounds-like
ADR: 0203

A refused copy, paste, fill or Delete is now announced once in the grid's live region, in English words that say what to do next, under both Chromes; a page that also announces a refusal itself from `OnPasteRefused` or `OnCopyRefused` will be heard twice. Replace the words through `CommandLabel` (or `MudGridChrome.Label`) by the ids in `RefusalLabelIds`, or answer the empty string to silence one and say it yourself.
