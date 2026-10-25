// When a live source's changes reach the grid (ADR-0141, on ADR-0067's rules): the first change after a
// quiet interval at once, and the changes that follow it within the interval as one, at the interval's
// end. It decides only when; its owner holds the changes, and publishes them when told to.

import type { GridClock } from './grid-clock';

/** ADR-0067's interval: a value that changes four times a second still feels live, and one that changes
 * forty times a second cannot be read. */
export const defaultGatherInterval = 250;

/** Decides when gathered changes are published. */
export class ChangeGatherer {
    readonly #clock: GridClock;
    readonly #due: () => void;
    #interval = defaultGatherInterval;
    #lastShown: number | undefined;
    #armedFor: number | undefined;
    #cancel: (() => void) | undefined;

    /** `due` is called when an armed wait ends; the owner then asks `shouldAskNow` again. */
    constructor(clock: GridClock, due: () => void) {
        this.#clock = clock;
        this.#due = due;
    }

    /** The clock the source reads its times from. */
    get clock(): GridClock {
        return this.#clock;
    }

    /** The shortest time, in milliseconds, between two changes reaching the grid. Zero passes every change
     * as it comes; a negative or non-finite interval is refused, since no wait could end before it began. */
    get interval(): number {
        return this.#interval;
    }

    set interval(value: number) {
        if (!Number.isFinite(value) || value < 0) {
            throw new RangeError(`A gathering interval is a finite number of milliseconds from 0, not ${value}: `
                + 'zero passes every change as it comes (ADR-0141, ADR-0067).');
        }
        this.#interval = value;
    }

    /** A change is waiting. True when it should be published now: the interval has passed since the last
     * change reached the grid. False when it waits for the interval's end, for which a wait is armed. */
    shouldAskNow(): boolean {
        const now = this.#clock.now();
        const due = this.#lastShown === undefined ? now : this.#lastShown + this.#interval;
        if (due <= now) {
            this.disarm();
            return true;
        }
        this.#arm(due, due - now);
        return false;
    }

    /** A change reached the grid now: the next one waits the interval from here. */
    shown(): void {
        this.#lastShown = this.#clock.now();
    }

    /** Nothing waits any longer: the changes were published another way, such as a user's own edit. */
    disarm(): void {
        if (this.#armedFor === undefined) return;
        this.#armedFor = undefined;
        this.#cancel?.();
        this.#cancel = undefined;
    }

    /** The armed wait is over; the owner asks `shouldAskNow` again, which arms a new wait if the timer came
     * a moment early. */
    fired(): void {
        this.#armedFor = undefined;
        this.#cancel = undefined;
    }

    #arm(due: number, wait: number): void {
        if (this.#armedFor === due) return;
        this.#cancel?.();
        this.#armedFor = due;
        this.#cancel = this.#clock.setTimer(this.#due, wait);
    }
}
