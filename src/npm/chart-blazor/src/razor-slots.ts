// ExChart's Chrome slots filled with Razor (ADR-0410): the kinds of the chart's toolbar and context menu, over the
// Shared Script's RazorSlotHost, which moves the contents in and out. Here is what each slot's context crosses to .NET
// as, and how the contents' controls are wired from the attributes they write (section 2): a command runs, a typed
// range is read and the axis is switched in the browser, so nothing a press means waits on the circuit, and a download
// is saved inside the click that asked for it. ExChart has no Settled Mark and no Input Order, so no kind takes the
// keyboard: the menu's frame is the chart's own, focused as it opens, and Escape on it is the chart's (ADR-0281).

import { drawnVersionAttribute, RazorSlotHost, type DotNetCalls, type HostedSlotKind, type RazorSlotKind } from '@ex-grid/blazor-shared';
import { chartWordIds, type AxisScale, type ChartChrome, type ChartCommand, type ChartMenuSlotContext, type ChartToolbarSlotContext } from '@ex-grid/chart';

export { drawnVersionAttribute };

/** The slots a Razor Chrome fills, as the chart names them. */
export type ChartRazorSlotName = keyof ChartChrome;

/** The attributes the contents write on their controls, as `ChartScriptSlotAttributes` names them in .NET. */
export const chartSlotAttributes = Object.freeze({
    /** On a control that runs one command: the command's id. */
    command: 'data-ex-command',
    /** On Chart Filters' start: an `input` or a `select`, whose value is read as Apply is pressed. */
    from: 'data-ex-chart-from',
    /** On Chart Filters' end, likewise. */
    to: 'data-ex-chart-to',
    /** On the control that applies the typed range. */
    apply: 'data-ex-chart-apply',
    /** On the axis switch: a native `select` whose options' values are `proportional` and `categories`. */
    scale: 'data-ex-chart-scale',
});

/** A command as .NET is handed it. JSON has no undefined: what a command does not say crosses as null. */
function commandJson(command: ChartCommand): object {
    return {
        id: command.id, label: command.label, enabled: command.enabled,
        expanded: command.expanded === undefined ? null : command.expanded, title: command.title === undefined ? null : command.title,
    };
}

/** Runs the command a press on `contents` names, from the context they are wired to: one not enabled there runs
 * nothing, as the chart's own `invoke` refuses it, so contents drawn a round trip behind still mean what the chart now
 * says. */
function wireCommands(contents: HTMLElement, commands: readonly ChartCommand[]): () => void {
    const click = (event: MouseEvent): void => {
        const target = event.target instanceof Element ? event.target.closest(`[${chartSlotAttributes.command}]`) : null;
        if (target === null || !contents.contains(target)) return;
        const id = target.getAttribute(chartSlotAttributes.command);
        const command = commands.find(each => each.id === id);
        if (command === undefined) throw new Error(`Internal: the chart's Razor Chrome drew a control for the command '${String(id)}', which the chart did not hand it.`);
        command.invoke();
    };
    contents.addEventListener('click', click);
    return () => { contents.removeEventListener('click', click); };
}

/** The toolbar's kind: the commands, Chart Filters and the chart's words as data. The range the chart holds is not
 * among them: the script writes it into the fields (below), so that .NET, a round trip behind, never writes over what
 * is being typed (SRV-7). */
