// The Chart Dock under Mantine (ADR-0258, ADR-0281, ADR-0390): the grid it is given, and a panel beside it or below
// it, never over it, holding the charts made from the grid's selection as Mantine's tabs. What it shows is the dock's
// state, `useChartDock`'s, which takes the bridge's events; this component draws that state and calls back.

import { createElement, type CSSProperties, type KeyboardEvent, type ReactElement, type ReactNode } from 'react';
import { Alert, CloseButton, Paper, Tabs } from '@mantine/core';
import type { GridDensity } from '@ex-grid/core';
import type { GridChartBridge } from '@ex-grid/chart';
import { DockedChart, useChartDockKeyboard, type ChartDockPlacement, type ChartDockState, type ExChartChrome } from '@ex-grid/chart-react';
import { MantineSizeContext, mantineSizeOf } from '@ex-grid/mantine';
import { mantineChartChrome } from './chart-chrome';

// Why a tab's chart can no longer be drawn, in Mantine's alert.
const refused = (why: string): ReactElement => createElement(Alert, { color: 'yellow', variant: 'light', className: 'mantine-ex-chart-dock-refusal', p: 'xs' }, why);

/** A MantineChartDock's props. */
export interface MantineChartDockProps<Row> {
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
     * the grid's Chrome's (ADR-0390); compact, `xs`, unless given. */
    readonly density?: GridDensity;
    /** What draws each chart's toolbar and context menu; {@link mantineChartChrome} unless given. */
    readonly chrome?: ExChartChrome;
    /** A class for the dock's own element. */
    readonly className?: string;
    /** A style for the dock's own element. */
    readonly style?: CSSProperties;
}

/**
 * The Chart Dock (ADR-0258) drawn with Mantine: the grid, and a panel of Mantine's tabs beside it or below it holding
 * the charts made from its selection. The context menu's "Chart selection" opens a tab directly, "Add to chart"
 * offers every open tab, a refusal is said in an alert above the tabs, and a click on a point shows its cell. Needs a
 * MantineProvider above it and `@ex-grid/chart-mantine/styles.css` loaded after Mantine's styles.
 */
export function MantineChartDock<Row>(props: MantineChartDockProps<Row>): ReactElement {
    const {
        dock, bridge, children, placement = 'right', chartWidth = 640, chartHeight = 320, density = 'compact', chrome = mantineChartChrome, className, style,
    } = props;
    const keyboard = useChartDockKeyboard(dock);
    const { charts, active, refusal } = dock;
    const classes = ['mantine-ex-chart', 'mantine-ex-chart-dock', `mantine-ex-chart-dock-${placement}`];
    if (className !== undefined) classes.push(className);
    // Beside the grid, the panel's content is as wide as its charts and the grid takes what is left, so a chart is
    // never clipped by a narrow page. Content-box, so the stylesheet's padding is not paired here.
    const panelStyle: CSSProperties | undefined = placement === 'right' ? { flex: '0 0 auto', boxSizing: 'content-box', width: chartWidth } : undefined;
    const panel = charts.length === 0 && refusal === undefined ? null : createElement(Paper<'div'>, {
        ref: keyboard.panel, withBorder: true, className: 'mantine-ex-chart-dock-panel', role: 'region', 'aria-label': 'Charts', ...(panelStyle === undefined ? {} : { style: panelStyle }),
    },
    refusal === undefined ? null : createElement(Alert, {
        color: 'yellow', variant: 'light', withCloseButton: true, closeButtonLabel: 'Dismiss', onClose: dock.dismiss, className: 'mantine-ex-chart-dock-refusal', p: 'xs',
    }, refusal),
    charts.length === 0 ? null : createElement(Tabs, {
        // A tab not shown keeps its chart, and with it the range narrowed and the filters open: hidden by display,
        // not by Activity, whose hidden mode runs the chart's cleanup and would draw it anew on its return.
        value: active ?? null, keepMounted: true, keepMountedMode: 'display-none', className: 'mantine-ex-chart-dock-tabs', // oxlint-disable-line ex-grid/no-stand-in-default -- Mantine's Tabs say "no tab" as null
        onChange: (value: string | null) => { if (value !== null) dock.select(value); },
    },
    createElement(Tabs.List, null, ...charts.map(({ name }) => createElement(Tabs.Tab, {
        key: name, value: name,
        // Delete closes the tab the keyboard is on, as the close mark does under the pointer.
        onKeyDown: (event: KeyboardEvent) => { if (event.key === 'Delete') keyboard.closeByKey(name); },
        // A span, not a button: a button inside the tab's button is not one the browser lets stand.
        rightSection: createElement(CloseButton<'span'>, {
            component: 'span', size: 'xs', role: 'button', 'aria-label': `Close ${name}`, className: 'mantine-ex-chart-dock-close',
            onClick: event => { event.stopPropagation(); dock.close(name); },
        }),
    }, name))),
    ...charts.map(target => createElement(Tabs.Panel, {
        key: target.name, value: target.name, className: 'mantine-ex-chart-dock-tab',
        children: createElement(DockedChart<Row>, {
            bridge, target, width: chartWidth, height: chartHeight, chrome,
            refused,
        }),
    }))));
    return createElement('div', { className: classes.join(' '), ...(style === undefined ? {} : { style }) },
        createElement('div', { ref: keyboard.content, className: 'mantine-ex-chart-dock-content' }, children),
        createElement(MantineSizeContext.Provider, { value: mantineSizeOf(density) }, panel));
}
