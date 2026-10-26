Product: ExPivot
Kind: Fixed
ADR: 0005, 0060, 0152

ExPivot drawn by the Script Core (`@ex-grid/pivot`, `@ex-grid/pivot-react`) now answers a copy from its report source, as the Blazor component does. Copy with headers writes a number column Item's header in full into the HTML Excel reads (`123456789012.345`), where it wrote the painted `1.23457E+11`; a copy asked under an order the report has since moved past is refused in ExPivot's words.
