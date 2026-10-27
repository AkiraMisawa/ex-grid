// The Chart Dock under MUI (ADR-0258, ADR-0281, ADR-0390): the grid it is given, and a panel beside it or below it,
// never over it, holding the charts made from the grid's selection as MUI's tabs. What it shows is the dock's state,
// `useChartDock`'s, which takes the bridge's events; this component draws that state and calls back.

import { createElement, useId, type CSSProperties, type KeyboardEvent, type ReactElement, type ReactNode } from 'react';
import { Alert, IconButton, Paper, Tab, Tabs } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import type { GridDensity } from '@ex-grid/core';
import type { GridChartBridge } from '@ex-grid/chart';
import { DockedChart, useChartDockKeyboard, type ChartDockPlacement, type ChartDockState, type ExChartChrome } from '@ex-grid/chart-react';
import { MuiSizeContext, muiSizeOf, refuseThemeWithoutVariables } from '@ex-grid/mui';
import { muiChartChrome } from './chart-chrome';

// Why a tab's chart can no longer be drawn, in MUI's alert.
const refused = (why: string): ReactElement => createElement(Alert, { severity: 'warning', className: 'mui-ex-chart-dock-refusal' }, why);

// The close mark's cross, drawn rather than written, so a tab's text is its name alone.
const cross = createElement('svg', { viewBox: '0 0 16 16', width: 12, height: 12, 'aria-hidden': true, focusable: false },
    createElement('path', { d: 'M4 4l8 8M12 4l-8 8', stroke: 'currentColor', strokeWidth: 1.75, strokeLinecap: 'round' }));

/** A MuiChartDock's props. */
export interface MuiChartDockProps<Row> {
    /** The dock's state, from `useChartDock`, whose `bridgeOptions` the bridge was made with. */
    readonly dock: ChartDockState;
    /** The bridge of the grid in `children`, which draws each chart from the grid's rows. */
    readonly bridge: GridChartBridge<Row>;
    /** The grid, and anything to stand beside the charts. */
    readonly children?: ReactNode;
    /** Where the charts go: beside the grid, the default, or below it. */
    readonly placement?: ChartDockPlacement;
    /** Each chart's width in CSS pixels; 640 unless given. */
    readonly chartWidth?: number;
    /** Each chart's height in CSS pixels; 320 unless given. */
    readonly chartHeight?: number;
    /** The grid's Density, which picks the size the charts' toolbar, Chart Filters and menu are drawn at, as it picks
     * the grid's Chrome's (ADR-0390); compact, `small` and dense, unless given. */
    readonly density?: GridDensity;
    /** What draws each chart's toolbar and context menu; {@link muiChartChrome} unless given. */
    readonly chrome?: ExChartChrome;
    /** A class for the dock's own element. */
    readonly className?: string;
    /** A style for the dock's own element. */
    readonly style?: CSSProperties;
}

/**
 * The Chart Dock (ADR-0258) drawn with MUI: the grid, and a panel of MUI's tabs beside it or below it holding the
 * charts made from its selection. The context menu's "Chart selection" opens a tab directly, "Add to chart" offers
 * every open tab, a refusal is said in an alert above the tabs, and a click on a point shows its cell. A tab not shown
 * keeps its chart, with its range and its open Chart Filters. Needs a ThemeProvider whose theme was made with
 * `cssVariables: true` above it, and `@ex-grid/chart-mui/styles.css` loaded.
 */
export function MuiChartDock<Row>(props: MuiChartDockProps<Row>): ReactElement {
    const {
        dock, bridge, children, placement = 'right', chartWidth = 640, chartHeight = 320, density = 'compact', chrome = muiChartChrome, className, style,
    } = props;
    const ids = useId();
    refuseThemeWithoutVariables(useTheme(), 'MuiChartDock');
    const keyboard = useChartDockKeyboard(dock);
    const { charts, active, refusal } = dock;
    const classes = ['mui-ex-chart', 'mui-ex-chart-dock', `mui-ex-chart-dock-${placement}`];
    if (className !== undefined) classes.push(className);
    // Beside the grid, the panel's content is as wide as its charts and the grid takes what is left, so a chart is
    // never clipped by a narrow page. Content-box, so the stylesheet's padding is not paired here.
    const panelStyle: CSSProperties | undefined = placement === 'right' ? { flex: '0 0 auto', boxSizing: 'content-box', width: chartWidth } : undefined;
    const tabId = (index: number): string => `${ids}-tab-${String(index)}`;
    const panelId = (index: number): string => `${ids}-panel-${String(index)}`;
    const panel = charts.length === 0 && refusal === undefined ? null : createElement(Paper, {
        ref: keyboard.panel, variant: 'outlined', className: 'mui-ex-chart-dock-panel', role: 'region', 'aria-label': 'Charts',
        ...(panelStyle === undefined ? {} : { style: panelStyle }),
    },
    refusal === undefined ? null : createElement(Alert, {
        severity: 'warning', onClose: dock.dismiss, closeText: 'Dismiss', className: 'mui-ex-chart-dock-refusal',
    }, refusal),
    charts.length === 0 ? null : createElement(Tabs, {
        value: active ?? false, variant: 'scrollable', scrollButtons: 'auto', className: 'mui-ex-chart-dock-tabs', // oxlint-disable-line ex-grid/no-stand-in-default -- MUI's Tabs say "no tab" as false
        onChange: (_: unknown, value: unknown) => {
            if (typeof value !== 'string') throw new TypeError('A Chart Dock\'s tab is named by its chart.');
            dock.select(value);
        },
    }, ...charts.map(({ name }, index) => createElement(Tab, {
        key: name, value: name, id: tabId(index), 'aria-controls': panelId(index), className: 'mui-ex-chart-dock-tab-button',
        // Delete closes the tab the keyboard is on, as the close mark does under the pointer.
        onKeyDown: (event: KeyboardEvent) => { if (event.key === 'Delete') keyboard.closeByKey(name); },
        label: createElement('span', { className: 'mui-ex-chart-dock-label' }, name,
            // A span, not a button: a button inside the tab's button is not one the browser lets stand.
            createElement(IconButton<'span'>, {
                component: 'span', role: 'button', size: 'small', 'aria-label': `Close ${name}`,
                // Off the Tab order: Delete closes from the keyboard, and a stop inside each tab's button would be one
                // the tabs' own roving order does not know.
                tabIndex: -1, className: 'mui-ex-chart-dock-close',
                onClick: event => { event.stopPropagation(); dock.close(name); },
            }, cross)),
    }))),
    ...charts.map((target, index) => createElement('div', {
        key: target.name, role: 'tabpanel', id: panelId(index), 'aria-labelledby': tabId(index), hidden: target.name !== active,
        className: 'mui-ex-chart-dock-tab',
    }, createElement(DockedChart<Row>, { bridge, target, width: chartWidth, height: chartHeight, chrome, refused }))));
    return createElement('div', { className: classes.join(' '), ...(style === undefined ? {} : { style }) },
        createElement('div', { ref: keyboard.content, className: 'mui-ex-chart-dock-content' }, children),
        createElement(MuiSizeContext.Provider, { value: muiSizeOf(density) }, panel));
}
