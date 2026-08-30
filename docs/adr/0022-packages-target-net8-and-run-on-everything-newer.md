# Packages target net8.0; every newer runtime is supported by construction

The shipped NuGet packages (`ExGrid`, the integration packages, later `ExSheet`) target
**`net8.0`, single-target**. A `net8.0` Razor class library loads into net8, net9 and net10+
applications unchanged — NuGet unifies `Microsoft.AspNetCore.Components.*` to the application's
own version. So "support .NET 8 and everything above it" is satisfied by **one** target, not a
list of them.

The **SDK used to build this repository is a separate question** and stays on the current LTS
(.NET 10, pinned by the flake and `global.json`). An SDK builds every TFM at or below itself;
pinning the newest SDK while targeting the oldest supported framework is the normal shape for a
library.

## Reasons

**The floor is a requirement, not a preference.** Consumers on .NET 8 must be able to reference
the package. A `net10.0`-only package shuts them out at restore time.

**Nothing in the other 21 ADRs needs an API newer than net8.** Virtualisation arithmetic,
`ShouldRender()` memoisation, the Chrome seams, and the three allowlisted JS uses
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)) all exist in net8. The features
that *are* recent — the Popover API and CSS Anchor Positioning
([ADR-0017](./0017-target-chromium-browsers-only.md)) — live in the **browser**, not the
framework, and render from any Blazor version.

Rejected:

- **Multi-target `net8.0;net9.0;net10.0`** — pays the cost now (a CI matrix, `#if` seams,
  per-TFM package references) for a benefit no consumer has asked for. Adding a TFM later is
  additive and breaks nobody, so the option stays open at zero cost. This mirrors the JS
  allowlist stance: the capability is added when a **recorded need** exists, not in advance.
- **`net10.0` only** — simplest, and defensible for a library born after net8's end of support.
  But the floor-of-8 requirement exists, and net8 is the last floor that covers every
  still-deployed LTS application.

## Consequences

- **`Microsoft.AspNetCore.Components.Web` is referenced at the lowest 8.0.x version.** The
  consumer's application resolves it upward; referencing a higher version would silently raise
  the real floor.
- **The code is C# 12.** The compiler enforces this by itself — `net8.0` defaults to
  LangVersion 12 regardless of how new the installed SDK is — so newer syntax fails the build
  rather than slipping through. Do not raise `LangVersion` to get around it.
- **Raising the floor is a breaking change.** It happens only as a deliberate decision — a major
  version bump and a rewrite of this ADR — never as a side effect of wanting a newer API.
- **If a newer-framework API is ever genuinely needed, that is when multi-targeting enters**,
  with the need recorded here, and `net8.0` remains in the list until the floor decision above
  is revisited.
- **Tests must eventually run on the net8 runtime, not just build against it.** The nix shell
  currently carries only the .NET 10 SDK; when the test projects exist, the dev shell grows the
  net8 runtime alongside it so the gating layers execute on the oldest supported runtime.
- **`spikes/render-bench` is unaffected.** It is disposable and not shipped; it may target
  whatever the SDK provides.
