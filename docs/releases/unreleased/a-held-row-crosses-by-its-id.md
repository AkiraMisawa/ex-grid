Product: ExGrid
Kind: Behaviour
ADR: 0350

Blazor's ExGrid no longer reads and sends again the values of a row the browser already holds: a row whose instance is the one sent before, under the same columns and source, crosses as its id and what it paints (its Cell States, Row Kind, mark, appearance and change times), which are still asked each time. A row rewritten in place therefore does not repaint, as ADR-0003 always said and as 0.1.0 did; replace a changed row with a new instance.
