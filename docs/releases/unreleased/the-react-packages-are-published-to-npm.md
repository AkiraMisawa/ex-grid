Product: ExGrid
Kind: Added
ADR: 0370, 0042, 0123

The family's twenty-three npm packages are published from the same tag as its NuGet packages, at the same version, each depending on the others at exactly that version: a prerelease or a release candidate under npm's `next` dist-tag. npm points `latest` at a package's first version whatever its tag, so until 0.3.0 itself is out, `npm install` without a version takes the first 0.3.0 prerelease; name the version you want. `@ex-grid/core`, `@ex-grid/sheet`, `@ex-grid/pivot` and `@ex-grid/chart` carry their product's stylesheet as `styles.css`, the same file the NuGet package serves: import `@ex-grid/core/styles.css`, and the product's own, once in a React application, before a Wrapper's `styles.css`. Each package has a README that names its peers and the stylesheets it reads.
