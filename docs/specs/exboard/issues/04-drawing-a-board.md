# 04: Drawing a Board

Status: ready-for-agent
Roadmap: internal

ADR-0340: an `ExBoard` component draws a read Board with each input bound to a source (in memory or
held where the data lives), refusing a mismatch by name before anything is drawn. `ExBoard.MudBlazor`
wraps it. Layer 3 covers the layout under both hosts.
