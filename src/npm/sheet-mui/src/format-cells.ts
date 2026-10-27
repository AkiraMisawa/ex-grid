// Format Cells in MUI's components (ADR-0071, ADR-0390): MUI's Tabs, fieldsets, Radios, Checkboxes, native selects,
// TextFields and Buttons, inside the grid's popover frame, where the built-in Format Cells stands. What it offers is
// `formatCellsOffer`'s, every choice is set through the Sheet's draft, and OK is the Sheet's, so the same choices set
// the same Cell Format under either Chrome. The palette's swatches are buttons painted in the document's colours, which
// no MUI control draws; the colour is the user's choice of data, written inline, never a token.
//
// A field typed into shows what is typed while it holds the keyboard, and the draft's text once it is left, so a
// choice never writes over typing (ADR-0240 section 1); one the draft did not take stays as typed, and OK refuses it.

import { createElement, useContext, useState, type ReactElement, type ReactNode } from 'react';
import { Button, Checkbox, FormControl, FormControlLabel, FormLabel, Radio, Tab, Tabs, TextField, Typography } from '@mui/material';
import { MuiSizeContext } from '@ex-grid/mui';
import {
    formatCellsLineStroke, formatCellsOffer, isDisabledCategory, type ColourTarget, type FormatCellsDraft, type FormatCellsTab, type PaletteSwatch,
    type SheetFormatCellsSlotContext, type SheetWords,
} from '@ex-grid/sheet';
import { useFormatCells } from '@ex-grid/sheet-react';
import type { BorderLineStyle, CellColour } from '@ex-grid/sheet-engine';

/** The `size` MUI's controls take. */
type Size = 'small' | 'medium';

/** What every tab is drawn with. */
interface TabProps {
    readonly draft: FormatCellsDraft;
    readonly words: SheetWords;
    readonly size: Size;
    readonly set: (choose: (draft: FormatCellsDraft) => void) => void;
}

/** A colour as a swatch and a preview paint it: Automatic is the Ink of the text around it. */
function paintOf(colour: CellColour): string {
    return colour.isAutomatic ? 'currentColor' : colour.toString();
}

/** The body text at the size the controls are drawn at. */
function textVariant(size: Size): 'body2' | 'body1' {
    return size === 'small' ? 'body2' : 'body1';
}

/** A group of controls under its legend, as MUI's forms group radios and checkboxes. */
function fieldset(legend: string, className: string | undefined, ...children: ReactNode[]): ReactElement {
    return createElement(FormControl<'fieldset'>, { component: 'fieldset', className: ['mui-ex-sheet-fieldset', className].filter(Boolean).join(' ') },
        createElement(FormLabel<'legend'>, { component: 'legend', className: 'mui-ex-sheet-legend' }, legend), ...children);
}

/** A radio with its label, in the body's text at the controls' size. */
function radio(key: string | number, label: ReactNode, size: Size, input: {
    readonly name: string; readonly checked: boolean; readonly onChange: () => void; readonly disabled?: boolean;
    readonly 'aria-label'?: string; readonly 'aria-describedby'?: string; readonly title?: string;
}): ReactElement {
    const { name, checked, onChange, disabled, title, ...named } = input;
    return createElement(FormControlLabel, {
        key, label, className: 'mui-ex-sheet-label', ...(disabled === undefined ? {} : { disabled }), ...(title === undefined ? {} : { title }),
        slotProps: { typography: { variant: textVariant(size) } },
        control: createElement(Radio, { name, size, checked, onChange, slotProps: { input: named } }),
    });
}

/** A checkbox with its label. */
function checkbox(label: string, size: Size, checked: boolean, onChange: (checked: boolean) => void): ReactElement {
    return createElement(FormControlLabel, {
        label, className: 'mui-ex-sheet-label', slotProps: { typography: { variant: textVariant(size) } },
        control: createElement(Checkbox, { size, checked, onChange: event => { onChange(event.target.checked); } }),
    });
}

/** A native select under its label, MUI's TextField drawing the browser's own list. */
function nativeSelect(label: string, size: Size, value: string, options: readonly { readonly value: string; readonly label: string }[],
    onChange: (value: string) => void): ReactElement {
    return createElement(TextField, {
        select: true, label, size, value, className: 'mui-ex-sheet-format-cells-dropdown', slotProps: { select: { native: true } },
        onChange: event => { onChange(event.target.value); },
    }, ...options.map(option => createElement('option', { key: option.value, value: option.value }, option.label)));
}

