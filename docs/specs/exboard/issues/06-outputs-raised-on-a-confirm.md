# 06: Outputs raised on a confirm

Status: ready-for-agent
Roadmap: internal

ADR-0340 (Q6): an output is declared and raised only by a declared confirm action, as a value
condition on the input with the source version it was taken against, as a .NET event and as JSON. A
version that has moved on is refused (spine rule 6).
