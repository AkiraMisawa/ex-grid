# 05: The ~10 MB paste never completes on the Server host

Status: done
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

- [x] Find why the stream stops under latency and a throttled CPU.
- [x] Fix it in the product. The WebAssembly failure may be a reading taken too early instead.
- [x] The test passes 20 runs on each host under the widened conditions.

**Found (2026-10-20):**

- **Server.** The stream did not stop; it was slow. Blazor's script sends a JS stream one chunk of
  about 31 KB per round trip once the round trip is slow, so 10 MB at 150 ms took close to a
  minute, past the test's 30 s and near the server's own one-minute stream timeout.
- **WebAssembly.** Three costs on a CPU throttled fourfold, none of them the stream: the DemoHost
  page rendered all 400 fields of 25 KB each into its status line, `ParseTsv` appended one
  character at a time, and the page's `OnPaste` replaced rows one at a time, each replacement
  re-applying the Grid Source. Together they took about 28 s.

**Fixed:** the paste crosses gzip-compressed and is taken at its turn (ADR-0005, amended
2026-10-20); a write handler's changes to the Grid Source are taken in once when it returns;
`ParseTsv` appends runs; the DemoHost shows the first 20 fields, cut to 40 characters. Under the
widened conditions the paste now lands in 2.0–2.8 s on Server and 8.7–11.3 s on WebAssembly
(most of what remains is decoding 10 MB of UTF-8 on a throttled CPU), and the next key is answered
in about 230 ms and 125 ms.
