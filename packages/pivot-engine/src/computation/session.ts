// A report's computation (ADR-0153): the pass, the cube and the report laid out from it, kept up to date from
// the Change Batches a Snapshot's source applies — each batch folded into the pass, and only the leaves, totals
// and rows it touched written again. Every report it returns is immutable. The C# engine's
// PivotComputationSession.
//
// Cancellation is heard only where the session's own state is whole, so a cancelled computation never costs the
// next its incremental state. Before any of it changes, a cancellation stops the work and nothing is kept.
// Folding the pending batches into the pass changes its working indexes in place: from the first batch folded,
// the work goes on, still yielding between slices, until the cube and the report have followed and the result
// is returned, and a cancellation that lands meanwhile is not heard. A new computation — a new question, a
// batch the pass cannot fold, a Pivot Source's answer — and a report laid out afresh are built aside and adopted
// together at the end, so cancelling one leaves the state before as it was. A result's changes are relative to
// the result returned before it, whenever the cancellations landed.

import type { SnapshotChange } from '@ex-grid/data';
import type { PivotSourceRefusal } from '../cube/answer';
import { buildCubeSteps, cubeParts, type CubeBuild, type PivotCube } from '../cube/cube';
import { levelQueryForLayout, queriesEqual, queryForLayout, type PivotLevelWindowInput, type PivotQuery } from '../cube/query';
import { checkCubeHolds } from '../engine';
import { fieldMetas, type PivotField } from '../fields/pivot-field';
import { writePivotLayoutJson } from '../layout/layout-json';
import type { PivotLayout } from '../layout/pivot-layout';
import type { ResolvedOptions } from '../report/labels';
import { ReportBuilder, type PivotReport, type PivotReportRow, type PivotRowKey } from '../report/report';
import { AggregationPass, chunkRows } from '../source/aggregation-pass';
import type { FieldBinding } from '../source/binding';
import {
    queryRefusal, SnapshotPivotSource, snapshotSourceLink, type PivotSource, type SnapshotSourceLink,
} from '../source/pivot-source';
import { Slicer, type CheckedSlicing } from '../source/slicing';
import { ComputationCube } from './cube';
import { ComputationReport } from './report';

// The most Change Batches held for a pass while no Window asks for them; past it the pass is let go.
const pendingBound = 1024;

// The most questions remembered as refused whole past the cap, newest first: a guard on memory, never on the data.
const windowedBound = 16;

/** One complete computation, or the Pivot Source's refusal. */
export interface PivotComputationResult {
    /** The complete, immutable report; `undefined` with a refusal. */
    readonly report: PivotReport | undefined;
    /** Why no report was made. */
    readonly refusal: PivotSourceRefusal | undefined;
    /** Whether the report was laid out afresh — a new question, layout or options, or a computation begun again —
     * so that its rows are to be taken whole rather than as the changes below. */
    readonly isReset: boolean;
    /** The rows the update added, or whose labels changed; none for a report laid out afresh. */
    readonly labelChanges: readonly PivotReportRow[];
    /** The keys of the rows the update took away. */
    readonly removedRows: readonly PivotRowKey[];
    /** Whether the rows' sequence moved from the result returned before. */
    readonly rowSequenceChanged: boolean;
}

/** What a computation is asked: the layout and its options, the cap on leaves, and how it is paced. */
export interface PivotComputationRequest {
    readonly layout: PivotLayout;
    readonly options: ResolvedOptions;
    /** The options written in one form: two that lay a report out alike are equal. */
    readonly optionsKey: string;
    readonly maxLeaves: number;
    /** How the work is paced. */
    readonly slicing: CheckedSlicing;
    /** The request's slices, which its signal cancels: the work built aside runs on them. */
    readonly slicer: Slicer;
    /** Asks a Pivot Source that names no changes again (ADR-0153). */
    readonly refreshData: boolean;
    /** Lays the layout out from the cube held, without the batches pending, where that cube holds it: a layout
     * gesture shows the data it was made on. */
    readonly preferHeld: boolean;
    /** Where the Sibling Windows start, for a Pivot Source that answers Level Aggregates (ADR-0370 section 3): asked
     * once the source refused the question whole past the cap, and from then on for that question. */
    readonly window?: PivotLevelWindowInput;
}

