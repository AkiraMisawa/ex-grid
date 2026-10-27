// The contract every Wrapper keeps with ExPivot's Chrome slots (ADR-0390 section 7): run at layer 2 against each one
// alike, the test Chrome of plain controls here and each design system's Wrapper. A menu draws the pivot's commands
// in the pivot's order, each enabled and marked as the pivot says, with the pivot's words, and the first enabled one
// takes the keyboard; a panel draws the pivot's draft and hands each choice back, OK applying what the pivot makes of
// it and saying the pivot's refusal; and the Wrapper decides nothing: the frames and Escape are the pivot's, and the
// report's grid is dressed by the same Chrome's grid slots, in the pivot's words.
import { createElement, type ComponentType, type ReactNode } from 'react';
import { act, cleanup, render, within } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { RecordPivotSource, createPivotLayout, pivotRecordField, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '../src/ex-pivot';
import type { ExPivotChrome } from '../src/chrome';

/** What a Wrapper's contract run needs around the pivot: a provider its components read, as a Consumer's page has. */
export interface PivotContractHost {
    /** Wraps the rendered pivot, as `@testing-library/react`'s `wrapper` does. */
    readonly wrapper?: ComponentType<{ readonly children: ReactNode }>;
}

interface Trade {
    readonly region: string;
    readonly desk: string;
    readonly amount: number;
}

const trades: readonly Trade[] = [
    { region: 'EU', desk: 'Rates', amount: 10 },
    { region: 'EU', desk: 'Credit', amount: 20 },
    { region: 'US', desk: 'Rates', amount: 5 },
    { region: 'US', desk: 'FX', amount: 7 },
];

const fields = [
    pivotRecordField<Trade>({ name: 'Region', type: 'text', value: trade => trade.region }),
    pivotRecordField<Trade>({ name: 'Desk', type: 'text', value: trade => trade.desk }),
    pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: trade => trade.amount }),
];

/** The report's work yields to microtasks only, so what a test reads never depends on how fast it ran. */
const slicing = Object.freeze({ yield: () => Promise.resolve() });

/** Lets every answer already given land, and the renders they ask of React run. */
async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 3; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

/** A press as the browser makes one: the control's own click, inside React's act. */
async function press(control: HTMLElement): Promise<void> {
    await act(async () => { control.click(); await Promise.resolve(); });
    await settled();
}

/** Types `text` as the browser does: through the field's own value setter, which a framework's change tracking reads,
 * and an input event after it. */
async function typeText(field: HTMLInputElement, text: string): Promise<void> {
    await act(async () => {
        if (!Reflect.set(HTMLInputElement.prototype, 'value', text, field)) throw new Error('This document\'s fields have no value setter.');
        field.dispatchEvent(new Event('input', { bubbles: true }));
        await Promise.resolve();
    });
}

/** Picks the option of a native select whose text reads `words`, as the browser does. */
async function choose(select: HTMLSelectElement, words: string): Promise<void> {
    const option = [...select.options].find(each => each.textContent.trim() === words);
    if (option === undefined) throw new Error(`The select offers no ${words}.`);
    await act(async () => {
        if (!Reflect.set(HTMLSelectElement.prototype, 'value', option.value, select)) throw new Error('This document\'s selects have no value setter.');
        select.dispatchEvent(new Event('change', { bubbles: true }));
        await Promise.resolve();
    });
}

interface Mounted {
    readonly root: HTMLElement;
    readonly layouts: PivotLayout[];
}

const start = createPivotLayout({ rows: ['Region'], values: ['Amount'], filters: ['Desk'] });

async function mounted(chrome: ExPivotChrome, host: PivotContractHost): Promise<Mounted> {
    const layouts: PivotLayout[] = [];
    const { container } = render(createElement(ExPivot, {
        source: new RecordPivotSource(trades, fields), layout: start, slicing, locale: 'en-US', chrome, viewportWidth: 600, viewportHeight: 320,
        onLayoutChanged: event => { layouts.push(event.layout); },
    }), host);
    const root = container.querySelector('.ex-pivot');
    if (!(root instanceof HTMLElement)) throw new Error('The pivot drew no root.');
    await settled();
    return { root, layouts };
}

/** The pivot's open frame of `role`: a menu or a panel's dialog. */
function frameOf(root: HTMLElement, role: 'menu' | 'dialog'): HTMLElement {
    const frame = root.querySelector<HTMLElement>(`.ex-pivot-popup[role=${role}]`);
    if (frame === null) throw new Error(`No ${role} of the pivot's stands.`);
    return frame;
}

