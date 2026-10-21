# 11: A pivot's cell links

Status: ready-for-agent
Roadmap: internal

ADR-0340 (Q5): a selection of a pivot's cells narrows the views a link from the pivot reaches, by the
items each selected cell stands for in the link's `on` fields: a cell under a subtotal stands for every
item below it, so it constrains only the fields its row and column paths name, and a grand total
constrains none. ExPivot says which items a selected cell stands for (a public reading of the report's
row and column paths at a position, as Show Details already reads them inside ExPivot). The reader
then accepts a link from a pivot whose `on` fields the pivot places in rows or columns, and refuses one
on a field it places as a value or a filter.
