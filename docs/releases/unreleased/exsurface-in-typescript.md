Product: ExChart
Kind: Added
ADR: 0370

`@ex-grid/chart` draws the 3D surface without Blazor: `createSurface(element, options)` returns `{ update, destroy, download, svgText }`. It behaves as `ExSurface` does: drag or the arrow keys turn it, the wheel or + and - zoom, and Shift or the right button pans. The tooltip reads the point nearest the pointer, and a click draws that slice as a chart beside the surface. A hole is drawn as a hole. "View as table" tabulates a grid or lists a mesh's nodes, and the drawing saves as SVG or PNG. The view and the slice are raised when a gesture ends (`onViewChanged`, `onSliceChanged`) and taken back as options. `surfaceSummary` writes the accessible summary on the shared cases. `@ex-grid/chart-react` adds `<ExSurface>`, whose `ref` reaches `download` and `svgText`. Its words are English by default, and `label: japaneseChartWords` switches them to Japanese. It is published with 0.3.0.
