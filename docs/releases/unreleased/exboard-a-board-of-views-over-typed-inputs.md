Product: ExBoard
Kind: Added
ADR: 0340

ExBoard ships as a package of its own: a Board, JSON an AI composes once and a workflow runs without AI, laid out as rows of ExGrid, ExPivot and ExChart views over typed Board Inputs. `BoardSpec.Read` reads the Board's JSON, a public surface from this version, and refuses what it cannot draw by its path; `<ExBoard>` draws it, with links, outputs on a confirm and the user's changes as a Board Diff; `BoardSpec.Tool` and `BoardSpec.Answer` are the `draw_board` tool. ExBoard takes the products' Chromes, so it has no MudBlazor Wrapper.
