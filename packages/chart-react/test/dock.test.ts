// @vitest-environment happy-dom
// The Chart Dock's contract run against a dock of plain elements (ADR-0390 section 7): what a Consumer's own design
// system writes over useChartDock, with nothing of a Wrapper's. Each Wrapper runs the same contract against its own.
import { createElement, type ReactElement } from 'react';
import { DockedChart } from '../src/chart-dock';
import { chartDockContract, type ContractDockProps, type Rate } from './chart-dock-contract';
import { plainChrome } from './plain-chrome';

function PlainDock({ dock, bridge, children }: ContractDockProps): ReactElement {
    const { charts, active, refusal } = dock;
    return createElement('div', null, children,
        charts.length === 0 && refusal === undefined ? null : createElement('section', { role: 'region', 'aria-label': 'Charts' },
            refusal === undefined ? null : createElement('p', null, refusal,
                createElement('button', { type: 'button', 'aria-label': 'Dismiss', onClick: dock.dismiss }, '×')),
            createElement('div', { role: 'tablist' }, ...charts.map(({ name }) => createElement('span', { key: name },
                createElement('button', { type: 'button', role: 'tab', 'aria-selected': name === active, onClick: () => { dock.select(name); } }, name),
                createElement('button', { type: 'button', 'aria-label': `Close ${name}`, onClick: () => { dock.close(name); } }, '×')))),
            ...charts.map(target => createElement('div', { key: target.name, role: 'tabpanel', hidden: target.name !== active },
                createElement(DockedChart<Rate>, { bridge, target, width: 320, height: 200, chrome: plainChrome, refused: why => why })))));
}

chartDockContract('a dock of plain elements', PlainDock);
