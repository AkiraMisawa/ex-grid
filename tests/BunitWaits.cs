using System.Runtime.CompilerServices;
using Bunit;

namespace ExGrid.Testing;

/// <summary>
/// The longest a bUnit wait — <c>WaitForAssertion</c>, <c>WaitForState</c>,
/// <c>WaitForElement</c> — waits before it fails, for every test in the assembly. Compiled into
/// each test project that references bUnit (tests/Directory.Build.targets).
/// </summary>
/// <remarks>
/// bUnit's own is one second, and on a CI runner that was not enough: ExGrid.MudBlazor.Tests
/// failed one, then two, of 198 tests there and never in a Linux container running the same
/// command, with coverage and every suite at once (claude/exsheet-cell-format, 2026-10-01).
/// A wait passes the moment its condition holds, so this changes how long a failing condition
/// is waited for, never what passes: a test that failed before still fails, ten seconds later.
/// </remarks>
internal static class BunitWaits
{
    [ModuleInitializer]
    internal static void WaitLongEnoughForACiRunner() => BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(10);
}
