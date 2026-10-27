// Format Cells in Mantine's components (ADR-0071, ADR-0390): Mantine's Tabs, Fieldsets, Radios, Checkboxes,
// NativeSelects, TextInputs and Buttons, inside the grid's popover frame, where the built-in Format Cells stands. What
// it offers is `formatCellsOffer`'s, every choice is set through the Sheet's draft, and OK is the Sheet's, so the same
// choices set the same Cell Format under either Chrome. The palette's swatches are buttons painted in the document's
// colours, which no Mantine control draws; the colour is the user's choice of data, written inline, never a token.
//
// A field typed into shows what is typed while it holds the keyboard, and the draft's text once it is left, so a
// choice never writes over typing (ADR-0240 section 1); one the draft did not take stays as typed, and OK refuses it.

import { createElement, useContext, useState, type FormEvent, type ReactElement, type ReactNode } from 'react';
import { Button, Checkbox, Fieldset, NativeSelect, Radio, Tabs, Text, TextInput, type MantineSize } from '@mantine/core';
import { MantineSizeContext } from '@ex-grid/mantine';
import {
    formatCellsLineStroke, formatCellsOffer, isDisabledCategory, type ColourTarget, type FormatCellsDraft, type FormatCellsTab, type PaletteSwatch,
    type SheetFormatCellsSlotContext, type SheetWords,
} from '@ex-grid/sheet';
import { useFormatCells } from '@ex-grid/sheet-react';
import { CellColour, CellFill, type BorderLineStyle } from '@ex-grid/sheet-engine';

/** What every tab is drawn with. */
interface TabProps {
    readonly draft: FormatCellsDraft;
    readonly words: SheetWords;
    readonly size: MantineSize;
    readonly set: (choose: (draft: FormatCellsDraft) => void) => void;
}

/** A colour as a swatch and a preview paint it: Automatic is the Ink of the text around it. */
function paintOf(colour: CellColour): string {
    return colour.isAutomatic ? 'currentColor' : colour.toString();
}

/** A field typed into: what is typed while it is, the draft's text otherwise; `valid` says whether a text left in it
 * is one the draft took, which it then shows as the draft has it. */
function DraftField({ label, value, typed, size, type = 'text', className, error, ...field }: {
    readonly label: string; readonly value: () => string; readonly typed: (text: string) => void; readonly size: MantineSize;
    readonly type?: 'text' | 'number'; readonly className: string; readonly error?: ReactNode; readonly min?: number; readonly max?: number;
    readonly required?: boolean; readonly placeholder?: string; readonly maxLength?: number; readonly 'data-target'?: string;
}): ReactElement {
    const [typing, setTyping] = useState<string | undefined>(undefined);
    return createElement(TextInput, {
        ...field, label, size, type, className, error, spellCheck: false, value: typing ?? value(),
        onFocus: event => { setTyping(event.currentTarget.value); },
        onChange: event => {
            const text = event.currentTarget.value;
            setTyping(text);
            typed(text);
        },
        onBlur: event => { if (event.currentTarget.checkValidity()) setTyping(undefined); },
    });
}

/** A list of codes to choose one of: a Negative Numbers list or a category's Types. */
function codeList(name: string, legend: string, codes: readonly string[], chosen: (index: number, code: string) => boolean,
    choose: (index: number, code: string) => void, size: MantineSize, className: string): ReactElement {
    return createElement(Fieldset, { key: name, legend, variant: 'unstyled', className },
        ...codes.map((code, index) => createElement(Radio, {
            key: index, name, size, checked: chosen(index, code), onChange: () => { choose(index, code); },
            label: createElement('code', null, code),
        })));
}