/** A field typed into: what is typed while it is, the draft's text otherwise. */
function DraftField({ label, value, typed, size, type = 'text', className, error, input }: {
    readonly label: string; readonly value: () => string; readonly typed: (text: string) => void; readonly size: Size;
    readonly type?: 'text' | 'number'; readonly className: string; readonly error?: string;
    readonly input?: {
        readonly min?: number; readonly max?: number; readonly required?: boolean; readonly maxLength?: number; readonly placeholder?: string;
        readonly 'data-target'?: string;
    };
}): ReactElement {
    const [typing, setTyping] = useState<string | undefined>(undefined);
    return createElement(TextField, {
        label, size, type, className, value: typing ?? value(), error: error !== undefined, ...(error === undefined ? {} : { helperText: error }),
        slotProps: { htmlInput: { ...input, spellCheck: false } },
        onFocus: event => { setTyping(event.target.value); },
        onChange: event => {
            const text = event.target.value;
            setTyping(text);
            typed(text);
        },
        onBlur: event => { if (event.target instanceof HTMLInputElement && event.target.checkValidity()) setTyping(undefined); },
    });
}

/** A list of codes to choose one of: a Negative Numbers list or a category's Types. */
function codeList(name: string, legend: string, codes: readonly string[], chosen: (index: number, code: string) => boolean,
    choose: (index: number, code: string) => void, size: Size, className: string): ReactElement {
    return createElement('div', { key: name }, fieldset(legend, className,
        ...codes.map((code, index) => radio(index, createElement('code', null, code), size, {
            name, checked: chosen(index, code), onChange: () => { choose(index, code); },
        }))));
}

function NumberTab({ draft, words, size, set, id }: TabProps & { readonly id: string }): ReactElement {
    const { category } = draft;
    const options: ReactNode[] = [];
    const hint = (key: string, text: string): ReactElement => createElement(Typography<'p'>, { key, component: 'p', variant: textVariant(size) }, text);
    if (category === 'general') options.push(hint('hint', words.formatCellsLabel('generalHint')));
    else if (category === 'text') options.push(hint('hint', words.formatCellsLabel('textHint')));
    if (draft.takesDecimalPlaces) {
        options.push(createElement(DraftField, {
            key: `places-${category}`, label: words.formatCellsLabel('decimalPlaces'), size, type: 'number', input: { min: 0, max: 30, required: true },
            className: 'mui-ex-sheet-format-cells-places', value: () => String(draft.decimalPlaces),
            typed: (text: string) => {
                const value = Number(text);
                // Only a whole number of places Excel takes is set; anything else leaves the field invalid, which OK refuses.
                if (text.trim() !== '' && Number.isInteger(value) && value >= 0 && value <= 30) set(each => { each.setDecimalPlaces(value); });
            },
        }));
    }
    if (category === 'number') {
        options.push(createElement('div', { key: 'separator' }, checkbox(words.formatCellsLabel('thousandsSeparator'), size, draft.thousandsSeparator,
            checked => { set(each => { each.setThousandsSeparator(checked); }); })));
    }
    if (category === 'currency') options.push(hint('symbol', words.currencySymbolShown(draft.currencySymbol)));
    if (draft.negativeNumbers.length > 0) {
        options.push(codeList(`${id}-negative`, words.formatCellsLabel('negativeNumbers'), draft.negativeNumbers, index => draft.negativeNumber === index,
            index => { set(each => { each.setNegativeNumber(index); }); }, size, 'mui-ex-sheet-format-cells-list'));
    }
    if (category === 'custom') {
        const why = draft.numberFormatRefusal;
        options.push(createElement(DraftField, {
            key: 'code', label: words.formatCellsLabel('type'), size, className: 'mui-ex-sheet-format-cells-code', value: () => draft.typeCode,
            typed: (text: string) => { set(each => { each.setType(text); }); }, ...(why === undefined ? {} : { error: words.formatCellsRefusal(why) }),
        }));
    }
    if (draft.types.length > 0) {
        options.push(codeList(`${id}-type`, words.formatCellsLabel(category === 'custom' ? 'codes' : 'type'), draft.types, (_, code) => draft.typeCode === code,
            (_, code) => { set(each => { each.setType(code); }); }, size, 'mui-ex-sheet-format-cells-list mui-ex-sheet-format-cells-types'));
    }
    return createElement('div', { className: 'mui-ex-sheet-format-cells-columns' },
        fieldset(words.formatCellsLabel('category'), 'mui-ex-sheet-format-cells-list',
            ...formatCellsOffer.categories.flatMap(({ category: each, enabled }) => {
                const reasonId = `${id}-reason-${each}`;
                const choice = radio(each, words.categoryName(each), size, {
                    name: `${id}-category`, checked: draft.category === each, disabled: !enabled,
                    ...(isDisabledCategory(each) ? { 'aria-describedby': reasonId } : {}), onChange: () => { set(one => { one.selectCategory(each); }); },
                });
                return isDisabledCategory(each)
                    ? [choice, createElement(Typography<'p'>, {
                        key: `${each}-reason`, id: reasonId, component: 'p', variant: 'caption', color: 'text.secondary', className: 'mui-ex-sheet-format-cells-reason',
                    }, words.categoryDisabled(each))]
                    : [choice];
            })),
        createElement('div', { className: 'mui-ex-sheet-format-cells-options' }, ...options));
}