interface Pending {
    readonly change: SnapshotChange;
    readonly bindings: ReadonlyMap<string, FieldBinding>;
}

function refused(refusal: PivotSourceRefusal, reset: boolean): PivotComputationResult {
    return Object.freeze({ report: undefined, refusal, isReset: reset, labelChanges: [], removedRows: [], rowSequenceChanged: false });
}

/** A report's computation over a Pivot Source it does not own. Asked one computation at a time. */
export class PivotComputationSession {
    readonly #source: PivotSource;
    readonly #fields: readonly PivotField[];
    readonly #link: SnapshotSourceLink | undefined;
    readonly #unwatch: (() => void) | undefined;
    #pending: Pending[] = [];
    #pass: AggregationPass | undefined;
    #bindings: ReadonlyMap<string, FieldBinding> | undefined;
    #computation: ComputationCube | undefined;
    // How many times a source that cannot name its changes said its data moved on, and how many of those the
    // cube held was asked after: a held cube older than a notice is not reused.
    #notices = 0;
    #noticesAnswered = 0;
    // The report last returned, and its structure: always laid out from the newest cube the session holds, so
    // the data held and the report shown from it are one.
    #report: PivotReport | undefined;
    #structure: ComputationReport | undefined;
    #layout: string | undefined;
    #options: string | undefined;
    // The questions without windows that the source refused past the cap, newest first: asked with windows from then on.
    #windowed: PivotQuery[] = [];
    #busy = false;
    #disposed = false;

