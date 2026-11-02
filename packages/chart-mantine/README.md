# @ex-grid/chart-mantine

ExChart for Mantine: the design-system Wrapper that draws ExChart for React's toolbar and context menu with Mantine's components, docks the charts made from a grid's selection beside it in Mantine's tabs, and maps the chart's Visual Tokens onto a Mantine theme.

It is one of the packages of the ExGrid family: Excel-like grids, sheets, pivot tables and charts for reading
money and risk numbers, published to npm under `@ex-grid` and to NuGet for Blazor, at one shared version.

## Install

```sh
npm install @ex-grid/chart-mantine @mantine/core @mantine/hooks react react-dom
```

`@mantine/core`, `@mantine/hooks`, `react` and `react-dom` are peer dependencies: your application provides them, at the versions `package.json` names.

It depends on exactly the version of `@ex-grid/chart`, `@ex-grid/chart-react`, `@ex-grid/core` and `@ex-grid/mantine` it was built with, so upgrade the family's packages together.

What it draws reads these stylesheets. Import them once in your application, after the design system's own:

```ts
import '@ex-grid/core/styles.css';
import '@ex-grid/chart/styles.css';
import '@ex-grid/chart-mantine/styles.css';
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
- **React 19.2.0 or a later 19.x.**

## License

MIT