function AlignmentTab({ draft, words, size, set }: TabProps): ReactElement {
    return nativeSelect(words.formatCellsLabel('horizontal'), size, draft.alignment,
        formatCellsOffer.alignments.map(alignment => ({ value: alignment, label: words.alignmentName(alignment) })),
        value => {
            const alignment = formatCellsOffer.alignments.find(each => each === value);
            if (alignment === undefined) throw new RangeError(`'${value}' is not an alignment Format Cells offers (ADR-0071).`);
            set(each => { each.setAlignment(alignment); });
        });
}

/** Case 23's palette as one group of radio buttons: Automatic or No Colour first, the theme's sixty and the ten
 * standard colours, each named as Excel names it, and More Colours, which takes a hex value. */
function Palette({ draft, words, size, set, target, id }: TabProps & { readonly target: ColourTarget; readonly id: string }): ReactElement {
    const name = `${id}-palette-${target}`;
    const isFill = target === 'fill';
    const chosen = draft.colourOf(target);
    const swatches = (row: readonly PaletteSwatch[], key: string): ReactElement => createElement('div', { key, className: 'mui-ex-sheet-swatches' },
        ...row.map((swatch, index) => {
            const swatchName = words.swatchName(swatch);
            const paint = paintOf(swatch.colour);
            return createElement('input', {
                key: index, type: 'radio', name, className: 'mui-ex-sheet-swatch', 'aria-label': swatchName, title: swatchName, 'data-rgb': paint,
                style: { backgroundColor: paint }, checked: chosen !== undefined && !chosen.isAutomatic && chosen.equals(swatch.colour),
                onChange: () => { set(each => { each.setColour(target, swatch.colour); }); },
            });
        }));
    return createElement('div', {
        role: 'radiogroup', 'aria-label': words.formatCellsLabel(isFill ? 'backgroundColour' : 'colour'), className: 'mui-ex-sheet-palette',
    },
    radio('first', words.formatCellsLabel(isFill ? 'noColour' : 'automatic'), size, {
        name, checked: isFill ? chosen === undefined : chosen?.isAutomatic === true, onChange: () => { set(each => { each.setColour(target, undefined); }); },
    }),
    swatches(formatCellsOffer.themeColours.flat(), 'theme'),
    swatches(formatCellsOffer.standardColours, 'standard'),
    createElement(DraftField, {
        key: `hex-${target}`, label: words.formatCellsLabel('moreColours'), size, input: { maxLength: 7, placeholder: '#RRGGBB', 'data-target': target },
        className: 'mui-ex-sheet-more-colours', value: () => draft.colourText(target), typed: (text: string) => { set(each => { each.setColourText(target, text); }); },
    }));
}

function FontTab(props: TabProps & { readonly id: string }): ReactElement {
    const { draft, words, size, set, id } = props;
    return createElement('div', null,
        createElement('div', { className: 'mui-ex-sheet-format-cells-columns' },
            fieldset(words.formatCellsLabel('fontStyle'), 'mui-ex-sheet-format-cells-list',
                ...formatCellsOffer.fontStyles.map(style => radio(style, words.fontStyleName(style), size, {
                    name: `${id}-font-style`, checked: draft.fontStyle === style, onChange: () => { set(each => { each.setFontStyle(style); }); },
                }))),
            createElement('div', { className: 'mui-ex-sheet-format-cells-options' },
                nativeSelect(words.formatCellsLabel('underline'), size, draft.underline ? 'single' : 'none',
                    [{ value: 'none', label: words.formatCellsLabel('underlineNone') }, { value: 'single', label: words.formatCellsLabel('underlineSingle') }],
                    value => { const single = value === 'single'; set(each => { each.setUnderline(single); }); }),
                checkbox(words.formatCellsLabel('strikethrough'), size, draft.strikethrough, checked => { set(each => { each.setStrikethrough(checked); }); }),
                // Excel's Normal font (ADR-0071, 2026-10-02): checking it sets every part of the Font to its default; it
                // shows checked while they are, so unchecking it changes nothing.
                checkbox(words.formatCellsLabel('normalFont'), size, draft.isNormalFont, checked => { if (checked) set(each => { each.setNormalFont(); }); }))),
        fieldset(words.formatCellsLabel('colour'), undefined, createElement(Palette, { ...props, target: 'font' })));
}

