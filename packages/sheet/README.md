# @ex-grid/sheet

ExSheet's Script Core: a general-purpose Excel-like sheet drawn by ExGrid as its Consumer, over @ex-grid/sheet-engine, with its words, Format Cells, the Name Box, Excel's keys and Pointing, free of any UI framework.

It is one of the packages of the ExGrid family: Excel-like grids, sheets, pivot tables and charts for reading
money and risk numbers, published to npm under `@ex-grid` and to NuGet for Blazor, at one shared version.

## Install

```sh
npm install @ex-grid/sheet
```

It depends on exactly the version of `@ex-grid/core` and `@ex-grid/sheet-engine` it was built with, so upgrade the family's packages together.

What it draws reads these stylesheets. Import them once in your application:

```ts
import '@ex-grid/core/styles.css';
import '@ex-grid/sheet/styles.css';
```

## Documentation

[ExSheet on the documentation site](https://akiramisawa.github.io/ex-grid/exsheet), read for React with the switch at the top, and every
version's [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes).

> **The API may still change.** While the version is `0.x`, it may change from one version to the next; each
> version's Release Notes say what changed. A prerelease, a version with a suffix such as `-rc.1`, passed every
> test layer in CI on the commit it was built from. A version without a suffix is published only once the
> project's [Definition of Done](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/definition-of-done.md) is
> signed off on it.

## Requirements

- **Chrome or Edge.** They are the supported browsers; Safari and Firefox are out of scope.

## License

MIT
