Product: ExGrid
Kind: Behaviour
Docs: exgrid/data#a-server--a-page-at-a-time
ADR: 0207

Two `GridQuery` values are equal when they ask for the same range, sort and filter, compared by what their lists and filter hold rather than by which lists hold it, so a query read back through `GridViewStateJson.ReadQuery` equals the one written. Before, two queries built from separate lists were never equal; a cache keyed by a `GridQuery` now finds a repeated ask.
