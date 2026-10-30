// The differential test's machinery (ADR-0240 §8): a seeded generator of inputs, and a runner that
// sends one sequence twice — once at machine speed, read once the product has settled, and once an
// input at a time, each read after the product has settled — and asks that both end the same.
//
// What a product is driven through, and what of it is compared, is a scenario's (differential.spec.mjs).
// This file knows only seeds, steps and the two runs.

import { expect } from './fixtures.mjs';

/**
 * A pseudo-random sequence of numbers in [0, 1) drawn from `seed` alone (mulberry32), so a sequence
 * of inputs reproduces from the seed printed with its test.
 */
export function random(seed) {
    let state = seed >>> 0;
    return () => {
        state = (state + 0x6D2B79F5) >>> 0;
        let t = state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

/** One of `list`, drawn by `next`. */
export const pick = (next, list) => list[Math.floor(next() * list.length)];

/**
 * The seeds a scenario runs. Every CI run runs the fixed ones, which gate it (decided with the user,
 * 2026-10-24). The Hunt sets EXGRID_DIFF_SEED to a number of its run's, and each run there adds that
 * seed, printed in the test's name, so a red it finds is reproduced by adding the seed to the fixed
 * ones (ADR-0220 §3: a red is a defect, never settled by a re-run). The seed comes from the run, not
 * from Math.random here: Playwright loads this file once to list the tests and again in each worker,
 * and a title drawn twice would name two different tests.
 */
export function seedsOf(fixed) {
    const extra = process.env.EXGRID_DIFF_SEED;
    if (!extra) {
        return fixed;
    }
    const number = Number(extra);
    if (!Number.isInteger(number) || number <= 0) {
        throw new Error(`EXGRID_DIFF_SEED is not a positive whole number: ${extra}`);
    }
    // A run id past mulberry32's range is folded into it; one that folds to 0 takes 1.
    const seed = number % 0x7FFFFFFF || 1;
    return fixed.includes(seed) ? fixed : [...fixed, seed];
}

/**
 * Sends `steps` to a product twice and answers the two readings: `slow`, each input sent once the one
 * before has settled, and `fast`, every input sent at machine speed and read once. Each run opens the
 * product afresh, takes the places its presses aim at from that page before the first input (a press is
 * aimed where it was made), and reads what the scenario reads. A step a scenario acts on by settling
 * (the pivot's `settle`) settles the fast run there too.
 */
export async function bothWays(page, scenario, steps) {
    const run = async (eachSettled) => {
        await scenario.open(page);
        const places = await scenario.places(page);
        for (const step of steps) {
            await scenario.act(page, step, places);
            if (eachSettled) {
                await scenario.settle(page);
            }
        }
        await scenario.settle(page);
        // Every press was aimed where its target stood when the run began; one that moved since may have
        // been missed in one run and not the other, which is the scenario's fault, not the product's.
        expect(await scenario.places(page), 'no target of a press moved during the run').toEqual(places);
        return scenario.read(page);
    };
    const slow = await run(true);
    const fast = await run(false);
    return { slow, fast };
}

/** A step as the test prints it: one line a reader can replay by hand. */
export const describeStep = (step) => (step.kind === 'type' ? `type ${JSON.stringify(step.text)}`
    : step.kind === 'key' ? step.key
        : step.kind === 'settle' ? '(settled)'
        : `${step.kind} ${step.target}${step.shift ? ' (Shift)' : ''}`);
