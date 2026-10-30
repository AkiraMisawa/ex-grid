Product: ExGrid
Kind: Behaviour
ADR: 0350

Measured against 0.1.0 on one machine (`spikes/render-bench/results/20261030-script-core/`): Blazor's components over the Script Core take keys up to fifteen times faster on WebAssembly and twice as fast on Server, but are slower where .NET hands the browser data. A new `Rows` list crosses about 28 KB on Blazor Server whatever changed, and takes 21 to 74 ms on WebAssembly where 0.1.0 took 3 to 31; an ExSheet whose `Document` is bound hands .NET the whole document after every edit, about a second on WebAssembly for 2,000 rows of formulas; and an ExPivot's Change Batch takes 63 ms and 63 KB on Server where 0.1.0 took 4 ms and 2.6 KB.
