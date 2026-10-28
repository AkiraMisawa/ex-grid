import type { CSSProperties, ReactElement } from 'react';
import { MantineExGrid } from '@ex-grid/mantine';
import { cellState, columns, source } from './ThemedGridParts';

// @ex-grid/mantine/styles.css already maps the tokens onto the theme; a token on the Wrapper overrides one of them.
const theme: CSSProperties & Record<`--ex-${string}`, string> = {
    '--ex-header-background': 'var(--mantine-primary-color-filled)',
    '--ex-header-color': 'var(--mantine-color-white)',
    '--ex-selection-fill': 'color-mix(in srgb, var(--mantine-primary-color-filled) 14%, transparent)',
};

export default function ThemedGridMantine(): ReactElement {
    return (
        <MantineExGrid withBorder striped style={theme} source={source} columns={columns} cellState={cellState} pinnedColumnCount={1}
            viewportHeight={300} viewportWidth="stretch" />
    );
}
