import type { CSSProperties, ReactElement } from 'react';
import { MuiExGrid } from '@ex-grid/mui';
import { cellState, columns, source } from './ThemedGridParts';

// @ex-grid/mui/styles.css already maps the tokens onto the theme's CSS variables; a token on the Wrapper overrides
// one of them.
const theme: CSSProperties & Record<`--ex-${string}`, string> = {
    '--ex-header-background': 'var(--mui-palette-primary-main)',
    '--ex-header-color': 'var(--mui-palette-primary-contrastText)',
    '--ex-selection-fill': 'color-mix(in srgb, var(--mui-palette-primary-main) 14%, transparent)',
};

export default function ThemedGridMui(): ReactElement {
    return (
        <MuiExGrid stripeRows style={theme} source={source} columns={columns} cellState={cellState} pinnedColumnCount={1}
            viewportHeight={300} viewportWidth="stretch" />
    );
}