function NumberTab({ draft, words, size, set, id }: TabProps & { readonly id: string }): ReactElement {
    const { category } = draft;
    const options: ReactNode[] = [];
    if (category === 'general') options.push(createElement(Text<'p'>, { key: 'hint', size }, words.formatCellsLabel('generalHint')));
    else if (category === 'text') options.push(createElement(Text<'p'>, { key: 'hint', size }, words.formatCellsLabel('textHint')));
    if (draft.takesDecimalPlaces) {
        options.push(createElement(DraftField, {
            key: `places-${category}`, label: words.formatCellsLabel('decimalPlaces'), size, type: 'number', min: 0, max: 30, required: true,
            className: 'mantine-ex-sheet-format-cells-places', value: () => String(draft.decimalPlaces),
            typed: (text: string) => {
                const value = Number(text);
                // Only a whole number of places Excel takes is set; anything else leaves the field invalid, which OK refuses.
                if (text.trim() !== '' && Number.isInteger(value) && value >= 0 && value <= 30) set(each => { each.setDecimalPlaces(value); });
            },
        }));
    }
    if (category === 'number') {
        options.push(createElement(Checkbox, {
            key: 'separator', size, label: words.formatCellsLabel('thousandsSeparator'), checked: draft.thousandsSeparator,
            onChange: event => { const { checked } = event.currentTarget; set(each => { each.setThousandsSeparator(checked); }); },
        }));
    }
    if (category === 'currency') options.push(createElement(Text<'p'>, { key: 'symbol', size }, words.currencySymbolShown(draft.currencySymbol)));
    if (draft.negativeNumbers.length > 0) {
        options.push(codeList(`${id}-negative`, words.formatCellsLabel('negativeNumbers'), draft.negativeNumbers, index => draft.negativeNumber === index,
            index => { set(each => { each.setNegativeNumber(index); }); }, size, 'mantine-ex-sheet-format-cells-list'));
    }
    if (category === 'custom') {
        const why = draft.numberFormatRefusal;
        options.push(createElement(DraftField, {
            key: 'code', label: words.formatCellsLabel('type'), size, className: 'mantine-ex-sheet-format-cells-code', value: () => draft.typeCode,
            typed: (text: string) => { set(each => { each.setType(text); }); }, error: why === undefined ? undefined : words.formatCellsRefusal(why),
        }));
    }
    if (draft.types.length > 0) {
        options.push(codeList(`${id}-type`, words.formatCellsLabel(category === 'custom' ? 'codes' : 'type'), draft.types, (_, code) => draft.typeCode === code,
            (_, code) => { set(each => { each.setType(code); }); }, size, 'mantine-ex-sheet-format-cells-list mantine-ex-sheet-format-cells-types'));
    }
    return createElement('div', { className: 'mantine-ex-sheet-format-cells-columns' },
        createElement(Fieldset, { legend: words.formatCellsLabel('category'), variant: 'unstyled', className: 'mantine-ex-sheet-format-cells-list' },
            ...formatCellsOffer.categories.flatMap(({ category: each, enabled }) => {
                const reasonId = `${id}-reason-${each}`;
                const radio = createElement(Radio, {
                    key: each, name: `${id}-category`, size, label: words.categoryName(each), checked: draft.category === each, disabled: !enabled,
                    'aria-describedby': isDisabledCategory(each) ? reasonId : undefined, onChange: () => { set(one => { one.selectCategory(each); }); },
                });
                return isDisabledCategory(each)
                    ? [radio, createElement(Text<'p'>, { key: `${each}-reason`, id: reasonId, size: 'xs', c: 'dimmed', className: 'mantine-ex-sheet-format-cells-reason' }, words.categoryDisabled(each))]
                    : [radio];
            })),
        createElement('div', { className: 'mantine-ex-sheet-format-cells-options' }, ...options));
}

function AlignmentTab({ draft, words, size, set }: TabProps): ReactElement {
    return createElement(NativeSelect, {
        label: words.formatCellsLabel('horizontal'), size, className: 'mantine-ex-sheet-format-cells-dropdown', value: draft.alignment,
        data: formatCellsOffer.alignments.map(alignment => ({ value: alignment, label: words.alignmentName(alignment) })),
        onChange: event => {
            const { value } = event.currentTarget;
            const alignment = formatCellsOffer.alignments.find(each => each === value);
            if (alignment === undefined) throw new RangeError(`'${value}' is not an alignment Format Cells offers (ADR-0071).`);
            set(each => { each.setAlignment(alignment); });
        },
    });
}

