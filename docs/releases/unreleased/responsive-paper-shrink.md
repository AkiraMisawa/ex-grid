Product: ExGrid
Kind: Fixed
Docs: exgrid#a-responsive-box
ADR: 0045

A MudExGridPaper in a flex row or CSS grid track can shrink with its container when its grid uses ViewportSize.Stretch. Column widths and saved pinning remain unchanged; declared pixel widths are still honoured. The same paper can host ExSheet and ExPivot. This change does not claim complete mobile touch support.
