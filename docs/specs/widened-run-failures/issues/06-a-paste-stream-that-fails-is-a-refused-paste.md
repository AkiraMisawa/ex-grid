# 06: A paste stream that fails to read is a refused paste, not an unhandled exception

Status: done

**Seen:** in both runs, about 60 s after each Server-host failure of ticket 05, the host logs:

```
fail: Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost[111]
      Unhandled exception in circuit '…'.
      System.TimeoutException: Did not receive any data in the allotted time.
         at System.IO.Pipelines.Pipe.GetReadAsyncResult()
         …
         at Microsoft.AspNetCore.Components.Server.Circuits.RemoteJSDataStream.ReadAsync(…)
         at System.IO.StreamReader.ReadToEndAsyncInternal(CancellationToken cancellationToken)
```

CON-6 charges that line to whichever test is running, so bystanders fail: heading-mark:122 (UX-21)
on `main`, harness:151 and :159 on the branch.

**Today:** `RemoteJSDataStream` is read in one place, `TakePasteStreamsAsync` in `ExGrid.razor`
(`reader.ReadToEndAsync()`), and nothing there catches a failed read. On a live circuit the exception
would end the circuit.

**Why it is a defect:** a paste that cannot be read should be refused aloud, as any paste the grid
cannot carry out is (the spine's first principle). It should not take the circuit down or reach the
host log as unhandled.

- [x] A failed or timed-out read refuses the paste with a stated reason.
- [x] Layer 2: a stream that throws on read refuses the paste and leaves the grid working.
- [x] Change Note and the Docs Site's paste page, if the refusal is visible.