/** The commands a menu's contents drew, in their order. */
function commandsOf(frame: HTMLElement): HTMLElement[] {
    return [...frame.querySelectorAll<HTMLElement>('[role=menuitem], [role=menuitemradio]')];
}

function commandOf(frame: HTMLElement, words: string): HTMLElement {
    const command = commandsOf(frame).find(each => each.textContent.trim() === words);
    if (command === undefined) throw new Error(`The menu draws no ${words}.`);
    return command;
}

function isDisabled(control: HTMLElement): boolean {
    return control.matches(':disabled') || control.getAttribute('aria-disabled') === 'true';
}

function buttonOf(within_: HTMLElement, words: string): HTMLButtonElement {
    const button = [...within_.querySelectorAll('button')].find(each => each.textContent.trim() === words);
    if (button === undefined) throw new Error(`No button reads ${words}.`);
    return button;
}

/** Opens a placed field's menu from its entry in the Field List. */
async function fieldMenu(root: HTMLElement, caption: string): Promise<HTMLElement> {
    await press(within(root).getByRole('button', { name: `Options for ${caption}` }));
    return frameOf(root, 'menu');
}

/** Opens a panel from a placed field's menu. */
async function panel(root: HTMLElement, caption: string, command: string): Promise<HTMLElement> {
    await press(commandOf(await fieldMenu(root, caption), command));
    return frameOf(root, 'dialog');
}