/** One stroke of a line style's sample, `y` down it. */
function sampleLine(y: number, width: string, dashes: string | undefined): ReactElement {
    return createElement('line', { key: y, x1: 0, y1: y, x2: 56, y2: y, stroke: 'currentColor', strokeWidth: width, strokeDasharray: dashes });
}

/** A line style's sample: a stroke and its dashes. */
function lineSample(style: BorderLineStyle): ReactElement {
    const { width, dashes } = formatCellsLineStroke(style);
    const lines = style === 'none' ? [] : style === 'double' ? [sampleLine(2.5, width, undefined), sampleLine(5.5, width, undefined)] : [sampleLine(4, width, dashes)];
    return createElement('svg', { className: 'mui-ex-sheet-format-cells-line', width: 56, height: 8, 'aria-hidden': true }, ...lines);
}

/** The preview: the range's edges as they will be, and an edge whose cells' sides differ as a grey dotted line. */
function preview(draft: FormatCellsDraft, label: string): ReactElement {
    const edges: [Parameters<FormatCellsDraft['edgeLine']>[0], number, number, number, number][] = [
        ['top', 4, 4, 116, 4], ['bottom', 4, 68, 116, 68], ['left', 4, 4, 4, 68], ['right', 116, 4, 116, 68],
    ];
    if (draft.hasInsideHorizontal) edges.push(['insideHorizontal', 4, 36, 116, 36]);
    if (draft.hasInsideVertical) edges.push(['insideVertical', 60, 4, 60, 68]);
    const lines = edges.flatMap(([edge, x1, y1, x2, y2]) => {
        const line = draft.edgeLine(edge);
        if (line?.isNone === true) return [];
        if (line === undefined) {
            return [createElement('line', { key: edge, x1, y1, x2, y2, 'data-edge': edge, className: 'mui-ex-sheet-format-cells-differs', stroke: '#808080', strokeWidth: 1, strokeDasharray: '1 2' })];
        }
        const { width, dashes } = formatCellsLineStroke(line.style);
        return [createElement('line', { key: edge, x1, y1, x2, y2, 'data-edge': edge, stroke: paintOf(line.colour), strokeWidth: width, strokeDasharray: dashes })];
    });
    return createElement('svg', { className: 'mui-ex-sheet-format-cells-preview', width: 120, height: 72, viewBox: '0 0 120 72', role: 'img', 'aria-label': label }, ...lines);
}

function BorderTab(props: TabProps & { readonly id: string }): ReactElement {
    const { draft, words, size, set, id } = props;
    const pressedOf = (edge: Parameters<FormatCellsDraft['edgeLine']>[0]): 'true' | 'false' | 'mixed' => {
        const line = draft.edgeLine(edge);
        if (line === undefined) return 'mixed';
        return line.isNone ? 'false' : 'true';
    };
    return createElement('div', null,
        createElement('div', { className: 'mui-ex-sheet-format-cells-columns' },
            fieldset(words.formatCellsLabel('lineStyle'), undefined,
                createElement('div', { className: 'mui-ex-sheet-format-cells-columns' },
                    ...formatCellsOffer.lineStyleColumns.map((column, at) => createElement('div', { key: at, className: 'mui-ex-sheet-format-cells-line-column' },
                        ...column.map(style => radio(style, lineSample(style), size, {
                            name: `${id}-line-style`, 'aria-label': words.lineStyleName(style), title: words.lineStyleName(style),
                            checked: draft.lineStyle === style, onChange: () => { set(each => { each.setLineStyle(style); }); },
                        })))))),
            createElement('div', { className: 'mui-ex-sheet-format-cells-options' },
                createElement('div', { role: 'group', 'aria-label': words.formatCellsLabel('presets'), className: 'mui-ex-sheet-format-cells-buttons' },
                    ...formatCellsOffer.presets.map(preset => createElement(Button, {
                        key: preset, type: 'button', size, variant: 'outlined', disabled: !draft.canApply(preset),
                        onClick: () => { set(each => { each.applyPreset(preset); }); },
                    }, words.presetName(preset)))),
                // An edge set is contained, one unset outlined, and one whose cells' sides differ outlined in the primary.
                createElement('div', { role: 'group', 'aria-label': words.formatCellsLabel('border'), className: 'mui-ex-sheet-format-cells-buttons' },
                    ...formatCellsOffer.edges.map(edge => {
                        const pressed = pressedOf(edge);
                        const named = { 'data-edge': edge };
                        return createElement(Button, {
                            key: edge, type: 'button', size, variant: pressed === 'true' ? 'contained' : 'outlined', color: pressed === 'false' ? 'inherit' : 'primary',
                            disabled: !draft.canSet(edge), 'aria-pressed': pressed, ...named, onClick: () => { set(each => { each.toggleEdge(edge); }); },
                        }, words.edgeName(edge));
                    })),
                preview(draft, words.formatCellsLabel('preview')))),
        fieldset(words.formatCellsLabel('colour'), undefined, createElement(Palette, { ...props, target: 'border' })));
}

