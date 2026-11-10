# 07: PST-6 measured at the sizes its row names

Status: ready-for-agent
Roadmap: internal

**Why:** PST-6 records a paste of 400 source cells, about 10 MB, where its row names 10⁵ and 10⁶. The 0.2.0
sign-off read it from CI's records as they were (the [sign-off](../../../../verification/2026-11-09-linux-signoff/results.md), "For after 0.2.0").

**What:** PST-6's harness pastes at 10⁵ and 10⁶ cells and records what the row names, observational and never gated.
If a browser cannot hold a clipboard that large, the record says so with the size it reached, and the row is
brought to the user.
