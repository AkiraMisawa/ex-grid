// The Mantine Chrome (ADR-0390 section 3): Mantine's components in each of ExGrid for React's Chrome slots. What a
// slot shows, its order, what a key means and what a choice does are the core's, reached through the React adapter's
// hooks; these components only draw it in Mantine's look, at the size the grid's density maps onto. The frames they
// stand in, the popovers and the boxes over a cell, are the core's, so Mantine's own Menu, Popover and Combobox,
// which position themselves, are not used inside the grid.

import { createContext, createElement, useCallback, useContext, type ReactElement } from 'react';
import { Button, Checkbox, Loader, NativeSelect, Radio, Text, TextInput, type MantineSize } from '@mantine/core';
import {
    filterPanelLabelIds, findPanelLabelIds, operatorLabelId, summaryLabelIds, type FilterOperator, type FilterPanelCondition,
    type GridCellEditorSlotContext, type GridCellMessageSlotContext, type GridEditorCompletionSlotContext, type GridFilterPanelSlotContext,
    type GridFindPanelSlotContext, type GridFormulaBarTextSlotContext, type GridLoadingIndicatorSlotContext, type GridMenuSlotContext, type GridNameBoxSlotContext,
    type GridSelectionSummarySlotContext,
} from '@ex-grid/core';
import {
    useCellEditor, useEditorCompletion, useFilterPanel, useFindPanel, useFormulaBarText, useMenuItems, useNameBox, type ExGridChrome,
    type FilterPanelHandles,
} from '@ex-grid/react';

/** The size Mantine's components are drawn at inside a grid: the one its density maps onto, handed down from
 * MantineExGrid through React's context, which a portal keeps. A frame of the Consumer's own that draws
 * {@link mantineChrome} gives it, as it gives the class `mantine-ex-grid` the Wrapper's stylesheet is scoped to. */
export const MantineSizeContext = createContext<MantineSize>('xs');

/** A menu, the column menu's or the Context Menu's: one of Mantine's subtle buttons per command, as its Menu draws
 * an item. */
function MantineMenu({ context }: { readonly context: GridMenuSlotContext }): ReactElement {
    const item = useMenuItems(context);
    const size = useContext(MantineSizeContext);
    return createElement('div', { role: 'none', className: 'mantine-ex-grid-menu' },
        ...context.commands.map((command, index) => createElement(Button<'button'>, {
            ...item(index), key: command.id, type: 'button', variant: 'subtle', color: 'gray', size, fullWidth: true,
            justify: 'flex-start', className: 'mantine-ex-grid-menu-item',
        }, context.label(command.id))));
}

/** One condition of the condition form: its operator, and its value where the operator takes one. */
function conditionControls(context: GridFilterPanelSlotContext, condition: FilterPanelCondition | undefined, allowed: readonly FilterOperator[],
    second: boolean, size: MantineSize, handles: FilterPanelHandles): ReactElement[] {
    const choices = allowed.map(operator => ({ value: operator, label: context.label(operatorLabelId(operator)) }));
    const operator = createElement(NativeSelect, {
        key: second ? 'second-operator' : 'operator', size,
        data: second ? [{ value: '', label: context.label(filterPanelLabelIds.none) }, ...choices] : choices,
        value: condition === undefined ? '' : condition.operator,
        onChange: event => {
            const { value } = event.currentTarget;
            if (second && value === '') context.setSecond(undefined);
            else if (second) context.setSecond(operatorOf(value, allowed));
            else context.setOperator(operatorOf(value, allowed));
        },
        ...(second ? {} : { ref: handles.first }),
    });
    if (condition === undefined || !condition.takesOperand) return [operator];
    const operand = createElement(TextInput, {
        key: second ? 'second-operand' : 'operand', size, type: 'text', value: condition.operand,
        error: condition.refusal,
        onChange: event => { if (second) context.setSecondOperand(event.currentTarget.value); else context.setOperand(event.currentTarget.value); },
        ...(second ? {} : { ref: handles.search }),
    });
    return [operator, operand];
}

/** The operator an option names: one the core offered, never one a changed option claims (ADR-0009, FN-18). */
function operatorOf(value: string, allowed: readonly FilterOperator[]): FilterOperator {
    const found = allowed.find(operator => operator === value);
    if (found === undefined) throw new RangeError(`'${value}' is not an operator this column's filter offers.`);
    return found;
}

