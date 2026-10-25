import { test, expect, setRoundTrip } from './fixtures.mjs';
import { random, seedsOf, bothWays, describeStep } from './differential.mjs';
import { scenarios } from './differential-products.mjs';

// The Input Order's acceptance (ADR-0240 §8, ticket 07): in each product that takes input, a
// sequence of keys, presses, typed text and commands drawn from a seed ends the same sent at machine
// speed as sent one input at a time, each read after the product has settled. On the Server host the
// round trip is 150 ms through the latency proxy, so the fast run's inputs leave long before their
// answers come back; on WebAssembly the same test is the case without one.
//
// The seeds below gate every CI run; the Hunt adds one of its run's per scenario (differential.mjs, seedsOf).
// A red names its seed and prints its steps: add the seed here if it is not, and it reproduces.
//
// What a sequence may hold is kept to inputs whose outcome does not depend on what the user saw when
// making them. A press is aimed at a place on screen, and both runs aim it at the same place, so
// nothing may scroll or reorder what is under it: the moves are bounded to the rows and columns in
// view, and nothing sorts or filters. A Tab
// typed straight after an Escape is left out: whether the grid has let Tab go yet is said aloud when
// it has not (ADR-0230 §1 (b)), and a Tab's move is the browser's, which script cannot make for it.

function differential(product, scenario, makeSteps, fixedSeeds) {
    for (const seed of seedsOf(fixedSeeds)) {
        test(`ADR-0240 §8: ${product} ends the same typed fast and slow, seed ${seed}`, async ({ page }, testInfo) => {
            test.setTimeout(240_000);
            const steps = makeSteps(random(seed));
            testInfo.annotations.push({ type: 'seed', description: String(seed) });
            testInfo.annotations.push({ type: 'steps', description: steps.map(describeStep).join(' · ') });
            console.log(`[differential] ${product}, seed ${seed}: ${steps.map(describeStep).join(' · ')}`);
            await setRoundTrip(150);
            const { slow, fast } = await bothWays(page, scenario, steps);
            expect(fast, `seed ${seed}: the inputs sent at machine speed end where they end one at a time`).toEqual(slow);
        });
    }
}

for (const [product, { scenario, steps }] of Object.entries(scenarios)) {
    differential(product, scenario, steps, [1, 2, 3, 987654321]);
}