const toolbarKind: RazorSlotKind<ChartToolbarSlotContext> = {
    takesKeyboard: false,
    data: toolbar => {
        const filters = toolbar.filters;
        const words = {
            from: toolbar.label(chartWordIds.from), to: toolbar.label(chartWordIds.to), apply: toolbar.label(chartWordIds.apply),
            axis: toolbar.label(chartWordIds.axis), proportional: toolbar.label(chartWordIds.proportional),
            categories: toolbar.label(chartWordIds.categories), downloads: toolbar.label(chartWordIds.downloads), filters: toolbar.label(chartWordIds.filters),
        };
        return {
            json: JSON.stringify({
                name: toolbar.name,
                commands: toolbar.commands.map(commandJson),
                filters: filters === undefined
                    ? null
                    : {
                        values: filters.values, labels: filters.labels, hint: filters.hint, error: filters.error,
                        scale: filters.scale === undefined ? null : filters.scale,
                    },
                words,
            }),
            drawn: undefined,
            memo: undefined,
        };
    },
    wire: (contents, toolbar) => {
        const filters = toolbar.filters;
        const field = (attribute: string): HTMLInputElement | HTMLSelectElement | null => {
            const found = contents.querySelector(`[${attribute}]`);
            if (found === null) return null;
            // A component that writes the attribute on a wrapper marks its own field inside it.
            const control = found instanceof HTMLInputElement || found instanceof HTMLSelectElement ? found : found.querySelector('input, select');
            if (!(control instanceof HTMLInputElement) && !(control instanceof HTMLSelectElement)) {
                throw new Error(`Internal: the chart's Razor Chrome marked ${attribute} on something that holds no field.`);
            }
            return control;
        };
        const from = field(chartSlotAttributes.from);
        const to = field(chartSlotAttributes.to);
        if (filters !== undefined) {
            // The range as the chart holds it replaces what both fields hold whenever it changes (Chart Filters opened,
            // a drag narrowed it, Reset range returned it), as React's Chromes replace it, and a field drawn anew opens
            // with it. An end typed and not applied is kept only while the range stands.
            const range = JSON.stringify([filters.from, filters.to]);
            const moved = givenRange.get(contents) !== range;
            givenRange.set(contents, range);
            shown(from, filters.from, moved);
            shown(to, filters.to, moved);
            const scale = field(chartSlotAttributes.scale);
            if (scale instanceof HTMLSelectElement && filters.scale !== undefined) {
                shown(scale, filters.scale, givenScale.get(scale) !== filters.scale);
                givenScale.set(scale, filters.scale);
            }
        }
        const unwire = wireCommands(contents, toolbar.commands);
        const click = (event: MouseEvent): void => {
            const target = event.target instanceof Element ? event.target.closest(`[${chartSlotAttributes.apply}]`) : null;
            if (target === null || !contents.contains(target) || filters === undefined) return;
            // Read in the browser, as the user left them: an empty end is the axis' own.
            filters.apply(typed(field(chartSlotAttributes.from)), typed(field(chartSlotAttributes.to)));
        };
        const change = (event: Event): void => {
            const target = event.target;
            if (!(target instanceof HTMLSelectElement) || !target.hasAttribute(chartSlotAttributes.scale) || filters === undefined) return;
            filters.setScale(scaleOf(target.value));
        };
        contents.addEventListener('click', click);
        contents.addEventListener('change', change);
        return () => {
            unwire();
            contents.removeEventListener('click', click);
            contents.removeEventListener('change', change);
        };
    },
};

/** The menu's kind: its name and commands as data, and a press on an item running its command, which closes the menu. */
const menuKind: RazorSlotKind<ChartMenuSlotContext> = {
    takesKeyboard: false,
    data: menu => ({ json: JSON.stringify({ name: menu.name, commands: menu.commands.map(commandJson) }), drawn: undefined, memo: undefined }),
    wire: (contents, menu) => wireCommands(contents, menu.commands),
};

// Every slot the chart takes has its kind: a slot added to ChartChrome without one does not compile.
const chartKindsByName = { toolbar: toolbarKind, menu: menuKind } satisfies Record<ChartRazorSlotName, HostedSlotKind>;

const chartKinds: ReadonlyMap<string, HostedSlotKind> = new Map(Object.entries(chartKindsByName));

/** The Razor Chrome of one chart, over `dotnet`: `MountSlot`, `UpdateSlot` and `UnmountSlot` are called on it. */
export function chartRazorSlots(dotnet: DotNetCalls): RazorSlotHost {
    return new RazorSlotHost(dotnet, chartKinds);
}

/** The chart's Chrome for the slots .NET names, each checked against the slots a Razor Chrome fills. */
export function chartRazorChrome(host: RazorSlotHost, names: readonly string[]): ChartChrome {
    for (const name of names) {
        if (!chartKinds.has(name)) throw new TypeError(`Internal: .NET named '${name}' as a chart slot its Chrome fills, which the chart does not take.`);
    }
    return host.mounts(names);
}

// The range the chart last wrote into each toolbar's fields, and the scale into each switch, so that a range the chart
// still holds never replaces what is being typed; and the fields written, so that one drawn anew opens with the range.
const givenRange = new WeakMap<HTMLElement, string>();
const givenScale = new WeakMap<HTMLSelectElement, AxisScale>();
const written = new WeakSet<HTMLInputElement | HTMLSelectElement>();

function shown(field: HTMLInputElement | HTMLSelectElement | null, value: string | null, moved: boolean): void {
    if (field === null || (!moved && written.has(field))) return;
    written.add(field);
    field.value = value ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- an end the chart holds as null is the axis' own, which an empty field means (ChartFiltersSlotContext.from)
}

function typed(field: HTMLInputElement | HTMLSelectElement | null): string | null {
    if (field === null) return null;
    return field.value === '' ? null : field.value;
}

function scaleOf(value: string): AxisScale {
    if (value === 'proportional' || value === 'categories') return value;
    throw new Error(`Internal: the axis switch offered '${value}', which is no scale it switches to.`);
}
