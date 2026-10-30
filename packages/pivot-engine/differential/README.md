# Differential runs

The Script Core's engine held to the C# engine on random inputs (ADR-0370, section 2), where the shared
cases (`tests/cases/pivot/`, `tests/cases/README.md`) hold it on chosen ones. Each run draws its inputs
from a seed, asks `dotnet/tools/pivot-oracle` what .NET and `ExPivot.Engine` answer, and fails on the first
difference it lists, so a failing seed is a failing case to write.

| File | Holds |
|---|---|
| `number-text.differential.ts` | .NET's number formats, standard and custom, under every culture the engine bundles |
| `date-text.differential.ts` | .NET's date formats likewise |
| `general.differential.ts` | General, a number with no format of its own as a PivotTable shows it (ADR-0060), from `PivotGeneral` |
| `text.differential.ts` | Comparing and casing text as .NET's cultures do |
| `report.differential.ts` | Whole reports: random data, a Snapshot of every kind of column or records of any kind, and random layouts over it, each asked as a `pivotSource` case — its Leaf Aggregates, the report with every reading, a field's Items — and answered on the C# side by the shared cases' own runner, which the oracle compiles |
| `live.differential.ts` | Live data: a random keyed Snapshot, a random question, and a run of random Change Batches, each followed by the question again and a field's Items under the current version, read in any order, since after a batch the two engines meet the records in different orders; the parts are those no order changes (counts, sums and extremes over numbers every order sums exactly) |

## Running one

```sh
nix develop -c bash -c 'cd dotnet && dotnet build tools/pivot-oracle'
cd packages/pivot-engine
EXGRID_PIVOT_ORACLE="dotnet ../../dotnet/tools/pivot-oracle/bin/Debug/net10.0/ExPivot.Oracle.dll serve" \
  nix develop .#browser -c npm run differential
```

`EXGRID_PIVOT_ORACLE` is the command that starts the oracle's `serve`, which answers one JSON request a
line. It runs on the flake's .NET, in CI too: the cultures' conventions are ICU's, and the culture table was
written from the flake's ICU, so a .NET on another ICU — a runner's own — differs where CLDR changed between
the two (en-ZA's decimal separator) and fails the run on data, not on the engine. A run prints its seed in the test's name; `EXGRID_PIVOT_SEED` repeats it. `EXGRID_PIVOT_DIFF_OUT`
names a file `report.differential.ts` writes each differing request to, a line each, as the oracle took it:
the `case` in it is a `pivotSource` case, which goes into `tests/cases/pivot/` once its data is cut down to
what makes the difference.

A random value is written as JSON keeps it: a `-0` the generator makes is `{"double": "-0"}`, since
`JSON.stringify` writes it as `0` and the oracle would be asked about another number.

The oracle also writes the tables the engine reads .NET's conventions from: `cultures`
(`src/culture/culture-table.ts`), `kana` (`src/text/tailored-ranks.ts`), `casing` (`src/text/upper-table.ts`,
.NET's simple upper case, which no JavaScript engine's own matches in every version) and `words`
(`src/words/word-tables.ts`), each with `dotnet run --project tools/pivot-oracle -- <command> <file>`, run in `dotnet/`.
