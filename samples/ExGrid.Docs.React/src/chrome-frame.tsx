// What a React Example runs inside under each Chrome the site offers (ADR-0390 section 8): nothing for the built-in
// Chrome, and for a Wrapper the provider and the stylesheets a Consumer's application puts above it once, as the site
// puts MudBlazor's above its Blazor Examples. The Example's own code is the grid and what it says; this is the page's.
import type { ReactElement, ReactNode } from 'react';

/** The Chrome the site's switch names in the frame's address. */
export type FrameChrome = 'builtin' | 'mantine' | 'mui';

/** Reads the Chrome from the address, refusing one the site never writes. */
export function chromeOf(value: string | null): FrameChrome {
    if (value === 'builtin' || value === 'mantine' || value === 'mui') return value;
    throw new Error(`The Chrome is '${String(value)}': open this page with ?chrome=builtin, mantine or mui, as the site does.`);
}

/** The element an Example runs inside, its design system's stylesheets loaded first, in the site's scheme. */
export async function chromeFrame(chrome: FrameChrome, scheme: 'light' | 'dark'): Promise<(example: ReactNode) => ReactElement> {
    switch (chrome) {
        case 'builtin':
            return example => <>{example}</>;
        case 'mantine': {
            await import('@mantine/core/styles.css');
            await import('../../../src/npm/mantine/styles.css');
            // The Sheet's, scoped under its own Paper: a grid's Example is untouched by it.
            await import('../../../src/npm/sheet-mantine/styles.css');
            const { MantineProvider } = await import('@mantine/core');
            return example => <MantineProvider forceColorScheme={scheme}>{example}</MantineProvider>;
        }
        case 'mui': {
            await import('../../../src/npm/mui/styles.css');
            const { ThemeProvider, createTheme } = await import('@mui/material/styles');
            // MuiExGrid reads the theme's colours as CSS variables, so the theme is made with them, in both schemes.
            const theme = createTheme({ cssVariables: { colorSchemeSelector: 'class' }, colorSchemes: { light: true, dark: true } });
            return example => <ThemeProvider theme={theme} defaultMode={scheme} storageManager={null}>{example}</ThemeProvider>;
        }
    }
}
