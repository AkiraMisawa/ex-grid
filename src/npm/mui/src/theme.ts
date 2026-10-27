// The one check every MUI Wrapper of the family makes before it draws (ADR-0390 sections 5 and 10): the stylesheets
// read the theme's colours as MUI's CSS variables, so a theme that writes none, or writes them under another prefix,
// would leave the Wrapper drawn in colours that are not the theme's.

/**
 * Refuses a theme the Wrappers' stylesheets cannot read, naming the option to change (ADR-0390 sections 5 and 10): one
 * made without CSS variables, which has no `vars` (MUI's ThemeProvider writes `null` there), and one whose variables
 * carry a prefix other than MUI's own `mui`, which the stylesheets' `--mui-*` names would not find. `who` names the
 * component that refuses it. Call it with `useTheme()` before drawing.
 */
export function refuseThemeWithoutVariables(theme: object, who: string): void {
    const vars: unknown = Reflect.get(theme, 'vars');
    if (vars === undefined || vars === null) {
        throw new Error(`${who} needs a MUI theme with CSS variables: make it with createTheme({ cssVariables: true }) and hand it to the `
            + 'ThemeProvider above it. Without them the theme\'s colours exist only in script, and it would be drawn in colours that are not '
            + 'the theme\'s (ADR-0390 section 5).');
    }
    const prefix: unknown = Reflect.get(theme, 'cssVarPrefix');
    if (prefix !== 'mui') {
        throw new Error(`${who} reads the theme's CSS variables by MUI's own prefix, --mui-*, and this theme writes them with the prefix '${String(prefix)}': `
            + 'leave cssVariables.cssVarPrefix unset (ADR-0390 section 10).');
    }
}
