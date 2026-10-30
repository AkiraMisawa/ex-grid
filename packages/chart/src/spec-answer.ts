// What the tool an AI calls with a Chart Spec answers it (ADR-0320): the refusal, or the chart said in words
// (ADR-0275) with how many pixels were thinned and how many points are missing. The port of the C# engine's
// `ChartSpec.Answer`, held to it by the shared cases under tests/cases/chart/ (ADR-0370, section 3).
import type { ChartData } from './chart-data';
import type { ChartMetrics } from './metrics';
import { buildScene, type ChartScene } from './scene';
import { ChartSpecRefusedError, readChartSpec, type ChartSpecReading } from './spec';
import { chartSummary } from './summary';
import { surfaceSummary } from './surface-mesh';
import { ChartRefusedError } from './values';

/** What the tool answers the AI: the counterpart of C#'s `ChartSpecAnswer`. */
export type ChartSpecAnswer =
    | {
        /** The chart can be drawn. */
        readonly accepted: true;
        /** The chart said in words, to check against what was meant. */
        readonly text: string;
        /** The spec read. */
        readonly reading: ChartSpecReading;
    }
    | {
        /** The chart cannot be drawn. */
        readonly accepted: false;
        /** The refusal, which names the path and the reason, for the AI to act on. */
        readonly text: string;
        /** No reading: nothing was drawn. */
        readonly reading: undefined;
    };

/**
 * Reads `text`, a Chart Spec, against `tables` and answers as the tool answers the AI: the refusal, or the
 * chart said in words with how many pixels were thinned and how many points are missing, at `metrics`
 * (800 by 400 CSS pixels by default). A surface is said as a screen reader hears it, with why a scale it
 * asked for is not drawn. Anything thrown but a refusal of the spec or of the chart is thrown on.
 */
export function answerChartSpec(
    text: string, tables: (handle: string) => ChartData | undefined, metrics: ChartMetrics = { width: 800, height: 400 },
): ChartSpecAnswer {
    let reading: ChartSpecReading;
    try {
        reading = readChartSpec(text, tables);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- ADR-0320: a refused spec is the tool's answer, said to the AI; anything else is rethrown
        if (error instanceof ChartSpecRefusedError) {
            return { accepted: false, text: error.message, reading: undefined };
        }
        throw error;
    }
    const surface = reading.surface;
    if (surface !== undefined) {
        const said = except(surface.allNotes, surface.notes).filter(n => !n.startsWith('Interpolated by'));
        return { accepted: true, text: [surfaceSummary(surface, reading.title), ...said].join(' '), reading };
    }
    const definition = reading.definition;
    let scene: ChartScene;
    try {
        scene = buildScene(definition, metrics);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- ADR-0320: a chart its definition refuses is the tool's answer, as C#'s; anything else is rethrown
        if (error instanceof ChartRefusedError) {
            return { accepted: false, text: error.message, reading: undefined };
        }
        throw error;
    }
    const parts = [chartSummary(definition, scene, reading.title)];
    const thinned = scene.panels.reduce((sum, p) => sum + p.series.reduce((n, s) => n + s.thinned.length, 0), 0);
    parts.push(thinned === 0 ? 'No pixel holds more than one point.'
        : `${String(thinned)} ${thinned === 1 ? 'pixel holds more than one point, and says its range' : 'pixels each hold more than one point, and say their range'}.`);
    const missing = definition.series.reduce((sum, s) => sum + s.points.filter(p => p.y === null).length, 0);
    parts.push(missing === 0 ? 'No point is missing.'
        : `${String(missing)} ${missing === 1 ? 'point is' : 'points are'} missing, drawn as ${missing === 1 ? 'a gap' : 'gaps'}.`);
    parts.push(...except(scene.notes, definition.notes));
    return { accepted: true, text: parts.join(' '), reading };
}

// The distinct lines of `lines` that `said` does not hold, in their order, as C#'s `Except` gives them.
function except(lines: readonly string[], said: readonly string[]): string[] {
    const seen = new Set(said);
    const kept: string[] = [];
    for (const line of lines) {
        if (!seen.has(line)) {
            seen.add(line);
            kept.push(line);
        }
    }
    return kept;
}
