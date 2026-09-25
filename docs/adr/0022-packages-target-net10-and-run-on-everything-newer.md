# Packages target net10.0; every newer runtime is supported by construction

*(Rewritten 2026-09-25. This ADR first read "Packages target net8.0" and rejected `net10.0`
only, on the ground that **Consumers on .NET 8 must be able to reference the package**. That
ground has an end date: .NET 8 (LTS) and .NET 9 (STS, extended to 24 months) both leave
support on 2026-11-10, and no release of any package had been published, so raising the floor
now breaks no Consumer. The rejected option's own entry below already called it "defensible
for a library born after net8's end of support" — this package is. The decision was taken
while designing the Blazor Server host, but it does not depend on it: nothing in that work
needs an API newer than net8. What changed is the floor, not a need. The file was renamed
with the title; the old name was `0022-packages-target-net8-and-run-on-everything-newer.md`.)*

The shipped NuGet packages (`ExGrid`, the integration packages, later `ExSheet`) target
**`net10.0`, single-target**. A `net10.0` Razor class library loads into net10 and every newer
application unchanged — NuGet unifies `Microsoft.AspNetCore.Components.*` to the
application's own version. So "support .NET 10 and everything above it" is satisfied by
**one** target, not a list of them.

The **SDK used to build this repository is a separate question** and stays on the current LTS
(.NET 10, pinned by the flake and `global.json`). Today the SDK and the target coincide; when
the next LTS SDK arrives the SDK moves and the target does not, which is the normal shape for
a library.

## Reasons

**The floor is the oldest runtime still supported, and from 2026-11-10 that is .NET 10.** A
floor below it would promise Consumers a runtime Microsoft no longer patches, and would keep
this repository on C# 12 and on a second runtime in the dev shell for nobody's benefit.

**Nothing in the other ADRs needs an API newer than net8** — which is why this is a floor
decision and not a feature one. Virtualisation arithmetic, `ShouldRender()` memoisation, the
Chrome seams and the allowlisted JS uses
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)) all existed in net8. The
features that *are* recent — the Popover API and CSS Anchor Positioning
([ADR-0017](./0017-target-chromium-browsers-only.md)) — live in the **browser**, not the
framework. Code written after this rewrite may use what net10 offers (for instance
`RendererInfo`, which tells a component whether it is interactive yet); it is no longer held
to net8's surface.

Rejected:

- **Multi-target `net8.0;net9.0;net10.0`** — pays the cost now (a matrix, `#if` seams,
  per-TFM package references) for runtimes that leave support within weeks of this decision.
- **Keep `net8.0`** — the original decision. Its requirement was real while net8 was
  supported; after 2026-11-10 it would be a floor under an unsupported runtime.

## Consequences

- **`Microsoft.AspNetCore.Components.Web` is referenced at the lowest 10.0.x version.** The
  Consumer's application resolves it upward; referencing a higher version would silently raise
  the real floor.
- **The code is C# 14**, the language `net10.0` defaults to. `LangVersion` stays unset: the
  compiler ties the language to the target framework by itself, so a newer SDK does not let
  newer syntax slip in. Do not raise `LangVersion` to get around it.
- **Raising the floor is a breaking change.** It happens only as a deliberate decision — a
  major version bump and a rewrite of this ADR, as this one was — never as a side effect of
  wanting a newer API.
- **The gating tests execute on the target runtime.** They target `net10.0`, the same runtime
  the SDK carries, so the dev shell no longer needs a second runtime alongside the SDK.
- **The hosts (`samples/`) and `spikes/render-bench` are unaffected.** They are not shipped
  and target whatever the SDK provides, which today is the same `net10.0`.
