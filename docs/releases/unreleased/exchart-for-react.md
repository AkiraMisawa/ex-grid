Product: ExChart
Kind: Added
ADR: 0370

`@ex-grid/chart-react`, a new npm package, is ExChart's React component: `<ExChart>` takes `createChart`'s options as props, mounts the chart into the element it renders, hands it each render's props and destroys it on unmount. A `ref` reaches `download(format)`. A new Chart Definition is a new drawing, so a Consumer builds it when its data changes, not on every render. It is published with 0.3.0.
