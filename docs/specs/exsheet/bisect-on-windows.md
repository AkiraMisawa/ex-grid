# Bisect the Windows-only layer-3 failures

Status: done — run at e9c448a on 2026-09-27, as the second run's Part E
(`verification/2026-09-27-windows-bisect/`): the first bad commit is 062be53. Step 3, the runs at
100% and 125%, was blocked, since a change of display scale needs a sign-out. No run needs it now:
ADR-0053 fixed what the bisect chased, and VZ-14, BIG-1 and BIG-5 pass at 150% from the third run
on. *(Set from the records on 2026-10-10.)*

For a Claude Code session on the Windows machine used on 2026-09-27
(`verification/2026-09-27-windows/results.md`). Decide nothing, and record everything.

**What failed.** On Windows 11 at 150%, on both Chrome and Edge and both hosts:

- **VZ-14** and ADR-0012/0013's "the Focus is never behind a scrollbar" (three tests in
  `scrollbar.spec.mjs`): at Ctrl+End the Focus sits about one row below the readable area.
- **BIG-1** and **BIG-5** (`virtualisation.spec.mjs`): the last row at 10⁶ rows is never painted.

The same specs passed on this machine on 2026-09-23 (`verification/2026-09-23-windows/`). They
already failed at `789b208` (`main`, #24), so they came in between, before the ExSheet work. The
same specs pass on Linux Chromium in the container.

**Do.**

1. Find the last commit on `main` that the 2026-09-23 record names, or the last `main` commit dated
   2026-09-23. That is *good*. `789b208` is *bad*.
2. `git bisect start 789b208 <good>`. At each step:
   - build and start the WebAssembly DemoHost, as Part B of `verify-on-windows.md` did (in WSL);
   - run only `scrollbar.spec.mjs` and `virtualisation.spec.mjs` (`-g "VZ-14|BIG-1|BIG-5"`) on
     Chrome at 150%;
   - mark `good` or `bad`.
3. Also run each spec at 100% and 125% at the first bad commit, to see whether the scale matters.
4. Record in `verification/<date>-windows-bisect/results.md`: the first bad commit, its diff
   summary, the runs at each step, and whether VZ-14 and BIG-1/5 share one cause. **Do not fix it.**
5. Commit the record on the branch the run uses. When it runs as Part E of
   `verify-on-windows-2.md`, that is `claude/exsheet-windows-verify-2`. Then push.
