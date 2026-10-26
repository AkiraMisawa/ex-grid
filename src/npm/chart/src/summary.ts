// The chart said in words, for a screen reader (ADR-0275): generated from the definition, so a chart nobody
// described still says what it is. The port of the C# engine's `ChartSummary`, held to it by the shared
// cases under tests/cases/chart/.
import { axisTitle } from './csv';
import type { ChartDefinition } from './definition';
import { upToPlaces } from './invariant-text';
import type { ChartView } from './metrics';
import type { ChartAxis, ChartPoint, ChartSeries, ChartValue } from './model';
import { defined, elementAt } from './must';
import { greatest, least } from './numbers';
import { valueRange } from './point-scales';
import type { ChartScene } from './scene';
import { toNumber, writePosition, writeValue } from './values';

/**
 * The chart's name, the horizontal axis with its range, each panel's vertical axes with theirs, and each
 * visible series with its maximum, written through its format, then the Consumer's description. The view
 * is the one the scene was built with, which the scene already holds.
 */
export function chartSummary(definition: ChartDefinition, scene: ChartScene, _view: ChartView, title?: string, description?: string): string {
    const axis = definition.axis;
    const parts: string[] = [];
    if (title !== undefined && title.trim() !== '') {
        parts.push(`${title.trim().replace(/\.+$/, '')}.`);
    }
    parts.push(`${axisTitle(axis)}, ${writePosition(axis, scene.xFrom)} to ${writePosition(axis, scene.xTo)}.`);
    const byId = new Map(definition.series.map(s => [s.id, s] as const));
    for (const panel of scene.panels) {
        const named = scene.panels.length > 1 && panel.name.length > 0 ? `${panel.name}: ` : '';
        for (const v of [panel.left, panel.right]) {
            if (v !== null) {
                parts.push(`${named}${v.side === 'left' ? 'Left' : 'Right'} axis, ${number(v.min)} to ${number(v.max)}.`);
            }
        }
        for (const piece of panel.series) {
            const s = defined(byId.get(piece.id), `the series '${piece.id}'`);
            parts.push(panel.volumeOf === null ? series(s, axis, scene) : volume(s, axis, scene));
        }
    }
    if (description !== undefined && description.trim() !== '') {
        parts.push(description.trim());
    }
    return parts.join(' ');
}

function series(s: ChartSeries, axis: ChartAxis, scene: ChartScene): string {
    if (s.kind === 'observation') {
        const seen = valued(s.points.filter(p => p.x >= scene.xFrom && p.x <= scene.xTo));
        if (seen.length === 0) {
            return `${s.name}: no point in range.`;
        }
        const low = first(seen, (a, b) => a < b);
        const high = first(seen, (a, b) => a > b);
        const xs = seen.map(p => p.x);
        const across = `${writePosition(axis, least(xs))} to ${writePosition(axis, greatest(xs))} across, ${writeValue(s, low.y)} to ${writeValue(s, high.y)} up.`;
        let said = s.edges.length > 0
            ? `${s.name}: ${String(s.points.length)} nodes and ${String(s.edges.length)} edges, ${across}`
            : `${s.name}: ${String(seen.length)} points, ${across}`;
        const colour = s.colourBy === undefined ? undefined : valueRange(s.colourBy.values);
        if (s.colourBy !== undefined && colour !== undefined) {
            said += ` ${s.name}: colour is ${s.colourBy.title}, ${number(colour[0])} to ${number(colour[1])}.`;
        }
        const size = s.sizeBy === undefined ? undefined : valueRange(s.sizeBy.values);
        if (s.sizeBy !== undefined && size !== undefined) {
            said += ` ${s.name}: size is ${s.sizeBy.title}, ${number(size[0])} to ${number(size[1])}.`;
        }
        return said;
    }
    const f = s.sampler;
    const points: readonly ChartPoint[] = f === undefined
        ? s.points.filter(p => p.x >= scene.xFrom && p.x <= scene.xTo)
        : scene.positions.filter(([position]) => position >= f.from && position <= f.to).map(([position]) => ({ x: position, y: f.valueAt(position) }));
    const seen = valued(points);
    if (seen.length === 0) {
        return `${s.name}: no value in range.`;
    }
    const max = first(seen, (a, b) => a > b);
    return `${s.name}: maximum ${writeValue(s, max.y)} at ${writePosition(axis, max.x)}.`;
}

// A volume panel says the volume, never its series' prices again (ADR-0300).
function volume(s: ChartSeries, axis: ChartAxis, scene: ChartScene): string {
    const seen = s.bars.flatMap(b => (b.volume !== undefined && b.volume !== null && b.x >= scene.xFrom && b.x <= scene.xTo ? [{ x: b.x, y: b.volume }] : []));
    if (seen.length === 0) {
        return `${s.name} volume: no value in range.`;
    }
    const max = first(seen, (a, b) => a > b);
    return `${s.name} volume: maximum ${writeValue(s, max.y)} at ${writePosition(axis, max.x)}.`;
}

interface Valued {
    readonly x: number;
    readonly y: ChartValue;
}

function valued(points: readonly { readonly x: number; readonly y: ChartValue | null }[]): Valued[] {
    return points.flatMap(p => (p.y === null ? [] : [{ x: p.x, y: p.y }]));
}

// The first point whose value `before` puts ahead of every other's, as C#'s MinBy and MaxBy take it.
function first(points: readonly Valued[], before: (a: number, b: number) => boolean): Valued {
    let best = elementAt(points, 0);
    for (const p of points) {
        if (before(toNumber(p.y), toNumber(best.y))) {
            best = p;
        }
    }
    return best;
}

const number = (v: number): string => upToPlaces(v, 4);