/** The colour a target holds now, `undefined` for No Colour on the Fill. */
function chosenColour(draft: FormatCellsDraft, target: ColourTarget): CellColour | undefined {
    switch (target) {
        case 'font': return draft.fontColour;
        case 'fill': return draft.fill.colour;
        case 'border': return draft.lineColour;
        default: throw new RangeError(`Unknown colour target '${String(target satisfies never)}' (ADR-0071).`);
    }
}

/** Chooses a colour for a target: `undefined` is Automatic for the Font and the Border, and No Colour for the Fill. */
function chooseColour(draft: FormatCellsDraft, target: ColourTarget, colour: CellColour | undefined): void {
    switch (target) {
        case 'font': draft.setFontColour(colour === undefined ? CellColour.automatic : colour); break;
        case 'fill': draft.setFill(colour === undefined ? CellFill.none : CellFill.solid(colour)); break;
        case 'border': draft.setLineColour(colour === undefined ? CellColour.automatic : colour); break;
        default: throw new RangeError(`Unknown colour target '${String(target satisfies never)}' (ADR-0071).`);
    }
}

/** Case 23's palette as one group of radio buttons: Automatic or No Colour first, the theme's sixty and the ten
 * standard colours, each named as Excel names it, and More Colours, which takes a hex value. */
function Palette({ draft, words, size, set, target, id }: TabProps & { readonly target: ColourTarget; readonly id: string }): ReactElement {
    const name = `${id}-palette-${target}`;
    const isFill = target === 'fill';
    const chosen = chosenColour(draft, target);
    const swatches = (row: readonly PaletteSwatch[], key: string): ReactElement => createElement('div', { key, className: 'mantine-ex-sheet-swatches' },
        ...row.map((swatch, index) => {
            const swatchName = words.swatchName(swatch);
            const paint = paintOf(swatch.colour);
            return createElement('input', {
                key: index, type: 'radio', name, className: 'mantine-ex-sheet-swatch', 'aria-label': swatchName, title: swatchName, 'data-rgb': paint,
                style: { backgroundColor: paint }, checked: chosen !== undefined && !chosen.isAutomatic && chosen.equals(swatch.colour),
                onChange: () => { set(each => { chooseColour(each, target, swatch.colour); }); },
            });
        }));
    return createElement('div', {
        role: 'radiogroup', 'aria-label': words.formatCellsLabel(isFill ? 'backgroundColour' : 'colour'), className: 'mantine-ex-sheet-palette',
    },
    createElement(Radio, {
        key: 'first', name, size, label: words.formatCellsLabel(isFill ? 'noColour' : 'automatic'),
        checked: isFill ? chosen === undefined : chosen?.isAutomatic === true, onChange: () => { set(each => { chooseColour(each, target, undefined); }); },
    }),
    swatches(formatCellsOffer.themeColours.flat(), 'theme'),
    swatches(formatCellsOffer.standardColours, 'standard'),
    createElement(DraftField, {
        key: `hex-${target}`, label: words.formatCellsLabel('moreColours'), size, placeholder: '#RRGGBB', maxLength: 7, 'data-target': target,
        className: 'mantine-ex-sheet-more-colours', value: () => draft.colourText(target), typed: (text: string) => { set(each => { each.setColourText(target, text); }); },
    }));
}

