# @ex-grid/board-react

ExBoard for React: the Framework Adapter of ExBoard's Script Core, which mounts a Board of ExGrid, ExPivot and ExChart views in a React tree.

It is one of the packages of the ExGrid family: Excel-like grids, sheets, pivot tables and charts for reading
money and risk numbers, published to npm under `@ex-grid` and to NuGet for Blazor, at one shared version.

## Install

```sh
npm install @ex-grid/board-react react react-dom
```

`react` and `react-dom` are peer dependencies: your application provides them, at the versions `package.json` names.

It depends on exactly the version of `@ex-grid/board`, `@ex-grid/chart-react`, `@ex-grid/pivot`, `@ex-grid/pivot-react` and `@ex-grid/react` it was built with, so upgrade the family's packages together.

What it draws reads these stylesheets. Import them once in your application:

```ts
import '@ex-grid/core/styles.css';
import '@ex-grid/pivot/styles.css';
import '@ex-grid/chart/styles.css';
```

## Documentation

[ExBoard on the documentation site](https://akiramisawa.github.io/ex-grid/exboard), read for React with the switch at the top, and every
version's [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes).

> **The API may still change.** While the version is `0.x`, it may change from one version to the next; each
> version's Release Notes say what changed. A prerelease, a version with a suffix such as `-rc.1`, passed every
> test layer in CI on the commit it was built from. A version without a suffix is published only once the
> project's [Definition of Done](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/definition-of-done.md) is
> signed off on it.

## Requirements

- **Chrome or Edge.** They are the supported browsers; Safari and Firefox are out of scope.
- **React 19.0.0 or a later 19.x.**

## License

MIT
