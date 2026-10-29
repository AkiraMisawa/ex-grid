Product: ExGrid
Kind: Added
ADR: 0020

A Blazor column can be declared not queryable, `new GridColumn<TRow>(...) { IsQueryable = false }`, as a React column says `queryable: false`: a header click sorts nothing and its menu offers its commands only. It is for a column the source cannot order or filter by, such as a server that does neither.