/** Runs ExPivot's slot contract against `chrome`, inside `host`. */
export function pivotSlotContract(name: string, chrome: ExPivotChrome, host: PivotContractHost = {}): void {
    if (chrome.grid?.contextMenu === undefined) throw new Error('A Wrapper\'s Chrome dresses the report\'s grid: its grid slots include the Context Menu.');
    describe(`ExPivot's slot contract, ${name}`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0061: the Layout menu draws the pivot\'s choices in its order and words, the current marked, one that changes nothing disabled, the first enabled holding the keyboard', async () => {
            const { root } = await mounted(chrome, host);
            await press(within(root).getByRole('button', { name: /^Layout/ }));
            const menu = frameOf(root, 'menu');
            const commands = commandsOf(menu);
            expect(commands.map(command => command.textContent.trim())).toEqual([
                'Do Not Show Subtotals', 'Show all Subtotals at Bottom of Group', 'Show all Subtotals at Top of Group',
                'Off for Rows and Columns', 'On for Rows and Columns', 'On for Rows Only', 'On for Columns Only',
                'Show in Compact Form', 'Show in Outline Form', 'Show in Tabular Form', 'Repeat All Item Labels', 'Do Not Repeat Item Labels',
            ]);
            const compact = commandOf(menu, 'Show in Compact Form');
            expect([compact.getAttribute('role'), compact.getAttribute('aria-checked'), isDisabled(compact)]).toEqual(['menuitemradio', 'true', true]);
            expect(commandOf(menu, 'Show in Outline Form').getAttribute('aria-checked')).toBe('false');
            expect(document.activeElement).toBe(commands.find(command => !isDisabled(command)));
        });

        it('ADR-0061: a choice runs as the pivot runs it: the menu closes, the report is laid out under it and the keyboard is back on Layout', async () => {
            const { root, layouts } = await mounted(chrome, host);
            const opener = within(root).getByRole('button', { name: /^Layout/ });
            await press(opener);
            await press(commandOf(frameOf(root, 'menu'), 'Show in Tabular Form'));
            expect([root.querySelector('.ex-pivot-popup'), layouts.at(-1)?.form, document.activeElement]).toEqual([null, 'tabular', opener]);
        });

        it('ADR-0061: a field\'s menu draws the pivot\'s moves under its entry, each enabled only where it changes something, and Escape in the frame closes it', async () => {
            const { root } = await mounted(chrome, host);
            const menu = await fieldMenu(root, 'Region');
            const drawn = commandsOf(menu).map(command => [command.textContent.trim(), isDisabled(command)]);
            expect(drawn.slice(0, 4)).toEqual([['Move Up', true], ['Move Down', true], ['Move to Beginning', true], ['Move to End', true]]);
            expect(drawn.map(([words]) => words)).toContain('Field Settings…');
            expect(menu.closest('.ex-pivot-entry')).not.toBeNull();
            await act(async () => {
                commandsOf(menu)[4]?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
                await Promise.resolve();
            });
            expect(root.querySelector('.ex-pivot-popup')).toBeNull();
        });

        it('ADR-0061: Field Settings… draws the pivot\'s draft, its first control holding the keyboard, and OK applies the subtotals chosen', async () => {
            const { root, layouts } = await mounted(chrome, host);
            const settings = await panel(root, 'Region', 'Field Settings…');
            expect(settings.contains(document.activeElement)).toBe(true);
            const none = within(settings).getByLabelText('None');
            await press(none);
            await press(buttonOf(settings, 'OK'));
            expect([root.querySelector('.ex-pivot-popup'), layouts.at(-1)?.rows[0]?.subtotals]).toEqual([null, false]);
        });

        it('ADR-0061: in Value Field Settings… a caption not written follows the Aggregation, and a caption taken is refused in the pivot\'s words', async () => {
            const { root, layouts } = await mounted(chrome, host);
            const settings = await panel(root, 'Sum of Amount', 'Value Field Settings…');
            const caption = within(settings).getByLabelText('Custom Name');
            if (!(caption instanceof HTMLInputElement)) throw new Error('The caption is a text field.');
            expect(document.activeElement).toBe(caption);
            const aggregation = within(settings).getByLabelText('Summarize value field by');
            if (!(aggregation instanceof HTMLSelectElement)) throw new Error('The Aggregation is a select.');
            await choose(aggregation, 'Count');
            expect(caption.value).toBe('Count of Amount');
            await typeText(caption, 'Region');
            await press(buttonOf(settings, 'OK'));
            expect([within(settings).queryByText('PivotTable field name already exists.') !== null, frameOf(root, 'dialog'), layouts.length]).toEqual([true, settings, 0]);
            await typeText(caption, 'Trades');
            await press(buttonOf(settings, 'OK'));
            expect([layouts.at(-1)?.values[0]?.caption, layouts.at(-1)?.values[0]?.aggregation]).toEqual(['Trades', 'count']);
        });

        it('ADR-0061, ADR-0060: Filter… lists the field\'s Items ticked, OK hides one unticked, and unticking every Item is refused', async () => {
            const { root, layouts } = await mounted(chrome, host);
            const filter = await panel(root, 'Region', 'Filter…');
            expect(document.activeElement).toBe(within(filter).getByRole('searchbox'));
            const eu = within(filter).getByLabelText('EU');
            const us = within(filter).getByLabelText('US');
            expect([eu, us].map(box => box instanceof HTMLInputElement && box.checked)).toEqual([true, true]);
            await press(eu);
            await press(us);
            expect([isDisabled(buttonOf(filter, 'OK')), within(filter).queryByText('Select at least one item.') !== null]).toEqual([true, true]);
            await press(us);
            await press(buttonOf(filter, 'OK'));
            expect(layouts.at(-1)?.rows[0]?.hiddenItems).toEqual([{ kind: 'text', value: 'EU' }]);
        });

        it('ADR-0390: the report\'s grid is dressed by the Chrome\'s grid slots, its Context Menu in the pivot\'s words and run as the pivot runs it', async () => {
            const { root } = await mounted(chrome, host);
            const grid = root.querySelector<HTMLElement>('.ex-pivot-sheet [role=grid]');
            const viewport = grid?.querySelector('.ex-viewport');
            if (grid === null || viewport === null || viewport === undefined) throw new Error('No report grid stands.');
            await act(async () => {
                grid.focus();
                for (const type of ['mousedown', 'contextmenu']) {
                    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 2 });
                    Object.defineProperty(event, 'offsetX', { value: 10 });
                    Object.defineProperty(event, 'offsetY', { value: 10 });
                    viewport.dispatchEvent(event);
                }
                await Promise.resolve();
            });
            await settled();
            const menu = grid.querySelector<HTMLElement>(':scope > .ex-popover[role=menu]');
            if (menu === null) throw new Error('No Context Menu stands over the report.');
            const items = [...menu.querySelectorAll<HTMLElement>('[role=menuitem]')];
            const hide = items.at(-1);
            if (hide === undefined) throw new Error('The Context Menu draws no command.');
            expect(hide.textContent.trim()).toBe('Hide Field List');
            await press(hide);
            expect(root.querySelector<HTMLElement>('.ex-pivot-field-list')?.hidden).toBe(true);
        });

        it('ADR-0061: Filter… opens under a report filter\'s button too, over the report', async () => {
            const { root } = await mounted(chrome, host);
            await press(within(root).getByRole('button', { name: 'Filter Desk' }));
            const filter = frameOf(root, 'dialog');
            expect([filter.classList.contains('ex-pivot-popup-overlay'), within(filter).queryByLabelText('Rates') !== null]).toEqual([true, true]);
        });
    });
}
