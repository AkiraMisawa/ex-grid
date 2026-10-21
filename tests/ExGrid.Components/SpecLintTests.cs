using ExGrid.Components.Tests.Support;
using Xunit;
using static ExGrid.Components.Tests.Support.SpecLint;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0220 §6: layer 3's specs neither wait for a fixed time nor read straight after an input before
/// anything waits for the grid's answer. The lint is <see cref="SpecLint"/>; the first test runs it over
/// every spec, and the rest pin what it refuses and what it lets through, so it is seen to catch what
/// it says it catches and not to pass a spec because a pattern was too loose.
/// </summary>
public class SpecLintTests
{
    private static readonly string Specs = Path.Combine(AssetSources.Repository, "tests", "ExGrid.Browser");

    public static TheoryData<string> SpecFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(Specs, "*.spec.mjs").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file));
        }
        return data;
    }

    [Theory, MemberData(nameof(SpecFiles))] // ADR-0220 §6: no spec waits a fixed time or reads before the grid has answered
    public void A_spec_neither_waits_a_fixed_time_nor_reads_before_the_grid_has_answered(string spec)
    {
        var findings = Check(File.ReadAllText(Path.Combine(Specs, spec)));
        Assert.True(findings.Count == 0,
            $"{spec}: {findings.Count} finding(s) of the spec lint (ADR-0220 §6; tests/ExGrid.Browser/README.md, \"The spec lint\"):\n"
            + string.Join("\n", findings.Select((f) => $"  line {f.Line}, {f.Rule}: {f.Why}\n    {Shorten(f.Statement)}")));
    }

    private static string Shorten(string statement) => statement.Length <= 160 ? statement : statement[..157] + "...";

    private static Rule[] Rules(string spec) => [.. Check(spec).Select((f) => f.Rule)];

    // A test body around the lines under test.
    private static string InTest(string body) => $$"""
        import { test, expect, circuitQuiet } from './fixtures.mjs';

        test('the case', async ({ page }) => {
        {{body}}
        });
        """;

    [Fact] // ADR-0220 §6 (2026-10-20): keys watched for where they land, typed under a round trip with the answers flowing
    public void Keys_watched_under_a_round_trip_without_the_answers_held_are_refused()
    {
        var findings = Check(InTest("""
                const landed = await watchWhereKeysLand(page);
                const circuit = await setRoundTrip(80);
                await page.keyboard.press('Escape');
                await page.keyboard.press('ArrowDown');
                await circuitQuiet();
                expect(await landed()).toEqual([]);
            """));
        var finding = Assert.Single(findings);
        Assert.Equal(Rule.RoundTrip, finding.Rule);
        Assert.Contains("Escape", finding.Statement);
    }

    [Theory] // ADR-0220 §6 (2026-10-20): held answers, no round trip, a reason given, or a record kept but not asserted, pass
    [InlineData("""
            const landed = await watchWhereKeysLand(page);
            const circuit = await holdAnswers(true);
            await page.keyboard.press('Escape');
            await holdAnswers(false);
            await circuitQuiet();
        """)]
    [InlineData("""
            const landed = await watchWhereKeysLand(page);
            await page.keyboard.press('Escape');
            await circuitQuiet();
        """)]
    [InlineData("""
            await setRoundTrip(80);
            await page.keyboard.press('Escape');
            await circuitQuiet();
        """)]
    [InlineData("""
            const landed = await watchWhereKeysLand(page);
            await setRoundTrip(80);
            await setRoundTrip(0);
            await page.keyboard.press('Escape');
            await circuitQuiet();
        """)]
    [InlineData("""
            const landed = await watchWhereKeysLand(page);
            await setRoundTrip(80);
            // spec-lint: round-trip: the test is about keys typed while the answer is on its way
            await page.keyboard.press('Escape');
            await circuitQuiet();
        """)]
    [InlineData("""
            const landed = await watchWhereKeysLand(page);
            await setRoundTrip(150);
            await page.keyboard.press('Escape');
            await circuitQuiet();
            await testInfo.attach('keys', { body: JSON.stringify(await landed()) });
        """)]
    public void Keys_watched_with_the_answers_held_or_with_no_round_trip_pass(string body)
    {
        Assert.Empty(Check(InTest(body)));
    }

    [Fact] // ADR-0220 §6: KeyTips typed under a round trip are keys too, and a record read into a variable is still the record
    public void KeyTips_typed_under_a_round_trip_are_watched_keys()
    {
        Assert.Equal([Rule.RoundTrip], Rules(InTest("""
                const landed = await watchWhereKeysLand(page);
                await setRoundTrip(80);
                await keyTips(page, 'h', 'h');
                await circuitQuiet();
                const keys = await landed();
                expect(keys.find((k) => k.key === 'h')?.where).not.toBe('list');
            """)));
    }

    [Theory] // ADR-0220 §6: a fixed wait, however it is spelled
    [InlineData("    await page.waitForTimeout(500);")]
    [InlineData("    await grid.page().waitForTimeout(PACE_MS);")]
    [InlineData("    await new Promise((resolve) => setTimeout(resolve, 300));")]
    [InlineData("    await page.evaluate(() => new Promise((resolve) => window.setTimeout(resolve, 500)));")]
    [InlineData("    await sleep(200);")]
    [InlineData("    if (pauseMs) await page.waitForTimeout(pauseMs);")]
    public void A_fixed_wait_is_refused(string line)
    {
        Assert.Equal([Rule.FixedWait], Rules(InTest(line)));
    }

    [Theory] // ADR-0220 §6: a test's own time limit, or a timer that is not a wait, is no fixed wait
    [InlineData("    test.setTimeout(120_000);")]
    [InlineData("    testInfo.setTimeout(600_000);")]
    [InlineData("    // await page.waitForTimeout(500);")]
    [InlineData("    const text = 'waitForTimeout(500)';")]
    [InlineData("    await expect(cell).toHaveText('A', { timeout: 10_000 });")]
    public void What_is_not_a_fixed_wait_is_let_through(string line)
    {
        Assert.Empty(Check(InTest(line)));
    }

    [Theory] // ADR-0220 §6: a read straight after an input, each kind of read
    [InlineData("const n = await grid.locator('.ex-row').count();")]
    [InlineData("expect(await nameBox(grid).inputValue()).toBe('B2');")]
    [InlineData("const box = await cell(grid, 'C3').boundingBox();")]
    [InlineData("const top = await grid.locator('.ex-scroller').evaluate((el) => el.scrollTop);")]
    [InlineData("const picture = await grid.screenshot();")]
    [InlineData("expect(await editor(grid).isVisible()).toBe(true);")]
    [InlineData("const at = await boxOf(cell(grid, 'E3'));")]
    [InlineData("const copied = await readClipboard(page);")]
    [InlineData("const text = await grid\n        .locator('.ex-cell')\n        .first()\n        .textContent();")]
    [InlineData("const moved = await page.evaluate(() => {\n        return document.activeElement.id;\n    });")]
    public void A_read_straight_after_an_input_is_refused(string read)
    {
        foreach (var input in new[]
                 {
                     "await page.keyboard.press('ArrowDown');",
                     "await page.keyboard.type('12');",
                     "await page.mouse.click(10, 10);",
                     "await nameBox(grid).click();",
                     "await bar(grid).fill('=A1');",
                     "await clickCell(grid, 'B2');",
                     "await page.setViewportSize({ width: 800, height: 600 });",
                 })
        {
            var findings = Check(InTest($"    {input}\n    {read}"));
            Assert.True(findings is [{ Rule: Rule.EarlyRead }], $"after `{input}`, `{read}` gave {findings.Count} finding(s)");
            Assert.Equal(5, findings[0].Line);
        }
    }

    [Fact] // ADR-0220 §6: a statement between the input and the read that waits for nothing is no shelter
    public void A_statement_that_waits_for_nothing_does_not_settle()
    {
        var spec = InTest("""
                await page.keyboard.press('Enter');
                const expected = 'B3';
                await page.evaluate(() => { window.__mark = 1; });
                expect(true).toBe(true);
                await expect(42).toBe(42);
                await twoFrames(page);
                const box = await cell(grid, expected).boundingBox();
            """);
        Assert.Equal([Rule.EarlyRead], Rules(spec));
    }

    [Theory] // ADR-0220 §6: between the input and the read, what waits for the grid's answer
    [InlineData("await expect(nameBox(grid)).toHaveValue('B2');")]
    [InlineData("await expect(editor(grid)).not.toBeVisible();")]
    [InlineData("await expect(grid.locator('.ex-row')).toHaveCount(20, { timeout: 10_000 });")]
    [InlineData("await expect.poll(() => grid.evaluate((el) => el.scrollTop)).toBe(300);")]
    [InlineData("await expect(async () => {\n        expect(await nameBox(grid).inputValue()).toBe('B2');\n    }).toPass();")]
    [InlineData("await circuitQuiet();")]
    [InlineData("await settled(grid);")]
    [InlineData("await expectFocusAt(grid, 'B2');")]
    [InlineData("await revealRepainted(page);")]
    [InlineData("await page.locator('.ex-popover').waitFor();")]
    [InlineData("await page.waitForFunction(() => window.__done);")]
    [InlineData("await expect(page.getByRole('menuitem', { name: 'Copy' })).toBeFocused();")]
    [InlineData("await expect(cell(grid, 'B2')).toHaveCSS('color', 'rgb(0, 0, 0)', {\n        timeout: 10_000,\n    });")]
    public void What_waits_for_the_answer_settles_the_input(string settle)
    {
        var spec = InTest($"""
                await page.keyboard.press('ArrowDown');
                {settle}
                const n = await grid.locator('.ex-row').count();
            """);
        Assert.Empty(Check(spec));
    }

    [Theory] // ADR-0220 §6: a helper that presses and waits for the answer is a settled input
    [InlineData("await pressCell(grid, 'B2');")]
    [InlineData("await goTo(grid, 'B2');")]
    [InlineData("await enter(page, grid, 'B2', '12');")]
    [InlineData("await openSheet(page);")]
    [InlineData("await page.goto('/sheet');")]
    public void A_helper_that_waits_for_its_answer_settles(string input)
    {
        Assert.Empty(Check(InTest($"    {input}\n    const box = await boxOf(cell(grid, 'B2'));")));
    }

    [Fact] // ADR-0220 §6: a read before any input, and a bare action, are no early reads
    public void A_read_with_no_input_owed_is_let_through()
    {
        var spec = InTest("""
                const before = await grid.locator('.ex-row').count();
                await page.keyboard.press('ArrowDown');
                await page.evaluate(() => navigator.clipboard.writeText('42'));
            """);
        Assert.Empty(Check(spec));
    }

    [Fact] // ADR-0220 §6: a function written after an input reads when it is called, not where it is written
    public void A_function_s_definition_is_no_read()
    {
        var spec = InTest("""
                await page.keyboard.press('ArrowDown');
                const boxOfHeader = async (column) => lineAlong(page, await header(column).boundingBox());
                await expect(nameBox(grid)).toHaveValue('B2');
                const box = await boxOfHeader(2);
            """);
        Assert.Empty(Check(spec));
    }

    [Fact] // ADR-0220 §6: each test starts owing nothing
    public void A_test_starts_afresh()
    {
        var spec = """
            test('one', async ({ page }) => {
                await page.keyboard.press('ArrowDown');
            });

            test('two', async ({ page }) => {
                const n = await grid.locator('.ex-row').count();
            });
            """;
        Assert.Empty(Check(spec));
    }

    [Fact] // ADR-0220 §6: strings, templates, regular expressions and comments do not cut or fool the reading
    public void Literals_and_comments_neither_cut_nor_count()
    {
        var spec = InTest("""
                await page.keyboard.press('{');
                const pattern = /;\s*await x\.count\(/;
                const label = `a ${'}'} b; await y.count()`;
                // const n = await grid.locator('.ex-row').count();
                /* await page.waitForTimeout(500); */
                const n = await grid.locator('.ex-row').count();
            """);
        var findings = Check(spec);
        Assert.True(findings is [{ Rule: Rule.EarlyRead, Line: 9 }], string.Join("; ", findings));
    }

    [Theory] // ADR-0220 §6: a comment naming the rule and why allows the statement, on it or straight above
    [InlineData("    await page.waitForTimeout(500); // spec-lint: fixed-wait: the test is about a key held for half a second")]
    [InlineData("    // spec-lint: fixed-wait: the test is about a key held for half a second\n    await page.waitForTimeout(500);")]
    [InlineData("    // spec-lint: fixed-wait: the test is about a key held for half a second,\n    // which the Keyboard Field must treat as one press.\n    await page.waitForTimeout(500);")]
    [InlineData("    await page.keyboard.press('A');\n    // spec-lint: early-read: the listener runs inside the press, so the answer is in before it returns\n    expect(await keySeenUntouched(page)).toBe(true);")]
    public void An_allowance_with_its_reason_lets_the_statement_through(string body)
    {
        Assert.Empty(Check(InTest(body)));
    }

    [Theory] // ADR-0220 §6: an allowance names its rule, and why; it reaches only its own statement
    [InlineData("    await page.waitForTimeout(500); // spec-lint: fixed-wait", new[] { Rule.Allowance, Rule.FixedWait })]
    [InlineData("    await page.waitForTimeout(500); // spec-lint: fixed-wait: needed", new[] { Rule.Allowance, Rule.FixedWait })]
    [InlineData("    await page.waitForTimeout(500); // spec-lint: early-read: the wrong rule for this statement", new[] { Rule.FixedWait })]
    [InlineData("    await page.waitForTimeout(500); // spec-lint: flaky: because it is", new[] { Rule.Allowance, Rule.FixedWait })]
    [InlineData("    // spec-lint: fixed-wait: the test is about a key held for half a second\n\n    await page.waitForTimeout(500);", new[] { Rule.FixedWait })]
    [InlineData("    // spec-lint: fixed-wait: the test is about a key held for half a second\n    await page.waitForTimeout(500);\n    await page.waitForTimeout(500);", new[] { Rule.FixedWait })]
    public void An_allowance_without_its_reason_or_its_statement_allows_nothing(string body, Rule[] expected)
    {
        Assert.Equal(expected, Rules(InTest(body)));
    }
}
