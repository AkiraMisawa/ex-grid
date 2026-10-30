import { useState } from 'react';
import { readGridViewSpec, shapedGrid, type ShapedGrid } from '@ex-grid/board';
import { BoardInput, BoardRefusedError, JsonTextError, readJsonText } from '@ex-grid/data';

// What a tool handed back as JSON, the input that says its types, and the grid's spec: all three only text, as they
// would be when an AI composes the view and a workflow runs it later.

export const input = `{"columns": [
  {"name": "desk", "type": "text"},
  {"name": "notional", "type": "decimal"},
  {"name": "rate", "type": "double"},
  {"name": "trades", "type": "integer"},
  {"name": "settles", "type": "date"}
]}`;

export const data = `[
  {"desk": "Rates", "notional": 125000000.50, "rate": 0.04125, "trades": 1342, "settles": "2026-10-22"},
  {"desk": "FX", "notional": "-8400000", "rate": 1.08734, "trades": 96, "settles": "2026-10-21"},
  {"desk": "Credit", "notional": 3250000.25, "rate": 0.0275, "trades": 410, "settles": "2026-10-23"},
  {"desk": "Equities", "notional": null, "rate": 0.0198, "trades": 7, "settles": "2026-10-22"}
]`;

export const spec = `{"data": "trades",
 "columns": [
   {"column": "desk", "header": "Desk", "pinned": true, "width": 100},
   {"column": "notional", "header": "Notional", "format": "#,##0.00;(#,##0.00)"},
   {"column": "rate", "header": "Rate", "format": "0.00000"},
   {"column": "trades", "header": "Trades"},
   {"column": "settles", "header": "Settles"}
 ],
 "sorts": [{"column": "notional", "direction": "Descending"}]}`;

/** The grid the three texts describe, or the refusal that names what is wrong. */
export function build(inputText: string, dataText: string, specText: string): { readonly grid: ShapedGrid } | { readonly refusal: string } {
    try {
        const trades = BoardInput.read('trades', readJsonText(inputText), 'inputs.trades');
        const snapshot = trades.fromJson(dataText);
        const reading = readGridViewSpec(readJsonText(specText), name => name === trades.name ? trades.shape : undefined, '');
        return { grid: shapedGrid(snapshot, reading) };
    } catch (error) {
        if (error instanceof BoardRefusedError) return { refusal: error.path === '' ? error.message : `${error.path}: ${error.message}` };
        if (error instanceof JsonTextError) return { refusal: `The input is not JSON: ${error.message}` };
        throw error;
    }
}

/** The three texts as the reader edits them, and the grid, or the refusal, they last drew. A refusal takes the last
 * grid off the screen: what is shown is what the texts say. Each grid drawn is a new source and a new column list,
 * so `drawn` counts them for a key that starts the grid again. */
export function useShapedTrades() {
    const [texts, setTexts] = useState({ input, data, spec });
    const [shown, setShown] = useState(() => ({ drawn: 0, result: build(input, data, spec) }));
    const draw = (): void => { setShown(last => ({ drawn: last.drawn + 1, result: build(texts.input, texts.data, texts.spec) })); };
    const edit = (part: keyof typeof texts) => (text: string): void => { setTexts(last => ({ ...last, [part]: text })); };
    return { texts, edit, draw, drawn: shown.drawn, result: shown.result };
}