function FillTab(props: TabProps & { readonly id: string }): ReactElement {
    return fieldset(props.words.formatCellsLabel('backgroundColour'), undefined, createElement(Palette, { ...props, target: 'fill' }));
}

function tabContents(tab: FormatCellsTab, props: TabProps & { readonly id: string }): ReactElement {
    switch (tab) {
        case 'number': return createElement(NumberTab, props);
        case 'alignment': return createElement(AlignmentTab, props);
        case 'font': return createElement(FontTab, props);
        case 'border': return createElement(BorderTab, props);
        case 'fill': return createElement(FillTab, props);
        default: throw new RangeError(`Unknown Format Cells tab '${String(tab satisfies never)}' (ADR-0071).`);
    }
}

// Unique on the page, for the radio groups' names and the tabs' ids: two Sheets' Format Cells each have their own.
let openings = 0;

/** Format Cells in MUI's components, over the Sheet's draft. */
export function MuiFormatCells({ context }: { readonly context: SheetFormatCellsSlotContext }): ReactElement {
    const { tab, showTab, tabControl, set, refusal, submit, cancel } = useFormatCells(context);
    const { size } = useContext(MuiSizeContext);
    const [id] = useState(() => { openings += 1; return `mui-ex-sheet-format-cells-${String(openings)}`; });
    const { draft, words } = context;
    // OK checks its fields first, on every tab, since each tab stays drawn while another is shown (useFormatCells).
    return createElement('form', { className: 'mui-ex-sheet-format-cells', onSubmit: submit, noValidate: true },
        createElement(Tabs, {
            // The arrows show the tab they reach, as the built-in Format Cells' and Mantine's do.
            value: tab, variant: 'scrollable', scrollButtons: false, selectionFollowsFocus: true, 'aria-label': words.formatCellsLabel('tabs'), className: 'mui-ex-sheet-format-cells-tabs',
            onChange: (_: unknown, value: unknown) => {
                const shown = formatCellsOffer.tabs.find(each => each === value);
                if (shown === undefined) throw new RangeError(`'${String(value)}' is not a tab of Format Cells (ADR-0071).`);
                showTab(shown);
            },
        }, ...formatCellsOffer.tabs.map(each => createElement(Tab, {
            key: each, value: each, label: words.tabName(each), id: `${id}-tab-${each}`, 'aria-controls': `${id}-panel-${each}`,
            className: 'mui-ex-sheet-format-cells-tab', ...(each === tab ? { ref: tabControl } : {}),
        }))),
        // Every tab stays drawn, its fields as typed: a field left invalid is still refused by OK from another tab.
        ...formatCellsOffer.tabs.map(each => createElement('div', {
            key: each, role: 'tabpanel', id: `${id}-panel-${each}`, 'aria-labelledby': `${id}-tab-${each}`, hidden: each !== tab,
            className: 'mui-ex-sheet-format-cells-panel', 'data-ex-tab': each,
        }, tabContents(each, { draft, words, size, set, id }))),
        refusal === undefined ? null : createElement(Typography<'p'>, {
            role: 'alert', component: 'p', color: 'error', variant: textVariant(size), className: 'mui-ex-sheet-format-cells-refusal',
        }, refusal),
        createElement('div', { className: 'mui-ex-sheet-format-cells-actions' },
            createElement(Button, { type: 'submit', size, variant: 'contained' }, words.formatCellsLabel('ok')),
            createElement(Button, { type: 'button', size, variant: 'outlined', onClick: cancel }, words.formatCellsLabel('cancel'))));
}
