// The Mantine Chrome for a Sheet (ADR-0390 section 3): ExGrid's Mantine Chrome in the slots the Sheet hands its grid,
// and Mantine's components in the Sheet's own: each Toolbar Item, Format Cells, and the Sheet Tabs' faces. What each
// shows and means is the Sheet's, reached through its slots' contexts and ExSheet for React's hooks.

import { createElement, useContext, type ReactElement } from 'react';
import { Text } from '@mantine/core';
import { MantineSizeContext, mantineChrome } from '@ex-grid/mantine';
import type { SheetFormatCellsSlotContext, SheetTabSlotContext, SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import type { ExSheetChrome } from '@ex-grid/sheet-react';
import { MantineFormatCells } from './format-cells';
import { MantineToolbarItem } from './toolbar-item';

/** A Sheet Tab's face: the Sheet's name in Mantine's text, in the theme's primary on the Sheet shown. The tab around
 * it, which takes the keyboard and hears the press, is the Sheet's. */
function MantineSheetTab({ context }: { readonly context: SheetTabSlotContext }): ReactElement {
    const size = useContext(MantineSizeContext);
    return createElement(Text<'span'>, {
        span: true, size, className: 'mantine-ex-sheet-tab-face', ...(context.selected ? { fw: 600, c: 'var(--mantine-primary-color-filled)' } : {}),
    }, context.name);
}

/** The + button's face: a plus drawn in the text's colour. */
function MantineSheetTabAdd(): ReactElement {
    return createElement('svg', {
        className: 'mantine-ex-sheet-tab-add-face', viewBox: '0 0 16 16', 'aria-hidden': true, focusable: false, fill: 'none', stroke: 'currentColor',
        strokeWidth: 1.5, strokeLinecap: 'round',
    }, createElement('path', { d: 'M8 3v10M3 8h10' }));
}

/** Every Chrome slot of a Sheet drawn in Mantine's components: ExGrid's Mantine Chrome for the grid's, and the Sheet's
 * own. What {@link MantineExSheet} hands its Sheet. A Consumer that frames an ExSheet itself spreads it into the Sheet's
 * `chrome`, inside an element of the classes `mantine-ex-grid mantine-ex-sheet` and a `MantineSizeContext`, as
 * MantineExSheet draws them. */
export const mantineSheetChrome: ExSheetChrome = Object.freeze({
    ...mantineChrome,
    toolbarItem: (context: SheetToolbarItemSlotContext) => createElement(MantineToolbarItem, { context }),
    formatCells: (context: SheetFormatCellsSlotContext) => createElement(MantineFormatCells, { context }),
    sheetTab: (context: SheetTabSlotContext) => createElement(MantineSheetTab, { context }),
    sheetTabAdd: () => createElement(MantineSheetTabAdd),
});
