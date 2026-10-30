import { GridMetrics, GridSource, type GridColumn, type GridPresentationDefaultsOptions } from '@ex-grid/core';
import { samplePositions, type Position } from './Positions';

const whole = new Intl.NumberFormat('en-US');
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export const source = GridSource.from(samplePositions());

export const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id },
    { name: 'Desk', type: 'text', value: p => p.desk },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
    { name: 'Quantity', type: 'number', value: p => p.quantity, format: v => whole.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', format: v => money.format(v as number) },
];

// The widths ExGrid measured its own font at, read at its Standard density's size, as the Blazor Example reads them; the
// grid scales them to the size of whichever density it resolves, the Wrappers' Compact included.
function measuredByExGrid(): Omit<GridPresentationDefaultsOptions, 'density' | 'highlightHoverRow' | 'stripeRows'> {
    const metrics = GridMetrics.resolve({ density: 'standard' });
    const cell = metrics.cellMetrics;
    const { glyphWidths } = cell;
    return {
        fontSizePx: metrics.fontSizePx, wideWidthPx: cell.wideWidthPx, digitWidthPx: cell.digitWidthPx, narrowWidthPx: cell.narrowWidthPx,
        otherWidthPx: cell.otherWidthPx, boldWideWidthPx: cell.boldWideWidthPx, boldDigitWidthPx: cell.boldDigitWidthPx,
        boldNarrowWidthPx: cell.boldNarrowWidthPx, boldOtherWidthPx: cell.boldOtherWidthPx,
        ...(glyphWidths === undefined ? {} : { glyphWidths }),
    };
}

/** A theme that sets the system font instead of its own: the Wrapper takes the family and the widths measured for it
 * as one value, so the font on screen and the widths behind #### come from one hand. The system font is the one
 * ExGrid's own stylesheet paints, so its widths are the core's. */
export const systemFont = Object.freeze({ family: 'system-ui, sans-serif', widths: Object.freeze(measuredByExGrid()) });
