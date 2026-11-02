# @ex-grid/chart

ExChart in TypeScript: its layout — scales, ticks, Thinning, Series Kind steps and gaps, the scene — what its tooltip, its summary and its files say, the fits and interpolations a Consumer hands in, and its Script Core, the chart drawn as SVG into an element, free of any UI framework.

It is one of the packages of the ExGrid family: Excel-like grids, sheets, pivot tables and charts for reading
money and risk numbers, published to npm under `@ex-grid` and to NuGet for Blazor, at one shared version.

## Install

```sh
npm install @ex-grid/chart
```

It depends on exactly the version of `@ex-grid/core` and `@ex-grid/data` it was built with, so upgrade the family's packages together.

What it draws reads these stylesheets. Import them once in your application:

```ts
import '@ex-grid/core/styles.css';
import '@ex-grid/chart/styles.css';
```

## Documentation

[ExChart on the documentation site](https://akiramisawa.github.io/ex-grid/exchart), read for React with the switch at the top, and every
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