/** The filter panel: the value list or the condition form as the core holds it, in Mantine's controls. */
function MantineFilterPanel({ context }: { readonly context: GridFilterPanelSlotContext }): ReactElement {
    const handles = useFilterPanel(context);
    const { view, first, last, search, onValueListKeyDown } = handles;
    const size = useContext(MantineSizeContext);
    // The search box is both the first control and the one E puts the keyboard on: one ref made once, so a render
    // does not hand React a new one, which it would take away and back (useFilterPanel's refs are stable too).
    const searchBox = useCallback((control: HTMLInputElement | null) => { first(control); search(control); }, [first, search]);
    const body = view.kind === 'valueList'
        ? [
            createElement(TextInput, {
                key: 'search', size, type: 'search', value: view.search, placeholder: context.label(filterPanelLabelIds.search),
                'aria-label': context.label(filterPanelLabelIds.search),
                ref: searchBox,
                onChange: event => { context.setSearch(event.currentTarget.value); },
            }),
            view.addOffered
                ? createElement(Checkbox, {
                    key: 'add', size, label: context.label(filterPanelLabelIds.addToCurrent), checked: view.addToCurrent,
                    onChange: event => { context.setAddToCurrent(event.currentTarget.checked); },
                })
                : null,
            createElement('div', { key: 'list', className: 'mantine-ex-grid-filter-values', onKeyDown: onValueListKeyDown },
                createElement(Checkbox, {
                    key: 'all', size, checked: view.selectAll === 'checked', indeterminate: view.selectAll === 'mixed',
                    label: context.label(view.search === '' ? filterPanelLabelIds.selectAll : filterPanelLabelIds.selectAllSearchResults),
                    onChange: () => { context.toggleAll(); },
                }),
                // Keyed by position: two values may read the same (1.001 and 1.004 at two places), and the core's list
                // is the order it stands in.
                ...view.entries.map((each, at) => createElement(Checkbox, {
                    key: at, size, label: each.text, checked: each.checked,
                    onChange: event => { context.setChecked(each.value, event.currentTarget.checked); },
                }))),
        ]
        : [
            ...conditionControls(context, view.first, view.allowed, false, size, handles),
            createElement(Radio.Group, {
                key: 'join', value: view.combinator, size,
                onChange: (value: unknown) => {
                    if (value !== 'and' && value !== 'or') throw new RangeError(`'${String(value)}' joins no two conditions (FN-18).`);
                    context.setCombinator(value);
                },
                children: createElement('div', { className: 'mantine-ex-grid-filter-join' },
                    createElement(Radio, { value: 'and', size, label: context.label(filterPanelLabelIds.and) }),
                    createElement(Radio, { value: 'or', size, label: context.label(filterPanelLabelIds.or) })),
            }),
            ...conditionControls(context, view.second, view.allowed, true, size, handles),
        ];
    // OK before Cancel, as Excel orders them; Cancel, never disabled, is the last control Shift+Tab wraps to.
    return createElement('div', { className: 'mantine-ex-grid-filter' }, ...body,
        createElement('div', { className: 'mantine-ex-grid-filter-actions' },
            createElement(Button<'button'>, {
                type: 'button', size, className: 'ex-popover-closes', disabled: !view.canApply, onClick: () => { context.apply(); },
            }, context.label(filterPanelLabelIds.ok)),
            createElement(Button<'button'>, {
                type: 'button', size, variant: 'default', className: 'ex-popover-closes', ref: last, onClick: () => { context.cancel(); },
            }, context.label(filterPanelLabelIds.cancel))));
}

/** The find panel and its Replace: each change and step handed to the core, Enter left to the core's form. */
function MantineFindPanel({ context }: { readonly context: GridFindPanelSlotContext }): ReactElement {
    const { view, field } = useFindPanel(context);
    const size = useContext(MantineSizeContext);
    const button = (id: string, press: () => void, variant: 'default' | 'filled', disabled = false): ReactElement =>
        createElement(Button<'button'>, { key: id, type: 'button', size, variant, disabled, onClick: press }, context.label(id));
    return createElement('div', { className: 'mantine-ex-grid-find' },
        createElement(TextInput, {
            size, ref: field, value: view.text, 'aria-label': context.label(findPanelLabelIds.field),
            placeholder: context.label(findPanelLabelIds.field), onChange: event => { context.setText(event.currentTarget.value); },
        }),
        createElement(Checkbox, {
            size, label: context.label(findPanelLabelIds.matchCase), checked: view.matchCase,
            onChange: event => { context.setMatchCase(event.currentTarget.checked); },
        }),
        createElement(Checkbox, {
            size, label: context.label(findPanelLabelIds.wholeCell), checked: view.wholeCell,
            onChange: event => { context.setWholeCell(event.currentTarget.checked); },
        }),
        createElement('div', { className: 'mantine-ex-grid-find-actions' },
            button(findPanelLabelIds.previous, context.previous, 'default'),
            button(findPanelLabelIds.next, context.next, 'filled')),
        ...(view.replaceShown ? [
            createElement(TextInput, {
                key: 'replace', size, value: view.replaceText, 'aria-label': context.label(findPanelLabelIds.replaceField),
                placeholder: context.label(findPanelLabelIds.replaceField), onChange: event => { context.setReplaceText(event.currentTarget.value); },
            }),
            createElement('div', { key: 'writes', className: 'mantine-ex-grid-find-actions' },
                button(findPanelLabelIds.replace, context.replace, 'default', view.replaceWaiting),
                button(findPanelLabelIds.replaceAll, context.replaceAll, 'filled', view.replaceWaiting)),
        ] : []),
        createElement(Text<'span'>, { size, role: 'status', className: 'mantine-ex-grid-find-outcome' }, view.outcome));
}

