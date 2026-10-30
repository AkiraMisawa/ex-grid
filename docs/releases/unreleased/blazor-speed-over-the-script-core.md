Product: ExGrid
Kind: Behaviour
ADR: 0350

Measured against 0.1.0 on one machine (`spikes/render-bench/results/20261030-script-core/`): Blazor's components over the Script Core take keys up to fifteen times faster on WebAssembly and twice as fast on Server. Where .NET hands the browser data they are close to 0.1.0 or slower: a push of one row into 10,000 takes 6 ms on Server and 12 ms on WebAssembly where 0.1.0 took 3 and 4; a Window of all new rows, or any push over 100,000 rows on WebAssembly, takes 11 to 75 ms where 0.1.0 took 7 to 31; an ExSheet edit takes 19 to 129 ms on either host where 0.1.0 took 13 to 103 on Server and 99 to 641 on WebAssembly; and an ExPivot Change Batch takes about 60 ms on either host where 0.1.0 took 4 on Server and 15 on WebAssembly, spent laying the report out again in the browser.
