// The MUI Chrome for a Sheet (ADR-0390 section 3): ExGrid's MUI Chrome in the slots the Sheet hands its grid, and MUI's
// components in the Sheet's own: each Toolbar Item, Format Cells, and the Sheet Tabs' faces. What each shows and means
// is the Sheet's, reached through its slots' contexts and ExSheet for React's hooks.

import { createElement, useContext, type ReactElement } from 'react';
import { Typography } from '@mui/material';
import { MuiSizeContext, muiChrome } from '@ex-grid/mui';
import type { SheetFormatCellsSlotContext, SheetTabSlotContext, SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import type { ExSheetChrome } from '@ex-grid/sheet-react';
import { MuiFormatCells } from './format-cells';
import { MuiToolbarItem } from './toolbar-item';

/** A Sheet Tab's face: the Sheet's name in MUI's text, in the theme's primary on the Sheet shown, as MUI's Tabs mark
 * the tab selected. The tab around it, which takes the keyboard and hears the press, is the Sheet's. */
function MuiSheetTab({ context }: { readonly context: SheetTabSlotContext }): ReactElement {
    const { size } = useContext(MuiSizeContext);
    return createElement(Typography<'span'>, {
        component: 'span', variant: size === 'small' ? 'body2' : 'body1', className: 'mui-ex-sheet-tab-face',
        ...(context.selected ? { color: 'primary', fontWeight: 500 } : {}),
    }, context.name);
}

/** The + button's face: a plus drawn in the text's colour. */
function MuiSheetTabAdd(): ReactElement {
    return createElement('svg', {
        className: 'mui-ex-sheet-tab-add-face', viewBox: '0 0 16 16', 'aria-hidden': true, focusable: false, fill: 'none', stroke: 'currentColor',
        strokeWidth: 1.5, strokeLinecap: 'round',
    }, createElement('path', { d: 'M8 3v10M3 8h10' }));
}

/** Every Chrome slot of a Sheet drawn in MUI's components: ExGrid's MUI Chrome for the grid's, and the Sheet's own.
 * What {@link MuiExSheet} hands its Sheet. A Consumer that frames an ExSheet itself spreads it into the Sheet's
 * `chrome`, inside an element of the classes `mui-ex-grid mui-ex-sheet` and a `MuiSizeContext`, under a ThemeProvider
 * whose theme was made with CSS variables, as MuiExSheet draws them. */
export const muiSheetChrome: ExSheetChrome = Object.freeze({
    ...muiChrome,
    toolbarItem: (context: SheetToolbarItemSlotContext) => createElement(MuiToolbarItem, { context }),
    formatCells: (context: SheetFormatCellsSlotContext) => createElement(MuiFormatCells, { context }),
    sheetTab: (context: SheetTabSlotContext) => createElement(MuiSheetTab, { context }),
    sheetTabAdd: () => createElement(MuiSheetTabAdd),
});