/** The Cell Editor's field, uncontrolled as the hook asks: the core reads it at the commit and marks a Reject on it. */
function MantineCellEditor({ context }: { readonly context: GridCellEditorSlotContext }): ReactElement {
    const { field, defaultValue } = useCellEditor(context);
    return createElement('input', { ref: field, defaultValue, className: 'mantine-ex-editor' });
}

/** The Formula Bar's text field, uncontrolled, dimmed where its text is borrowed. */
function MantineBarText({ context }: { readonly context: GridFormulaBarTextSlotContext }): ReactElement {
    const { view, field } = useFormulaBarText(context);
    return createElement('input', { ref: field, className: 'mantine-ex-formula-bar-text', 'data-borrowed': view.borrowed ? '' : undefined });
}

/** The Name Box's field. */
function MantineNameBox({ context }: { readonly context: GridNameBoxSlotContext }): ReactElement {
    const { field } = useNameBox(context);
    return createElement('input', { ref: field, className: 'mantine-ex-name-box' });
}

/** A cell's message, the core's sentence, in Mantine's text. */
function MantineCellMessage({ context }: { readonly context: GridCellMessageSlotContext }): ReactElement {
    const size = useContext(MantineSizeContext);
    return createElement(Text<'span'>, { size, span: true }, context.message);
}

/** The loading indicator: Mantine's Loader while rows are on their way, and nothing once they are in. */
function MantineLoading({ context }: { readonly context: GridLoadingIndicatorSlotContext }): ReactElement | null {
    const size = useContext(MantineSizeContext);
    return context.loading ? createElement(Loader, { size, type: 'dots' }) : null;
}

/** The Selection Summary's figures, each named and formatted by the core, in Excel's order. */
function MantineSelectionSummary({ context }: { readonly context: GridSelectionSummarySlotContext }): ReactElement {
    const size = useContext(MantineSizeContext);
    const said = context.status === 'pending' ? context.label(summaryLabelIds.pending)
        : context.status === 'declined' ? context.declineReason
        : context.figures.map(figure => `${context.label(figure.labelId)}: ${figure.text}`).join('   ');
    return createElement(Text<'span'>, { size, span: true }, said);
}

/** The completion list and the argument hint (ADR-0051): the chosen candidate in the theme's primary as Mantine's
 * Combobox marks its selected option, and the hint beneath in Mantine's dimmed colour with the argument the caret is
 * in set off. Both are in the cell's font, as the text typed above them is. The box, its place and every key are the
 * core's, so Mantine's Combobox, which keeps its own store and keys, is not used. */
function MantineCompletion({ context }: { readonly context: GridEditorCompletionSlotContext }): ReactElement {
    const { list, option } = useEditorCompletion(context);
    const hint = context.hint?.parts();
    return createElement('div', { className: 'mantine-ex-grid-completion' },
        context.candidates.length === 0 ? null : createElement('div', { ...list, className: 'mantine-ex-grid-completion-list' },
            ...context.candidates.map((candidate, index) => createElement('div', {
                ...option(index), key: index, className: 'mantine-ex-grid-completion-item',
            }, candidate.label))),
        hint === undefined ? null : createElement('div', { className: 'mantine-ex-grid-completion-hint' },
            hint[0], hint[1].length === 0 ? null : createElement('strong', null, hint[1]), hint[2]));
}

const menu = (context: GridMenuSlotContext): ReactElement => createElement(MantineMenu, { context });

/** Every Chrome slot drawn in Mantine's components: what {@link MantineExGrid} hands its grid. A Consumer that frames
 * an ExGrid itself spreads it into the grid's `chrome`, inside an element of the class `mantine-ex-grid` and a
 * {@link MantineSizeContext}, as MantineExGrid draws them. */
export const mantineChrome: ExGridChrome<unknown> = Object.freeze({
    columnMenu: menu,
    contextMenu: menu,
    filterPanel: (context: GridFilterPanelSlotContext) => createElement(MantineFilterPanel, { context }),
    findPanel: (context: GridFindPanelSlotContext) => createElement(MantineFindPanel, { context }),
    cellEditor: (context: GridCellEditorSlotContext) => createElement(MantineCellEditor, { context }),
    cellMessage: (context: GridCellMessageSlotContext) => createElement(MantineCellMessage, { context }),
    loadingIndicator: (context: GridLoadingIndicatorSlotContext) => createElement(MantineLoading, { context }),
    selectionSummary: (context: GridSelectionSummarySlotContext) => createElement(MantineSelectionSummary, { context }),
    formulaBarText: (context: GridFormulaBarTextSlotContext) => createElement(MantineBarText, { context }),
    nameBox: (context: GridNameBoxSlotContext) => createElement(MantineNameBox, { context }),
    editorCompletion: (context: GridEditorCompletionSlotContext) => createElement(MantineCompletion, { context }),
});