function FontTab(props: TabProps & { readonly id: string }): ReactElement {
    const { draft, words, size, set, id } = props;
    return createElement('div', null,
        createElement('div', { className: 'mantine-ex-sheet-format-cells-columns' },
            createElement(Fieldset, { legend: words.formatCellsLabel('fontStyle'), variant: 'unstyled', className: 'mantine-ex-sheet-format-cells-list' },
                ...formatCellsOffer.fontStyles.map(style => createElement(Radio, {
                    key: style, name: `${id}-font-style`, size, label: words.fontStyleName(style), checked: draft.fontStyle === style,
                    onChange: () => { set(each => { each.setFontStyle(style); }); },
                }))),
            createElement('div', { className: 'mantine-ex-sheet-format-cells-options' },
                createElement(NativeSelect, {
                    label: words.formatCellsLabel('underline'), size, className: 'mantine-ex-sheet-format-cells-dropdown', value: draft.underline ? 'single' : 'none',
                    data: [{ value: 'none', label: words.formatCellsLabel('underlineNone') }, { value: 'single', label: words.formatCellsLabel('underlineSingle') }],
                    onChange: event => { const single = event.currentTarget.value === 'single'; set(each => { each.setUnderline(single); }); },
                }),
                createElement(Checkbox, {
                    size, label: words.formatCellsLabel('strikethrough'), checked: draft.strikethrough,
                    onChange: event => { const { checked } = event.currentTarget; set(each => { each.setStrikethrough(checked); }); },
                }),
                // Excel's Normal font (ADR-0071, 2026-10-02): checking it sets every part of the Font to its default; it
                // shows checked while they are, so unchecking it changes nothing.
                createElement(Checkbox, {
                    size, label: words.formatCellsLabel('normalFont'), checked: draft.isNormalFont,
                    onChange: event => { if (event.currentTarget.checked) set(each => { each.setNormalFont(); }); },
                }))),
        createElement(Fieldset, { legend: words.formatCellsLabel('colour'), variant: 'unstyled' }, createElement(Palette, { ...props, target: 'font' })));
}

/** One stroke of a line style's sample, `y` down it. */
function sampleLine(y: number, width: string, dashes: string | undefined): ReactElement {
    return createElement('line', { key: y, x1: 0, y1: y, x2: 56, y2: y, stroke: 'currentColor', strokeWidth: width, strokeDasharray: dashes });
}

/** A line style's sample: a stroke and its dashes. */
function lineSample(style: BorderLineStyle): ReactElement {
    const line = sampleLine;
    const { width, dashes } = formatCellsLineStroke(style);
    const lines = style === 'none' ? [] : style === 'double' ? [line(2.5, width, undefined), line(5.5, width, undefined)] : [line(4, width, dashes)];
    return createElement('svg', { className: 'mantine-ex-sheet-format-cells-line', width: 56, height: 8, 'aria-hidden': true }, ...lines);
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
            return [createElement('line', { key: edge, x1, y1, x2, y2, 'data-edge': edge, className: 'mantine-ex-sheet-format-cells-differs', stroke: '#808080', strokeWidth: 1, strokeDasharray: '1 2' })];
        }
        const { width, dashes } = formatCellsLineStroke(line.style);
        return [createElement('line', { key: edge, x1, y1, x2, y2, 'data-edge': edge, stroke: paintOf(line.colour), strokeWidth: width, strokeDasharray: dashes })];
    });
    return createElement('svg', { className: 'mantine-ex-sheet-format-cells-preview', width: 120, height: 72, viewBox: '0 0 120 72', role: 'img', 'aria-label': label }, ...lines);
}

