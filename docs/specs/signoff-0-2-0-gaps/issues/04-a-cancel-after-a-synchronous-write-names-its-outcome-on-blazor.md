# 04: On Blazor, a cancel after a synchronous write is said as what happened (ED-34)

Status: ready-for-agent
Roadmap: ExGrid — On Blazor, an Escape that arrives after your OnEdit or OnPaste has already written is said as written, not as cancelled.

**Why:** ED-34 asks that an Escape typed while a commit is pending says its outcome in the live region, cancelled
or already answered. On Blazor's ExGrid over the Script Core, a cancel that reaches .NET after a synchronous
`OnEdit` or `OnPaste` has written is said as cancelled though the write stands: the browser decides Escape, and
to name which happened it needs the outcome from .NET (ADR-0240's note of 2026-10-29). The React half is held. The
user made it a Known limitation of 0.2.0 on 2026-11-07 (0.2.0's Release Notes; the [sign-off](../../../../verification/2026-11-09-linux-signoff/results.md), ED-34).

**What:** the answer that crosses from .NET carries whether the handler had already run when the cancel arrived,
and the Script Core announces that outcome; a change to the bridge's wire and to the core's pending commit. Read
ADR-0240 section 12 and ADR-0380 section 3 first: if the outcome cannot cross without a decision they do not hold,
stop and record it. Layer 2 holds it on both adapters (ED-34's Check). Then the Known limitation goes from the Docs
Site's Validation section, and a Change Note says so.
