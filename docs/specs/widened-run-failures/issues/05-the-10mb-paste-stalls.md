# 05: The ~10 MB paste never completes on the Server host

Status: needs-triage
Roadmap: ExGrid — A ~10 MB paste always completes on a slow connection.

**Seen:** `gestures.spec.mjs:167`, "a ~10MB paste parses without freezing the grid (PST-5, PST-6
recorded, ADR-0052)":

- Server host, Chrome and Edge, shard 3/4 on `main` and shard 4/4 on the branch: `#paste-status`
  expected "400 cells from 400x1", received "Pasted: —" after 30 s (line 183).
- WebAssembly, Edge, shard 4/4 on the branch: the poll of the active descendant for `r1c1` timed out
  at its 500 ms (line 192). The test took 22.1 s.

About 60 s after each Server failure the host logs a `TimeoutException` from `RemoteJSDataStream`,
which ticket 06 is about.

**Inferred:** the paste stream stops delivering its chunks. On the Server host,
`RemoteJSDataStream` times out after one minute with no chunk, which fits the 60 s.

- [ ] Find why the stream stops under latency and a throttled CPU.
- [ ] Fix it in the product. The WebAssembly failure may be a reading taken too early instead.
- [ ] The test passes 20 runs on each host under the widened conditions.
