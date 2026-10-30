import type { CSSProperties, ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { cellState, columns, source } from './ThemedGridParts';

// A theme is Visual Tokens set on any element around the grid. light-dark() follows the page's colour scheme.
const theme: CSSProperties & Record<`--ex-${string}`, string> = {
    '--ex-background': 'light-dark(#fbfcfe, #0f1620)',
    '--ex-color': 'light-dark(#1d2733, #d8e1ea)',
    '--ex-pinned-background': 'light-dark(#fbfcfe, #0f1620)',
    '--ex-header-background': 'light-dark(#1f3a5f, #1a2a3d)',
    '--ex-header-color': '#f2f6fb',
    '--ex-row-rule-color': 'light-dark(#e3e8ef, #1f2b38)',
    '--ex-row-stripe-background': 'light-dark(rgba(31, 58, 95, 0.05), rgba(120, 160, 210, 0.06))',
    '--ex-selection-fill': 'rgba(47, 129, 247, 0.16)',
    '--ex-focus-outline': '#2f81f7',
    '--ex-tone-positive-color': 'light-dark(#1a7f37, #4ac26b)',
    '--ex-tone-negative-color': 'light-dark(#cf222e, #ff7b72)',
};

export default function ThemedGrid(): ReactElement {
    return (
        <div style={theme}>
            <ExGrid source={source} columns={columns} cellState={cellState} pinnedColumnCount={1} stripeRows viewportHeight={300} viewportWidth="stretch" />
        </div>
    );
}
