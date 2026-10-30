using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// F4 cycles the Reference at the caret (ADR-0051, 2026-09-29; SH-28). What the engine rewrites, and the span it
/// answers, is in <c>tests/cases/sheet/reference-cycle.json</c>, with a selection outside the text; what stays here is
/// a null text, which the Script Core's signature does not admit.
/// </summary>
public class ReferenceCycleTests
{
    [Fact] // ADR-0051: no text is an argument error, not a guess
    public void No_text_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => FormulaEntry.CycleReference(null!, 0, 0));
    }
}
