// The MUI Chrome (ADR-0390 section 3): MUI's components in each of ExGrid for React's Chrome slots. What a slot
// shows, its order, what a key means and what a choice does are the core's, reached through the React adapter's
// hooks; these components only draw it in MUI's look, at the size the grid's density maps onto. The frames they stand
// in, the popovers and the boxes over a cell, are the core's, so MUI's own Menu, Popover, Autocomplete and MenuList,
// which position themselves or keep keys of their own, are not used inside the grid.

import { createContext, createElement, useCallback, useContext, type ReactElement } from 'react';
import {
    Button, Checkbox, CircularProgress, FormControlLabel, ListItemButton, NativeSelect, Radio, RadioGroup, TextField, Typography,
} from '@mui/material';
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
import { muiSizeOf, type MuiExGridSize } from './presentation';

/** The size MUI's components are drawn at inside a grid: the one its density maps onto, handed down from MuiExGrid
 * through React's context, which a portal keeps. A frame of the Consumer's own that draws {@link muiChrome} gives it,
 * as it gives the class `mui-ex-grid` the Wrapper's stylesheet is scoped to. */
export const MuiSizeContext = createContext<MuiExGridSize>(muiSizeOf('compact'));

/** A menu, the column menu's or the Context Menu's: one of MUI's list item buttons per command, as its Menu draws an
 * item. MUI's MenuItem is not used: it stands only inside MUI's MenuList, which answers the arrows itself. */
function MuiMenu({ context }: { readonly context: GridMenuSlotContext }): ReactElement {
    const item = useMenuItems(context);
    const { dense } = useContext(MuiSizeContext);
    return createElement('div', { role: 'none', className: 'mui-ex-grid-menu' },
        ...context.commands.map((command, index) => createElement(ListItemButton<'button'>, {
            ...item(index), key: command.id, component: 'button', type: 'button', dense, className: 'mui-ex-grid-menu-item',
        }, context.label(command.id))));
}

/** A checkbox with its label, in the body's smaller text at the small size. */
function labelled(key: string, label: string, control: ReactElement, size: MuiExGridSize['size']): ReactElement {
    return createElement(FormControlLabel, {
        key, label, control, className: 'mui-ex-grid-label',
        slotProps: { typography: { variant: size === 'small' ? 'body2' : 'body1' } },
    });
}

/** One condition of the condition form: its operator, and its value where the operator takes one. */
function conditionControls(context: GridFilterPanelSlotContext, condition: FilterPanelCondition | undefined, allowed: readonly FilterOperator[],
    second: boolean, size: MuiExGridSize['size'], handles: FilterPanelHandles): ReactElement[] {
    const choices = allowed.map(operator => createElement('option', { key: operator, value: operator }, context.label(operatorLabelId(operator))));
    const operator = createElement(NativeSelect, {
        key: second ? 'second-operator' : 'operator', size,
        value: condition === undefined ? '' : condition.operator,
        onChange: event => {
            const { value } = event.target;
            if (second && value === '') context.setSecond(undefined);
            else if (second) context.setSecond(operatorOf(value, allowed));
            else context.setOperator(operatorOf(value, allowed));
        },
        ...(second ? {} : { inputRef: handles.first }),
    }, ...(second ? [createElement('option', { key: '', value: '' }, context.label(filterPanelLabelIds.none)), ...choices] : choices));
    if (condition === undefined || !condition.takesOperand) return [operator];
    const operand = createElement(TextField, {
        key: second ? 'second-operand' : 'operand', size, type: 'text', value: condition.operand,
        error: condition.refusal !== undefined,
        ...(condition.refusal === undefined ? {} : { helperText: condition.refusal }),
        onChange: event => { if (second) context.setSecondOperand(event.target.value); else context.setOperand(event.target.value); },
        ...(second ? {} : { inputRef: handles.search }),
    });
    return [operator, operand];
}

/** The operator an option names: one the core offered, never one a changed option claims (ADR-0009, FN-18). */
function operatorOf(value: string, allowed: readonly FilterOperator[]): FilterOperator {
    const found = allowed.find(operator => operator === value);
    if (found === undefined) throw new RangeError(`'${value}' is not an operator this column's filter offers.`);
    return found;
}

