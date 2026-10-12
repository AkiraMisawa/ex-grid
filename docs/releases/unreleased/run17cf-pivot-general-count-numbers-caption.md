Product: ExPivot
Kind: Behaviour
Docs: expivot/values#aggregations
ADR: 0060

A Count Numbers Value Field is captioned as Excel captions it, as Count is: `Count of <field>`, where it read `Count Numbers of <field>`, and in the bundled Japanese words `データの個数 / <field>`, where they read `数値の個数 / <field>`. Beside a Count of the same field it takes the next free number, `Count of Amount2`, as any repeated caption does. A Consumer that wants the old caption replaces the word `caption-count-numbers` through `Label`.
