// A clock a test drives, as C#'s FakeTimeProvider is driven: time moves only when the test advances it, and
// the timers due by then run, in the order they are due, each seeing the time advanced to.
import type { GridClock } from '../src/source/grid-clock';

interface Timer {
    readonly at: number;
    readonly order: number;
    readonly callback: () => void;
    cancelled: boolean;
}

export class FakeClock implements GridClock {
    #now = 0;
    #order = 0;
    #timers: Timer[] = [];

    now(): number {
        return this.#now;
    }

    setTimer(callback: () => void, milliseconds: number): () => void {
        const timer: Timer = { at: this.#now + milliseconds, order: this.#order++, callback, cancelled: false };
        this.#timers.push(timer);
        return () => { timer.cancelled = true; };
    }

    /** Moves the time on, then runs every timer due by then, a timer armed by one of them included. */
    advance(milliseconds: number): void {
        this.#now += milliseconds;
        for (;;) {
            this.#timers = this.#timers.filter(timer => !timer.cancelled);
            const [next] = this.#timers.filter(timer => timer.at <= this.#now).toSorted((a, b) => (a.at === b.at ? a.order - b.order : a.at - b.at));
            if (next === undefined) return;
            next.cancelled = true;
            next.callback();
        }
    }
}
