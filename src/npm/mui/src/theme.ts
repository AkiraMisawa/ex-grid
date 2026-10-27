// The theme a MUI Wrapper reads (ADR-0390 sections 5 and 10): its colours as the CSS variables MUI writes on the page,
// never as script.

/**
 * Refuses a theme the family's MUI stylesheets cannot read (ADR-0390 sections 5 and 10): one made without CSS
 * variables, which has no `vars` (MUI's ThemeProvider writes `null` there), and one whose variables carry a prefix
 * other than MUI's own, which the stylesheets' `--mui-*` names would not find. Either way the Wrapper would be drawn
 * in colours that are not the theme's. `who` names the Wrapper in the refusal. Every MUI Wrapper of the family calls
 * it with the theme `useTheme()` answers.
 */
export function refuseThemeWithoutVariables(theme: object, who: string): void {
    const vars: unknown = Reflect.get(theme, 'vars');
    if (vars === undefined || vars === null) {
        throw new Error(`${who} needs a MUI theme with CSS variables: make it with createTheme({ cssVariables: true }) and hand it to the `
            + `ThemeProvider above it. Without them the theme's colours exist only in script, and ${who} would be drawn in colours `
            + 'that are not the theme\'s (ADR-0390 section 5).');
    }
    const prefix: unknown = Reflect.get(theme, 'cssVarPrefix');
    if (prefix !== 'mui') {
        throw new Error(`${who} reads the theme's CSS variables by MUI's own prefix, --mui-*, and this theme writes them with the prefix '${String(prefix)}': `
            + 'leave cssVariables.cssVarPrefix unset (ADR-0390 section 10).');
    }
}
