// The charts made from a grid's selection, as tabs below it: ExChart.Showcases' ChartTabs, what the Chart Dock
// is under MUI, written as page code. It takes the bridge's events: a chart made opens as a new tab, one with a
// series added replaces its tab, a refusal is said above the tabs, every open chart is a target of "Add to
// chart", and a click on a point shows its cell in the grid. Each chart is drawn again from the grid's rows on
// every render, so it follows a recalculation.
import { useSyncExternalStore, type ReactElement } from 'react';
import { ChartRefusedError, type ChartDefinition, type ChartTarget, type GridChart, type GridChartBridge } from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';

interface TabsState {
    readonly charts: readonly ChartTarget[];
    readonly active: string | undefined;
    readonly refusal: string | undefined;
}

/**
 * The open tabs, which the page hands the bridge's callbacks to when it makes the bridge (its options are fixed
 * once made): `open`, `added`, `refused` and `targets`.
 */
export class ChartTabsModel {
    #state: TabsState = { charts: [], active: undefined, refusal: undefined };
    #made = 0;
    readonly #listeners = new Set<() => void>();

    /** Opens `chart` as a new tab, named "Chart N", and selects it. */
    readonly open = (chart: GridChart): void => {
        this.#made += 1;
        const name = `Chart ${String(this.#made)}`;
        this.#set({ charts: [...this.#state.charts, { name, chart }], active: name, refusal: undefined });
    };

    /** Replaces the tab `target` was with `chart`, a series added to it, and selects it. */
    readonly added = (target: ChartTarget, chart: GridChart): void => {
        // A tab closed after its "Add to chart" was chosen has nowhere to take the series: said, never dropped.
        if (!this.#state.charts.some(c => c.name === target.name)) {
            this.refused(`${target.name} was closed, so the series could not be added to it.`);
            return;
        }
        this.#set({
            charts: this.#state.charts.map(c => (c.name === target.name ? { name: c.name, chart } : c)), active: target.name, refusal: undefined,
        });
    };

    /** Says why the bridge made no chart, above the tabs. */
    readonly refused = (why: string): void => { this.#set({ ...this.#state, refusal: why }); };

    /** Every open chart, each a target of "Add to chart". */
    readonly targets = (): readonly ChartTarget[] => this.#state.charts;

    readonly select = (name: string): void => { this.#set({ ...this.#state, active: name }); };

    readonly close = (name: string): void => {
        const charts = this.#state.charts.filter(c => c.name !== name);
        this.#set({ ...this.#state, charts, active: this.#state.active === name ? charts.at(-1)?.name : this.#state.active });
    };

    readonly subscribe = (listener: () => void): (() => void) => {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    };

    readonly snapshot = (): TabsState => this.#state;

    #set(next: TabsState): void {
        this.#state = next;
        for (const listener of this.#listeners) listener();
    }
}

/** The tabs of the charts a bridge makes, held by `tabs`. */
export function ChartTabs<Row>({ tabs, bridge, chartWidth, chartHeight, fileName }: {
    readonly tabs: ChartTabsModel;
    readonly bridge: GridChartBridge<Row>;
    readonly chartWidth: number;
    readonly chartHeight: number;
    readonly fileName: string;
}): ReactElement {
    const { charts, active, refusal } = useSyncExternalStore(tabs.subscribe, tabs.snapshot);
    return (
        <div className="xs-panel xs-charts" role="region" aria-label="Charts">
            {refusal === undefined ? null : <p className="xs-refusal" role="alert">{refusal}</p>}
            {charts.length === 0 ? null : (
                <>
                    <div className="xs-tabs" role="tablist" aria-label="Charts">
                        {charts.map(({ name }) => (
                            <span key={name}>
                                <button type="button" role="tab" className="xs-tab" aria-selected={name === active}
                                    onClick={() => { tabs.select(name); }}>{name}</button>
                                <button type="button" className="xs-tab-close" aria-label={`Close ${name}`} onClick={() => { tabs.close(name); }}>×</button>
                            </span>
                        ))}
                    </div>
                    {charts.filter(c => c.name === active).map(({ name, chart }) => {
                        const drawn = drawnOf(bridge, chart);
                        return (
                            <div key={name} role="tabpanel" aria-label={name}>
                                {typeof drawn === 'string'
                                    ? <p className="xs-refusal" role="alert">{drawn}</p>
                                    : <ExChart definition={drawn} title={name} width={chartWidth} height={chartHeight} fileName={fileName}
                                        onPointClick={click => { void bridge.showInGrid(chart, click); }} />}
                            </div>
                        );
                    })}
                </>
            )}
        </div>
    );
}

/** The chart drawn from the grid's rows as they stand, or the sentence that says why it cannot be. */
function drawnOf<Row>(bridge: GridChartBridge<Row>, chart: GridChart): ChartDefinition | string {
    try {
        return bridge.draw(chart);
    } catch (error) {
        if (error instanceof ChartRefusedError) return error.message;
        throw error;
    }
}