/** The filter panel: the value list or the condition form as the core holds it, in MUI's controls. */
function MuiFilterPanel({ context }: { readonly context: GridFilterPanelSlotContext }): ReactElement {
    const handles = useFilterPanel(context);
    const { view, first, last, search, onValueListKeyDown } = handles;
    const { size } = useContext(MuiSizeContext);
    // The search box is both the first control and the one E puts the keyboard on: one ref made once, so a render
    // does not hand React a new one, which it would take away and back (useFilterPanel's refs are stable too).
    const searchBox = useCallback((control: HTMLInputElement | null) => { first(control); search(control); }, [first, search]);
    const body = view.kind === 'valueList'
        ? [
            createElement(TextField, {
                key: 'search', size, type: 'search', value: view.search, placeholder: context.label(filterPanelLabelIds.search),
                inputRef: searchBox,
                slotProps: { htmlInput: { 'aria-label': context.label(filterPanelLabelIds.search) } },
                onChange: event => { context.setSearch(event.target.value); },
            }),
            view.addOffered
                ? labelled('add', context.label(filterPanelLabelIds.addToCurrent), createElement(Checkbox, {
                    size, checked: view.addToCurrent, onChange: event => { context.setAddToCurrent(event.target.checked); },
                }), size)
                : null,
            createElement('div', { key: 'list', className: 'mui-ex-grid-filter-values', onKeyDown: onValueListKeyDown },
                labelled('all', context.label(view.search === '' ? filterPanelLabelIds.selectAll : filterPanelLabelIds.selectAllSearchResults),
                    createElement(Checkbox, {
                        size, checked: view.selectAll === 'checked', indeterminate: view.selectAll === 'mixed',
                        onChange: () => { context.toggleAll(); },
                    }), size),
                // Keyed by position: two values may read the same (1.001 and 1.004 at two places), and the core's list
                // is the order it stands in.
                ...view.entries.map((each, at) => labelled(String(at), each.text, createElement(Checkbox, {
                    size, checked: each.checked, onChange: event => { context.setChecked(each.value, event.target.checked); },
                }), size))),
        ]
        : [
            ...conditionControls(context, view.first, view.allowed, false, size, handles),
            createElement(RadioGroup, {
                key: 'join', row: true, value: view.combinator,
                onChange: (_event: unknown, value: string) => {
                    if (value !== 'and' && value !== 'or') throw new RangeError(`'${value}' joins no two conditions (FN-18).`);
                    context.setCombinator(value);
                },
            },
            labelled('and', context.label(filterPanelLabelIds.and), createElement(Radio, { size, value: 'and' }), size),
            labelled('or', context.label(filterPanelLabelIds.or), createElement(Radio, { size, value: 'or' }), size)),
            ...conditionControls(context, view.second, view.allowed, true, size, handles),
        ];
    // OK before Cancel, as Excel orders them; Cancel, never disabled, is the last control Shift+Tab wraps to.
    return createElement('div', { className: 'mui-ex-grid-filter' }, ...body,
        createElement('div', { className: 'mui-ex-grid-filter-actions' },
            createElement(Button, {
                type: 'button', size, variant: 'contained', className: 'ex-popover-closes', disabled: !view.canApply, onClick: () => { context.apply(); },
            }, context.label(filterPanelLabelIds.ok)),
            createElement(Button, {
                type: 'button', size, variant: 'outlined', className: 'ex-popover-closes', ref: last, onClick: () => { context.cancel(); },
            }, context.label(filterPanelLabelIds.cancel))));
}

/** The find panel and its Replace: each change and step handed to the core, Enter left to the core's form. */
function MuiFindPanel({ context }: { readonly context: GridFindPanelSlotContext }): ReactElement {
    const { view, field } = useFindPanel(context);
    const { size } = useContext(MuiSizeContext);
    const button = (id: string, press: () => void, variant: 'outlined' | 'contained', disabled = false): ReactElement =>
        createElement(Button, { key: id, type: 'button', size, variant, disabled, onClick: press }, context.label(id));
    const textField = (key: string, labelId: string, value: string, change: (text: string) => void, ref?: (control: HTMLInputElement | null) => void): ReactElement =>
        createElement(TextField, {
            key, size, value, placeholder: context.label(labelId),
            slotProps: { htmlInput: { 'aria-label': context.label(labelId) } },
            onChange: event => { change(event.target.value); },
            ...(ref === undefined ? {} : { inputRef: ref }),
        });
    return createElement('div', { className: 'mui-ex-grid-find' },
        textField('find', findPanelLabelIds.field, view.text, context.setText, field),
        labelled('case', context.label(findPanelLabelIds.matchCase), createElement(Checkbox, {
            size, checked: view.matchCase, onChange: event => { context.setMatchCase(event.target.checked); },
        }), size),
        labelled('whole', context.label(findPanelLabelIds.wholeCell), createElement(Checkbox, {
            size, checked: view.wholeCell, onChange: event => { context.setWholeCell(event.target.checked); },
        }), size),
        createElement('div', { key: 'steps', className: 'mui-ex-grid-find-actions' },
            button(findPanelLabelIds.previous, context.previous, 'outlined'),
            button(findPanelLabelIds.next, context.next, 'contained')),
        ...(view.replaceShown ? [
            textField('replace', findPanelLabelIds.replaceField, view.replaceText, context.setReplaceText),
            createElement('div', { key: 'writes', className: 'mui-ex-grid-find-actions' },
                button(findPanelLabelIds.replace, context.replace, 'outlined', view.replaceWaiting),
                button(findPanelLabelIds.replaceAll, context.replaceAll, 'contained', view.replaceWaiting)),
        ] : []),
        createElement(Typography<'span'>, {
            key: 'outcome', component: 'span', variant: size === 'small' ? 'body2' : 'body1', role: 'status', className: 'mui-ex-grid-find-outcome',
        }, view.outcome));
}

