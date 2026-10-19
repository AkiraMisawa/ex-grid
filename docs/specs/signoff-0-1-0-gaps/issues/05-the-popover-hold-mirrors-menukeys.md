# 05: The script's popover hold mirrors MenuKeys

Status: ready-for-agent
Roadmap: internal

**Why:** KB-2 (restated 2026-10-18) has the core hand the script every set of keys at attach. The 0.1.0
sign-off's inspection found one place left where the script carries a "must move together" twin of a C#
key resolution: the popover hold's `handsOver`/`escapeCloses`, which its own comment calls "this
listener's own mirror of MenuKeys.ResolveInColumnMenu". KB-2's Pass holds, since the hold decides only
whether the keys after it wait; but a pairing that must move together is the defect AGENTS.md warns of.

**What:** the hold's keys handed from C# at attach, as KB-2's gate sets are, with a layer-2 test that
reads them from `MenuKeys`, and the script's comment removed.
