Product: ExGrid
Kind: Fixed
Docs: exgrid/columns#declaring-columns
ADR: 0090

A number or a date that fits its column is painted whole where the column is drawn a Device Pixel narrower than declared: such a cell may use one Device Pixel of its right padding, where a bold value just under its edge was cut (VZ-19, #73).