function BorderTab(props: TabProps & { readonly id: string }): ReactElement {
    const { draft, words, size, set, id } = props;
    const pressedOf = (edge: Parameters<FormatCellsDraft['edgeLine']>[0]): 'true' | 'false' | 'mixed' => {
        const line = draft.edgeLine(edge);
        if (line === undefined) return 'mixed';
        return line.isNone ? 'false' : 'true';
    };
    return createElement('div', null,
        createElement('div', { className: 'mantine-ex-sheet-format-cells-columns' },
            createElement(Fieldset, { legend: words.formatCellsLabel('lineStyle'), variant: 'unstyled' },
                createElement('div', { className: 'mantine-ex-sheet-format-cells-columns' },
                    ...formatCellsOffer.lineStyleColumns.map((column, at) => createElement('div', { key: at, className: 'mantine-ex-sheet-format-cells-line-column' },
                        ...column.map(style => createElement(Radio, {
                            key: style, name: `${id}-line-style`, size, 'aria-label': words.lineStyleName(style), title: words.lineStyleName(style),
                            label: lineSample(style), checked: draft.lineStyle === style, onChange: () => { set(each => { each.setLineStyle(style); }); },
                        })))))),
            createElement('div', { className: 'mantine-ex-sheet-format-cells-options' },
                createElement('div', { role: 'group', 'aria-label': words.formatCellsLabel('presets'), className: 'mantine-ex-sheet-format-cells-buttons' },
                    ...formatCellsOffer.presets.map(preset => createElement(Button<'button'>, {
                        key: preset, type: 'button', size, variant: 'default', disabled: !draft.canApply(preset),
                        onClick: () => { set(each => { each.applyPreset(preset); }); },
                    }, words.presetName(preset)))),
                createElement('div', { role: 'group', 'aria-label': words.formatCellsLabel('border'), className: 'mantine-ex-sheet-format-cells-buttons' },
                    ...formatCellsOffer.edges.map(edge => {
                        const pressed = pressedOf(edge);
                        const named = { 'data-edge': edge };
                        return createElement(Button<'button'>, {
                            key: edge, type: 'button', size, variant: pressed === 'true' ? 'filled' : pressed === 'mixed' ? 'light' : 'default',
                            disabled: !draft.canSet(edge), 'aria-pressed': pressed, ...named, onClick: () => { set(each => { each.toggleEdge(edge); }); },
                        }, words.edgeName(edge));
                    })),
                preview(draft, words.formatCellsLabel('preview')))),
        createElement(Fieldset, { legend: words.formatCellsLabel('colour'), variant: 'unstyled' }, createElement(Palette, { ...props, target: 'border' })));
}

function FillTab(props: TabProps & { readonly id: string }): ReactElement {
    return createElement(Fieldset, { legend: props.words.formatCellsLabel('backgroundColour'), variant: 'unstyled' }, createElement(Palette, { ...props, target: 'fill' }));
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

// Unique on the page, for the radio groups' names: two Sheets' Format Cells each have their own.
let openings = 0;

/** Format Cells in Mantine's components, over the Sheet's draft. */
export function MantineFormatCells({ context }: { readonly context: SheetFormatCellsSlotContext }): ReactElement {
    const { tab, showTab, tabControl, set, refusal, ok, cancel } = useFormatCells(context);
    const size = useContext(MantineSizeContext);
    const [id] = useState(() => { openings += 1; return `mantine-ex-sheet-format-cells-${String(openings)}`; });
    const { draft, words } = context;
    // OK checks its fields first: one the browser holds invalid, a number of places out of range, is reported where it
    // stands, and nothing is set.
    const submitted = (event: FormEvent<HTMLFormElement>): void => {
        event.preventDefault();
        if (!event.currentTarget.checkValidity()) {
            event.currentTarget.reportValidity();
            return;
        }
        ok();
    };
    return createElement('form', { className: 'mantine-ex-sheet-format-cells', onSubmit: submitted, noValidate: true },
        createElement(Tabs, {
            value: tab, keepMounted: false,
            onChange: (value: string | null) => {
                const shown = formatCellsOffer.tabs.find(each => each === value);
                if (shown === undefined) throw new RangeError(`'${String(value)}' is not a tab of Format Cells (ADR-0071).`);
                showTab(shown);
            },
        },
        createElement(Tabs.List, {
            'aria-label': words.formatCellsLabel('tabs'),
            children: formatCellsOffer.tabs.map(each => createElement(Tabs.Tab, {
                key: each, value: each, ...(each === tab ? { ref: tabControl } : {}),
            }, words.tabName(each))),
        }),
        createElement(Tabs.Panel, { value: tab, className: 'mantine-ex-sheet-format-cells-panel', children: tabContents(tab, { draft, words, size, set, id }) })),
        refusal === undefined ? null : createElement(Text<'p'>, { role: 'alert', c: 'red', size, className: 'mantine-ex-sheet-format-cells-refusal' }, refusal),
        createElement('div', { className: 'mantine-ex-sheet-format-cells-actions' },
            createElement(Button<'button'>, { type: 'submit', size }, words.formatCellsLabel('ok')),
            createElement(Button<'button'>, { type: 'button', size, variant: 'default', onClick: cancel }, words.formatCellsLabel('cancel'))));
}