    /** A computation over `source`, under `fields` — the source's own, or with the Order Keys a report's
     * policies give. */
    constructor(source: PivotSource, fields: readonly PivotField[]) {
        this.#source = source;
        this.#fields = fields;
        if (source instanceof SnapshotPivotSource) {
            const link = snapshotSourceLink(source);
            this.#link = link;
            this.#unwatch = link.watch((change, bindings) => {
                // With no pass held and none being made, the next computation reads the Snapshot whole.
                if (this.#pass === undefined && !this.#busy) return;
                this.#pending.push({ change, bindings });
                // Batches no Window asks for are not held without end: past a bound, the pass they would be
                // folded into is let go, and the next computation reads the Snapshot whole. A guard on memory,
                // never on the data.
                if (!this.#busy && this.#pending.length > pendingBound) {
                    this.#pass = undefined;
                    this.#computation = undefined;
                    this.#pending = [];
                }
            });
        }
    }

    /** The newest cube the session holds — the one the report it last returned was laid out from — or
     * `undefined` before its first result. */
    get heldCube(): PivotCube | undefined {
        return this.#report?.cube;
    }

    /** Computes the newest complete report under the request's layout and options, or the Pivot Source's refusal.
     * Rejects with what a layout the cube cannot lay out throws, an Order Key's failure, and — before the state
     * held changes — the signal's reason once it is aborted. */
    async compute(request: PivotComputationRequest): Promise<PivotComputationResult> {
        if (this.#disposed) throw new Error('Internal: a computation session is used after it was disposed.');
        if (this.#busy) throw new Error('Internal: a computation session is asked one computation at a time.');
        this.#busy = true;
        // Set while the computation held is changed in place: a failure then — never a cancellation, which is
        // not heard there — leaves it half made, and it is dropped.
        const state = { inPlace: false };
        try {
            return await this.#compute(request, changing => {
                state.inPlace = changing;
            });
        } catch (error) {
            if (state.inPlace) this.#drop();
            throw error;
        } finally {
            this.#busy = false;
            // Disposed while it ran: what it made is dropped.
            if (this.#isDisposed()) this.#drop();
        }
    }

    async #compute(request: PivotComputationRequest, inPlace: (changing: boolean) => void): Promise<PivotComputationResult> {
        const { layout, options, maxLeaves, slicing } = request;
        // Where a cancellation is heard: work that changes nothing the session keeps.
        const aside = request.slicer;
        const signal = aside.signal;
        signal?.throwIfAborted();
        const source = this.#source;
        // A source that answers Level Aggregates is asked with the groups the layout shows (ADR-0370 section 3): it
        // answers with the leaves when they fit the cap, and with the groups' cells when they do not.
        const unwindowed = source.features.answersLevels ? levelQueryForLayout(layout, maxLeaves) : queryForLayout(layout, maxLeaves);
        // A source that answers Level Aggregates and refused the question whole past the cap is asked for windows of
        // the sets ordered by a value (ADR-0370 section 3); none is asked while the question fits.
        const windowed = request.window === undefined || !source.features.answersLevels ? undefined : levelQueryForLayout(layout, maxLeaves, request.window);
        const windowable = windowed?.levels?.rows.some(rule => rule.order !== undefined) === true;
        let query = windowable && this.#windowed.some(asked => queriesEqual(asked, unwindowed)) ? windowed : unwindowed;
        const layoutName = writePivotLayoutJson(layout);
        // Where it is not: the work that carries batches folded in place through to a whole state.
        let whole: Slicer | undefined;
        let cube: PivotCube;
        // A new computation, built aside and adopted with the report.
        let made: { readonly pass: AggregationPass; readonly bindings: ReadonlyMap<string, FieldBinding>; readonly computation: ComputationCube } | undefined;
        let updated = false;
        let answered = false;
        let noticesAsked = this.#noticesAnswered;
        const held = this.#report;
        const link = this.#link;
        if (request.preferHeld && held !== undefined && holdsAsked(held.cube, layout, query)) {
            cube = held.cube;
        } else if (link !== undefined) {
            const refusal = queryRefusal(source.fields, source.features, query);
            if (refusal !== undefined) return refused(refusal, false);
            const captured = link.capture();
            const changes = this.#pending.filter(pending => pending.change.after.version <= captured.snapshot.version);
            const pass = this.#pass;
            const computation = this.#computation;
            let incremental = pass !== undefined && computation !== undefined && held !== undefined && queriesEqual(pass.query, query);
            if (incremental && pass !== undefined && changes.some(pending => pending.change.after.version > pass.snapshot.version)) {
                inPlace(true);
                whole = new Slicer(slicing, undefined);
                for (const { change, bindings } of changes) {
                    if (change.after.version <= pass.snapshot.version) continue;
                    if (!pass.fold(change, bindings)) {
                        // The batch reaches past what the pass read, or the leaves passed the cap: the pass may be
                        // half folded, and is dropped before anything is awaited. The report held is whole and
                        // stays; the question is asked afresh below.
                        this.#pass = undefined;
                        this.#computation = undefined;
                        inPlace(false);
                        incremental = false;
                        break;
                    }
                    this.#bindings = bindings;
                }
                updated = incremental;
            }
            if (updated && pass !== undefined && computation !== undefined && whole !== undefined) {
                cube = await whole.run(computation.update(pass, link.versionOf(pass.snapshot)));
                pass.clearChanges();
            } else if (incremental && held !== undefined) {
                cube = held.cube;
            } else {
                const fresh = new AggregationPass(captured.snapshot, captured.bindings, query, captured.snapshot.recordKey !== undefined);
                for (let at = 0; at < fresh.rowCount;) {
                    const to = Math.min(fresh.rowCount, at + chunkRows);
                    if (!fresh.step(to)) break;
                    at = to;
                    await aside.step();
                }
                const answer = await aside.run(fresh.answerSteps(captured.version));
                if (answer.refusal !== undefined) return refused(answer.refusal, true);
                // The cube the engine makes, and the nodes it made for each leaf, which the computation starts from.
                let build: CubeBuild | undefined;
                cube = await aside.run(buildCubeSteps(query, answer, fieldMetas(this.#fields), undefined, b => {
                    build = b;
                }));
                if (build === undefined) throw new Error('Internal: the cube was made without what its build made.');
                made = { pass: fresh, bindings: captured.bindings, computation: await aside.run(ComputationCube.create(cube, build, fresh)) };
            }
        } else if (!request.refreshData && this.#noticesAnswered === this.#notices && held !== undefined && holdsAsked(held.cube, layout, query)) {
            // A source that cannot name its changes is asked again for a refresh (ADR-0153), and once it says its
            // data moved on (ADR-0370 section 3).
            cube = held.cube;
        } else {
            answered = true;
            noticesAsked = this.#notices;
            // Refresh goes to the server, past the answer a fetching source folds batches into.
            if (request.refreshData) source.refresh?.();
            await aside.step();
            let answer = await source.aggregate(query, signal);
            signal?.throwIfAborted();
            if (answer.refusal?.kind === 'tooManyLeaves' && query === unwindowed && windowable) {
                this.#windowed = [unwindowed, ...this.#windowed.slice(0, windowedBound - 1)];
                query = windowed;
                await aside.step();
                answer = await source.aggregate(query, signal);
                signal?.throwIfAborted();
            }
            if (answer.refusal !== undefined) return refused(answer.refusal, true);
            cube = await aside.run(buildCubeSteps(query, answer, fieldMetas(this.#fields)));
        }
        const relayout = made !== undefined || answered || held === undefined || this.#structure === undefined
            || this.#layout !== layoutName || this.#options !== request.optionsKey;
        let report: PivotReport;
        let structure: ComputationReport;
        if (relayout) {
            // Laid out afresh, aside: a cancellation drops it, unless the batches it follows were folded in place,
            // which it then completes.
            checkCubeHolds(cube, layout);
            if (!updated) signal?.throwIfAborted();
            structure = new ComputationReport();
            report = await (updated && whole !== undefined ? whole : aside).run(structure.initialize(new ReportBuilder(cube, layout, options, true)));
        } else if (updated && whole !== undefined && this.#structure !== undefined && this.#computation !== undefined) {
            structure = this.#structure;
            report = await whole.run(structure.update(held, cube, this.#computation));
        } else if (this.#structure !== undefined) {
            structure = this.#structure;
            report = held;
        } else {
            throw new Error('Internal: a computation found no report to keep.');
        }

        // Adopted together; nothing below awaits.
        inPlace(false);
        if (made !== undefined) {
            this.#pass = made.pass;
            this.#bindings = made.bindings;
            this.#computation = made.computation;
        }
        const kept = this.#pass;
        if (link !== undefined && kept !== undefined && this.#bindings !== undefined) {
            link.remember(kept.snapshot, this.#bindings);
            this.#pending = this.#pending.filter(pending => pending.change.after.version > kept.snapshot.version);
        }
        this.#report = report;
        this.#structure = structure;
        this.#layout = layoutName;
        this.#noticesAnswered = noticesAsked;
        this.#options = request.optionsKey;
        const changes = updated && !relayout;
        return Object.freeze({
            report,
            refusal: undefined,
            isReset: relayout,
            labelChanges: changes ? Object.freeze([...structure.labelChanges]) : [],
            removedRows: changes ? Object.freeze([...structure.removedRows]) : [],
            rowSequenceChanged: relayout || (updated && structure.rowSequenceChanged),
        });
    }

    #drop(): void {
        this.#windowed = [];
        this.#pass = undefined;
        this.#bindings = undefined;
        this.#computation = undefined;
        this.#report = undefined;
        this.#structure = undefined;
        this.#layout = undefined;
        this.#options = undefined;
    }

    /** Tells the session that a source that cannot name its changes said its data moved on: the next
     * computation asks it again rather than lay the held cube out. */
    sourceMoved(): void {
        this.#notices++;
    }

    /** Stops hearing changes and lets the computation go; the Pivot Source stays. */
    dispose(): void {
        if (this.#disposed) return;
        this.#disposed = true;
        this.#unwatch?.();
        this.#pending = [];
        if (!this.#busy) this.#drop();
    }

    // Read through a call: a computation awaits, and the session may be disposed meanwhile.
    #isDisposed(): boolean {
        return this.#disposed;
    }
}

// Whether a cube held answers the question a layout asks: it holds the layout under the same cap, and a cube of
// Sibling Windows was asked for them where this question asks — the same orders, collapse and starts.
function holdsAsked(cube: PivotCube, layout: PivotLayout, query: PivotQuery): boolean {
    return cube.holds(layout) && cube.query.maxLeaves === query.maxLeaves && (cubeParts(cube).windows.size === 0 || queriesEqual(cube.query, query));
}
