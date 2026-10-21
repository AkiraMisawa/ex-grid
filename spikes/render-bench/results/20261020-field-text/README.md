# Field text in the Input Order, 2026-10-20

Before: `8027b9f52f8b30eedd1b277a485b0ecaaf330b74`.
After: the field-text implementation in this branch, based on that revision. The SHA-256 of
`git diff 8027b9f5 -- src` at measurement was
`b402e4386c331096659047efcad880c71ece402b1b359717a01fe37eb5a6f0b7`.
The PR identifies the committed implementation revision.

macOS arm64, installed Chrome 155 headless, 1280×1000, .NET SDK 10.0.203 through Nix,
Release builds. The existing `Bench.Live` `/settled` page and `cdp-input.mjs` ran seven rounds
per configuration, with 20 characters, 20 presses and ten menu iterations. The Server host
ran at port 5791 through the latency proxy at 5891 (control 5991); WebAssembly at 5792,
with Development enabled for its build-time static assets. Chrome used a dedicated profile
and CDP port 9347. No CPU throttling was applied to these measurements.

Medians in milliseconds, before → after:

| Configuration | Typing | Presses | Menu burst |
|---|---:|---:|---:|
| WebAssembly | 55.9 → 58.0 | 104.4 → 106.7 | 82.4 → 84.3 |
| Server, 0 ms | 16.5 → 16.9 | 16.9 → 16.6 | 350.2 → 349.6 |
| Server, 60 ms | 200.3 → 200.2 | 150.2 → 150.7 | 2099.9 → 2150.0 |

All six configurations completed seven rounds, with no failed phase and no browser complaint.
These are descriptive readings from one before/after pass, not performance thresholds or a
statistical claim about small differences. The raw JSON retains each round and menu step.
The benchmark's legacy `hasMark` metadata looks on the root rather than the stamp child and
is false in both builds; the phases judge the visible outcomes they exercise.

The disposable WebAssembly benchmark host reports its existing NU1903 warnings for
`Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.7; this change does not alter that
dependency. The product solution build had no warnings. The first after-run could not save
because its output directory was absent. That setup was corrected before the retained runs;
its idle tab was still present during the retained WebAssembly run and was closed before the
Server runs. No result from the failed save is reported. All measurement hosts, proxy and the
dedicated Chrome were stopped after collection.
