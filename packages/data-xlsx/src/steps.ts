// A streamed part read a step at a time. The XML reader holds the text that has arrived; a step reads items from it,
// synchronously, while at least `margin` characters lie ahead, and ends asking for more, pausing for the pacer, or done.
// An item larger than what lies ahead makes the reader throw `NeedMore`: the step is then rewound — the reader to the
// mark taken before it, the caller's state by the rewind its `save` returned — and read again with more text.

import { NeedMore, type XmlReader } from './xml-reader';

/** The text a step keeps ahead of the item it reads next: no item a workbook writes in its streamed parts comes near it,
 * so a step is rarely read twice. */
const margin = 64 * 1024;

/** The text a step is given before it starts. */
const ahead = 1 << 20;

/** How a step ended: wanting more text, its state kept; pausing for the pacer; or with nothing more to read. */
export type StepEnd = 'more' | 'pause' | 'done';

/** Whether a step should end for more text before reading another item. */
export function low(xml: XmlReader): boolean {
    return !xml.ended() && xml.unread() < margin;
}

/**
 * Runs `step` until it pauses or is done, giving the reader more text whenever it asks. `save` is called before each
 * step and returns what puts the caller's state back, for a step read again.
 */
export async function runSteps(xml: XmlReader, save: () => () => void, step: () => StepEnd): Promise<'pause' | 'done'> {
    let want = ahead;
    for (;;) {
        await xml.fill(want);
        const mark = xml.snapshot();
        const rewind = save();
        let end: StepEnd;
        try {
            end = step();
            // oxlint-disable-next-line ex-grid/no-catch -- NeedMore never escapes: the step is read again with more text
        } catch (error) {
            if (!(error instanceof NeedMore)) throw error;
            xml.restore(mark);
            rewind();
            want = Math.max(want, xml.unread()) * 2;
            continue;
        }
        if (end !== 'more') return end;
        want = ahead;
    }
}
