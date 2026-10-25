// What a page records of its start, for tools/cdp-boot.mjs: when the Worker was made and said .NET had
// started, when the document was open and its first Window answered, when the grid first painted a cell
// with text, and the files the Worker fetched. Every time is ms on the page's clock, from navigation.

import type { EngineBoot } from './engine-client';

export interface BootRecord {
    readonly boot: EngineBoot;
    readonly openedAt: number;
    readonly firstWindowAt: number;
    readonly firstPaintAt: number;
    readonly openEngineMs: number;
}

export const recordBoot = (record: BootRecord): void => {
    (window as unknown as { engineBoot: BootRecord }).engineBoot = record;
};

/** Resolves at the animation frame after the grid first holds a cell with text. */
export const waitForPaint = (root: HTMLElement): Promise<number> => new Promise((resolve) => {
    const painted = (): boolean => [...root.querySelectorAll('[role=gridcell]')].some((cell) => (cell.textContent ?? '') !== '');
    const done = (): void => { requestAnimationFrame(() => { resolve(performance.now()); }); };
    if (painted()) { done(); return; }
    const observer = new MutationObserver(() => {
        if (!painted()) return;
        observer.disconnect();
        done();
    });
    observer.observe(root, { subtree: true, childList: true, characterData: true });
});