/** The Cell Editor's field, uncontrolled as the hook asks: the core reads it at the commit and marks a Reject on it. */
function MuiCellEditor({ context }: { readonly context: GridCellEditorSlotContext }): ReactElement {
    const { field, defaultValue } = useCellEditor(context);
    return createElement('input', { ref: field, defaultValue, className: 'mui-ex-editor' });
}

/** The Formula Bar's text field, uncontrolled, dimmed where its text is borrowed. */
function MuiBarText({ context }: { readonly context: GridFormulaBarTextSlotContext }): ReactElement {
    const { view, field } = useFormulaBarText(context);
    return createElement('input', { ref: field, className: 'mui-ex-formula-bar-text', 'data-borrowed': view.borrowed ? '' : undefined });
}

/** The Name Box's field. */
function MuiNameBox({ context }: { readonly context: GridNameBoxSlotContext }): ReactElement {
    const { field } = useNameBox(context);
    return createElement('input', { ref: field, className: 'mui-ex-name-box' });
}

/** A cell's message, the core's sentence, in MUI's text. */
function MuiCellMessage({ context }: { readonly context: GridCellMessageSlotContext }): ReactElement {
    const { size } = useContext(MuiSizeContext);
    return createElement(Typography<'span'>, { component: 'span', variant: size === 'small' ? 'body2' : 'body1' }, context.message);
}

/** The loading indicator: MUI's circular progress while rows are on their way, and nothing once they are in. */
function MuiLoading({ context }: { readonly context: GridLoadingIndicatorSlotContext }): ReactElement | null {
    const { size } = useContext(MuiSizeContext);
    return context.loading ? createElement(CircularProgress, { size: size === 'small' ? 16 : 20 }) : null;
}

/** The Selection Summary's figures, each named and formatted by the core, in Excel's order. */
function MuiSelectionSummary({ context }: { readonly context: GridSelectionSummarySlotContext }): ReactElement {
    const { size } = useContext(MuiSizeContext);
    const said = context.status === 'pending' ? context.label(summaryLabelIds.pending)
        : context.status === 'declined' ? context.declineReason
        : context.figures.map(figure => `${context.label(figure.labelId)}: ${figure.text}`).join('   ');
    return createElement(Typography<'span'>, { component: 'span', variant: size === 'small' ? 'body2' : 'body1' }, said);
}

/** The completion list and the argument hint (ADR-0051): the chosen candidate in the theme's selected tint as MUI's
 * Autocomplete marks its highlighted option, and the hint beneath in the theme's secondary text with the argument the
 * caret is in set off. Both are in the cell's font, as the text typed above them is. The box, its place and every key
 * are the core's, so MUI's Autocomplete, which keeps its own state and keys, is not used. */
function MuiCompletion({ context }: { readonly context: GridEditorCompletionSlotContext }): ReactElement {
    const { list, option } = useEditorCompletion(context);
    const hint = context.hint?.parts();
    return createElement('div', { className: 'mui-ex-grid-completion' },
        context.candidates.length === 0 ? null : createElement('div', { ...list, className: 'mui-ex-grid-completion-list' },
            ...context.candidates.map((candidate, index) => createElement('div', {
                ...option(index), key: index, className: 'mui-ex-grid-completion-item',
            }, candidate.label))),
        hint === undefined ? null : createElement('div', { className: 'mui-ex-grid-completion-hint' },
            hint[0], hint[1].length === 0 ? null : createElement('strong', null, hint[1]), hint[2]));
}

const menu = (context: GridMenuSlotContext): ReactElement => createElement(MuiMenu, { context });

/** Every Chrome slot drawn in MUI's components: what {@link MuiExGrid} hands its grid. A Consumer that frames an
 * ExGrid itself spreads it into the grid's `chrome`, inside an element of the class `mui-ex-grid` and a
 * {@link MuiSizeContext}, as MuiExGrid draws them. */
export const muiChrome: ExGridChrome<unknown> = Object.freeze({
    columnMenu: menu,
    contextMenu: menu,
    filterPanel: (context: GridFilterPanelSlotContext) => createElement(MuiFilterPanel, { context }),
    findPanel: (context: GridFindPanelSlotContext) => createElement(MuiFindPanel, { context }),
    cellEditor: (context: GridCellEditorSlotContext) => createElement(MuiCellEditor, { context }),
    cellMessage: (context: GridCellMessageSlotContext) => createElement(MuiCellMessage, { context }),
    loadingIndicator: (context: GridLoadingIndicatorSlotContext) => createElement(MuiLoading, { context }),
    selectionSummary: (context: GridSelectionSummarySlotContext) => createElement(MuiSelectionSummary, { context }),
    formulaBarText: (context: GridFormulaBarTextSlotContext) => createElement(MuiBarText, { context }),
    nameBox: (context: GridNameBoxSlotContext) => createElement(MuiNameBox, { context }),
    editorCompletion: (context: GridEditorCompletionSlotContext) => createElement(MuiCompletion, { context }),
});
