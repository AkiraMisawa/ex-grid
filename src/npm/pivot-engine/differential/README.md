# Differential runs

The Script Core's engine held to the C# engine on random inputs (ADR-0370, section 2), where the shared
cases (`tests/cases/pivot/`, `tests/cases/README.md`) hold it on chosen ones. Each run draws its inputs
from a seed, asks `tools/pivot-oracle` what .NET and `ExPivot.Engine` answer, and fails on the first
difference it lists, so a failing seed is a failing case to write.

| File | Holds |
|---|---|
| `number-text.differential.ts` | .NET's number formats, standard and custom, under every culture the engine bundles |
| `date-text.differential.ts` | .NET's date formats likewise |
| `general.differential.ts` | General, a number with no format of its own as a PivotTable shows it (ADR-0060), from `PivotGeneral` |
| `text.differential.ts` | Comparing and casing text as .NET's cultures do |
| `report.differential.ts` | Whole reports: random data, a Snapshot of every kind of column or records of any kind, and random layouts over it, each asked as a `pivotSource` case — its Leaf Aggregates, the report with every reading, a field's Items — and answered on the C# side by the shared cases' own runner, which the oracle compiles |

## Running one

```sh
nix develop -c dotnet build tools/pivot-oracle
cd src/npm/pivot-engine
EXGRID_PIVOT_ORACLE="dotnet ../../../tools/pivot-oracle/bin/Debug/net10.0/ExPivot.Oracle.dll serve" \
  nix develop .#browser -c npm run differential
```

`EXGRID_PIVOT_ORACLE` is the command that starts the oracle's `serve`, which answers one JSON request a
line. A run prints its seed in the test's name; `EXGRID_PIVOT_SEED` repeats it. `EXGRID_PIVOT_DIFF_OUT`
names a file `report.differential.ts` writes each differing request to, a line each, as the oracle took it:
the `case` in it is a `pivotSource` case, which goes into `tests/cases/pivot/` once its data is cut down to
what makes the difference.

A random value is written as JSON keeps it: a `-0` the generator makes is `{"double": "-0"}`, since
`JSON.stringify` writes it as `0` and the oracle would be asked about another number.

The oracle also writes the tables the engine reads .NET's conventions from: `cultures`
(`src/culture/culture-table.ts`), `kana` (`src/text/tailored-ranks.ts`) and `words`
(`src/words/word-tables.ts`), each with `dotnet run --project tools/pivot-oracle -- <command> <file>`.
