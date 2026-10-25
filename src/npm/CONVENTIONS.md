# TypeScript conventions

How the code of this workspace is written. The spine of the design in [AGENTS.md](../../AGENTS.md)
holds here as it does in C#; this page says how it is spelt in TypeScript, and which of it a tool
holds you to. A rule marked **(lint)** or **(tsc)** fails `npm run lint` or `npm run typecheck`; the
rest is for review.

## No quiet fallback

The spine's first rule is *rather than be quietly wrong, say it cannot be done*. Its most common
violation in TypeScript is code that carries on when something is missing or has failed, with a value
nobody chose: `?? 0`, `|| ''`, `!`, `catch { return [] }`. Each looks reasonable on its own line and
each turns a defect into a figure on screen that looks right. This is also the shape of code a model
writes to make an error go away, so the workspace refuses it by tool rather than by review.

| Do not | Do instead |
|---|---|
| `list[i] ?? 0`, `map.get(k) \|\| ''`, `x ?? []` **(lint)** | Decide whether the value can be absent. If it cannot, read it with `elementAt` or `defined` (`core/src/must.ts`), which throw. If it can, the absence is part of the contract: a parameter or property default where the signature shows it, or `undefined` returned and documented |
| `list[i]!`, `value!` **(lint)** | `elementAt(list, i)`, `defined(value, 'what it is')`. A `!` reads on with `undefined` where C# would have thrown |
| `catch { … }` or `catch (e) { return fallback }`, on any path **(lint)** | Let the error propagate. A catch that adds context ends in `throw` (`throw new Error('…', { cause })`), returns nothing on the way, and passes |
| `promise.catch(() => x)`, `promise.then(f, () => x)` **(lint)** | Let the rejection propagate to whoever awaits it |
| `if (count)`, `if (name)`, `a \|\| b` on numbers or strings **(lint)** | Say the comparison: `count > 0`, `name !== undefined`, `name !== ''`. 0, `''` and `NaN` are values, not absences |
| A `default:` that returns something plausible, or a switch that falls out as `undefined` **(lint)** | Take every member of the union, and end with `default: return unreachable(value)` |
| `any`, `as T` that narrows, `@ts-ignore` **(lint)** | `unknown` and a check that narrows it. `@ts-expect-error` only with a description |
| `() => {}` as a handler that does nothing **(lint)** | Make the callback optional and do not call it, or say in a named function why nothing happens |
| A test runner that defaults a key a case left out | Read it with a reader that throws (`wholeNumber` in `core/test/cases.ts`). Every case calls `expect.hasAssertions()` |

**A refusal says which kind it is,** so the C# core and the Script Core refuse alike in the shared cases
(`tests/cases/README.md`):

| Thrown | When | C# counterpart |
|---|---|---|
| `RangeError` | An argument outside what the grid has: a position past the extent, a negative count | `ArgumentOutOfRangeException` |
| `TypeError` | An argument of the wrong kind, which only an untyped caller can pass | `ArgumentException` |
| `Error` | An operation the state does not allow: a selection that no longer fits | `InvalidOperationException` |
| `Error('Internal: …')` | A broken invariant of the core itself: a defect, never an answer | `InvalidOperationException` |

**Check at the boundary, trust the types inside.** Values arrive untyped where the core meets the outside:
an adapter's props from JavaScript, JSON, a DOM event, the clipboard. Check them there and refuse what
does not hold. Inside the core the type checker has already proved what a re-check would test, and
`no-unnecessary-condition` **(lint)** says so; a guard it calls unnecessary is removed, not silenced.

**An exception is one line, with its reason.** Where a rule is wrong for one line, switch it off for that
line and say why after ` -- `, naming the ADR that decides it where one does:

```ts
// oxlint-disable-next-line ex-grid/no-stand-in-default -- ADR-0123: an empty cell copies as ''
```

A disable without a reason, or for a whole file or block, fails **(lint)**, and so does one that no
longer suppresses anything. A reviewer reads every one.

## Shape of the code

- **The public surface is what `src/index.ts` exports**, held to `etc/<package>.api.md` by API Extractor,
  with a doc comment on every member (`README.md`). It is read-only data and interfaces that read;
  transitions stay inside the package.
- **Immutable base plus a thin diff** (spine 2): fields are `readonly`, data handed out is frozen, and an
  operation returns a new value rather than changing one. `Array#sort` and the other mutating methods
  are refused **(lint)** in favour of `toSorted` and its kin.
- **A value with an invariant is a class** with `#private` readonly fields, made only through what checks
  what it is given: a public constructor taking an options object (`new ViewportGeometry({ … })`) where
  any caller may make one, or a private constructor behind static factories and transitions where only
  the class may make its states (`Selection`). A derived value made on first asking may be kept in a
  private field, the one field written after construction. Stateless helpers are functions.
- **Only syntax TypeScript erases** **(tsc, `erasableSyntaxOnly`)**: no `enum`, `namespace` or parameter
  properties. A closed set of names is a union of string literals (`'up' | 'down' | 'left' | 'right'`).
- **Names are the glossary's** (`CONTEXT.md`): Focus, Extent, Overlay, Window. Types and classes are
  PascalCase, everything else camelCase, files kebab-case, interfaces without an `I`.
- **Comments say why, and cite the ADR**, as the C# does. A comment that restates the line is removed.
- **A library does not talk**: no `console` **(lint)**, no globals, nothing at module load but
  declarations (`sideEffects: false`).
- **Formatting follows the code around it**: four spaces, single quotes, semicolons, lines up to about
  120 columns. No formatter is wired in yet.

## The compiler and the lint

`tsconfig.base.json` turns on, beyond `strict`: `noUncheckedIndexedAccess` (an indexed read may miss),
`exactOptionalPropertyTypes` (an optional property is absent, not `undefined`), `noImplicitReturns`,
`noPropertyAccessFromIndexSignature`, `noUnusedLocals`, `noUnusedParameters`, `erasableSyntaxOnly`, and
refuses unreachable code and unused labels.

`npm run lint` runs [oxlint](https://oxc.rs) with type information (`oxlint-tsgolint`, which is built on
the same TypeScript 7 as `tsc`), configured in `.oxlintrc.json`. Every rule is an error. The workspace's
own rules live in `lint/ex-grid.mjs`:

| Rule | Refuses |
|---|---|
| `ex-grid/no-stand-in-default` | `??`, `\|\|`, `??=`, `\|\|=` whose right side is a literal, `undefined`, `[]` or `{}` |
| `ex-grid/no-catch` | A `catch` clause that does not end in `throw` or returns before it, every `.catch(…)`, and `.then(…, onRejected)` |
| `ex-grid/disable-states-why` | A disable comment without ` -- <why>`, or one that covers more than a line |

**The lint is held to its own fixtures first.** `lint/fixtures/` holds code that breaks each rule on
purpose, each line marked with what it must draw. `lint/check.mjs` lints them before the workspace and
fails if any rule stopped firing or a new one started, so an upgrade, a renamed rule or a misspelt
setting cannot turn the lint quietly green. A new rule gets a fixture line in the same change.
