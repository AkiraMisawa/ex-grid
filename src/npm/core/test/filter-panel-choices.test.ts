// Layer 1: what a filter panel's choices mean (ADR-0009), in one place, so that the built-in panel and a
// substituted one turn the same choices into the same FilterSpec (ADR-0010). Ported from the C# core's
// FilterPanelChoicesTests, which pin the same answers.
import { describe, expect, it } from 'vitest';
import {
    canApply, ChosenValues, fromCondition, fromConditions, fromSearchedValueList, fromValueList, initialOperand,
    initialOperator, initialSecond, initiallyChosen, offersAddToFilter, stateOfAll, takesOperand, toggleAll, valueIdentity,
} from '../src/chrome/filter-panel-choices';
import { filterOperatorsFor, type FilterOperator, type FilterSpec } from '../src/query/filter';
import type { DistinctValues } from '../src/source/grid-source';

const domain: DistinctValues = { kind: 'values', values: ['Alpha', 'Beta', null, 'Gamma'] };
const inList = (...values: unknown[]): FilterSpec => ({ clauses: [{ operator: 'in', values }] });
const textOf = (value: unknown): string => (typeof value === 'string' ? value : '(Blanks)');
const chosen = (...values: unknown[]): ChosenValues => new ChosenValues(values);

describe('a value list', () => {
    it('ADR-0009: with no filter in force, every value starts chosen', () => {
        expect(initiallyChosen(undefined, domain).values()).toEqual(['Alpha', 'Beta', null, 'Gamma']);
    });

    it('ADR-0009: the applied list starts chosen, intersected with the domain just fetched', () => {
        const start = initiallyChosen(inList('Beta', 'Delta', null), domain);
        expect(start.size).toBe(2);
        expect(start.has('Beta')).toBe(true);
        // A Blank is a value like any other (ADR-0023), and one a source answers as undefined is the same one.
        expect(start.has(null)).toBe(true);
        expect(start.has(undefined)).toBe(true);
        // Not shown, so never chosen unseen.
        expect(start.has('Delta')).toBe(false);
    });

    it('ADR-0009: everything chosen is no filter at all: set membership, never a count', () => {
        expect(fromValueList(domain, chosen('Gamma', null, 'Beta', 'Alpha'))).toBeUndefined();
        // As many values chosen, one of them not in the domain, is not everything.
        expect(fromValueList(domain, chosen('Gamma', null, 'Beta', 'Delta'))).toEqual(inList('Beta', null, 'Gamma'));
    });

    it('ADR-0009: a part chosen is one in clause, in the domain\'s order', () => {
        expect(fromValueList(domain, chosen('Gamma', null))).toEqual(inList(null, 'Gamma'));
    });

    it('ADR-0009, ADR-0023: nothing chosen is never silently no filter, and it is no filter Apply may hand on', () => {
        const spec = fromValueList(domain, chosen());
        expect(spec).toEqual(inList());
        expect(canApply(spec)).toBe(false);
    });

    it('ADR-0009, ADR-0023 (ticket 76): a search with no chosen value among its matches cannot be applied either', () => {
        const everything = chosen('Alpha', 'Beta', null, 'Gamma');
        // "Alpha " matches no value: the text SRV-5's replayed Space left in the search box.
        expect(canApply(fromSearchedValueList(domain, everything, textOf, 'Alpha ', false, undefined))).toBe(false);
        // Alpha matches, and is not chosen.
        expect(canApply(fromSearchedValueList(domain, chosen('Beta', 'Gamma'), textOf, 'alp', false, undefined))).toBe(false);
    });

    it('ADR-0009, FL-13 (ticket 76): added to the filter in force, a search with nothing chosen applies that filter', () => {
        const spec = fromSearchedValueList(domain, chosen(), textOf, 'zzz', true, inList('Beta'));
        expect(spec).toEqual(inList('Beta'));
        expect(canApply(spec)).toBe(true);
    });

    it('ADR-0009: every other answer can be applied: no filter, a part of the list, a condition', () => {
        expect(canApply(undefined)).toBe(true);
        expect(canApply(inList('Alpha'))).toBe(true);
        expect(canApply(inList(null))).toBe(true);
        expect(canApply(fromCondition('isBlank', undefined))).toBe(true);
        expect(canApply(fromCondition('in', 'Alpha'))).toBe(true);
        expect(canApply(fromConditions('startsWith', 'Al', 'or', 'endsWith', 'ma'))).toBe(true);
    });

    it('ADR-0009: a TooMany answer has no list to choose from', () => {
        expect(() => fromValueList({ kind: 'tooMany' }, chosen())).toThrow(TypeError);
        expect(() => initiallyChosen(undefined, { kind: 'tooMany' })).toThrow(TypeError);
    });

    it('ADR-0009, FL-10: with a search, OK applies the checked values among those that match', () => {
        const everything = chosen('Alpha', 'Beta', null, 'Gamma');
        expect(fromSearchedValueList(domain, everything, textOf, 'mm', false, undefined)).toEqual(inList('Gamma'));
        // The search is matched against the text the panel shows, so a Blank is found as "(Blanks)".
        expect(fromSearchedValueList(domain, everything, textOf, 'blank', false, undefined)).toEqual(inList(null));
    });

    it('ADR-0009, FL-10: a checked value the search hides is not applied', () => {
        expect(fromSearchedValueList(domain, chosen('Alpha', 'Gamma'), textOf, 'alp', false, undefined)).toEqual(inList('Alpha'));
    });

    it('ADR-0009, FL-13: added to the filter in force, the matches join it', () => {
        const everything = chosen('Alpha', 'Beta', null, 'Gamma');
        expect(fromSearchedValueList(domain, everything, textOf, 'gam', true, inList('Beta'))).toEqual(inList('Beta', 'Gamma'));
    });

    it('ADR-0009, FL-13: adding keeps a value in force that the domain no longer shows', () => {
        expect(fromSearchedValueList(domain, chosen('Alpha'), textOf, 'alp', true, inList('Delta'))).toEqual(inList('Alpha', 'Delta'));
    });

    it.each([
        ['gam', true, true],
        ['', true, false],
        ['gam', false, false],
    ])('ADR-0009, FL-13: add is offered only while searching a column with a value filter (%j, %j)', (search, valueFilter, offered) => {
        const current: FilterSpec = valueFilter ? inList('Beta') : { clauses: [{ operator: 'contains', value: 'a' }] };
        expect(offersAddToFilter(current, search)).toBe(offered);
    });

    it('ADR-0009: with no search, what is checked is what applies', () => {
        expect(fromSearchedValueList(domain, chosen('Beta'), textOf, '', true, inList('Alpha'))).toEqual(inList('Beta'));
    });

    it('ADR-0009, FL-11: (Select All) reads the values shown: checked, clear or mixed', () => {
        const shown = ['Alpha', 'Gamma'];
        expect(stateOfAll(shown, chosen('Alpha', 'Gamma', 'Beta'))).toBe('checked');
        expect(stateOfAll(shown, chosen('Beta'))).toBe('unchecked');
        expect(stateOfAll(shown, chosen('Gamma'))).toBe('mixed');
    });

    it('ADR-0009, FL-11: toggling (Select All) checks every value shown, or clears them when all were checked', () => {
        const held = chosen('Beta');
        toggleAll(['Alpha', 'Gamma'], held);
        expect(held.size).toBe(3);
        toggleAll(['Alpha', 'Gamma'], held);
        expect(held.values()).toEqual(['Beta']);
    });

    it('ADR-0009: a value is held as the value it is: a number whatever its JavaScript type, a date by the date it names', async () => {
        const { Temporal } = await import('temporal-polyfill');
        expect(valueIdentity(1)).toBe(valueIdentity(1n));
        expect(valueIdentity(1.5)).not.toBe(valueIdentity(1));
        expect(valueIdentity('1')).not.toBe(valueIdentity(1));
        expect(valueIdentity(true)).not.toBe(valueIdentity('true'));
        expect(valueIdentity(Temporal.PlainDate.from('2026-01-05'))).toBe(valueIdentity(Temporal.PlainDate.from('2026-01-05')));
        const object = {};
        expect(valueIdentity(object)).toBe(valueIdentity(object));
        expect(valueIdentity(object)).not.toBe(valueIdentity({}));
        expect(() => valueIdentity(Symbol('x'))).toThrow(TypeError);
    });
});

describe('a condition form', () => {
    it('ADR-0009: the condition in force is what the form starts with', () => {
        const current: FilterSpec = { clauses: [{ operator: 'greaterThan', value: 5 }] };
        expect(initialOperator('number', current, false)).toBe('greaterThan');
        expect(initialOperand(current)).toBe(5);
        // TooMany does not override a condition the user was looking at.
        expect(initialOperator('number', current, true)).toBe('greaterThan');
    });

    it('ADR-0009: TooMany degrades to Excel\'s search box: contains where the type has it', () => {
        expect(initialOperator('text', undefined, true)).toBe('contains');
        expect(initialOperator('number', undefined, true)).toBe(filterOperatorsFor('number')[0]);
        expect(initialOperator('text', undefined, false)).toBe(filterOperatorsFor('text')[0]);
        // An applied value list is not a condition to start from.
        expect(initialOperand(inList('Alpha'))).toBeUndefined();
    });

    it('ADR-0009: isBlank and isNotBlank take no operand; everything else does', () => {
        const all: readonly FilterOperator[] = [...new Set((['text', 'number', 'date', 'boolean'] as const).flatMap(filterOperatorsFor))];
        for (const operator of all) expect(takesOperand(operator)).toBe(operator !== 'isBlank' && operator !== 'isNotBlank');
        expect(all).toContain('isBlank');
    });

    it('ADR-0009: a condition is one clause; an operand-taking operator with none is no filter', () => {
        expect(fromCondition('greaterThan', 5)).toEqual({ clauses: [{ operator: 'greaterThan', value: 5 }] });
        expect(fromCondition('equals', undefined)).toBeUndefined();
        expect(fromCondition('equals', null)).toBeUndefined();
        expect(fromCondition('isBlank', undefined)).toEqual({ clauses: [{ operator: 'isBlank' }] });
    });

    it('ADR-0009, ADR-0023: in offered in a condition form is membership of its one operand: values, never value', () => {
        expect(fromCondition('in', 5)).toEqual({ clauses: [{ operator: 'in', values: [5] }] });
    });

    it('ADR-0009, FL-14: two conditions make one spec of two clauses with the chosen combinator', () => {
        expect(fromConditions('greaterThan', 10, 'or', 'lessThan', 2)).toEqual({
            clauses: [{ operator: 'greaterThan', value: 10 }, { operator: 'lessThan', value: 2 }], combinator: 'or',
        });
    });

    it('ADR-0009, FL-14: an unfinished second condition leaves the first alone', () => {
        expect(fromConditions('greaterThan', 10, 'and', 'lessThan', undefined)).toEqual({ clauses: [{ operator: 'greaterThan', value: 10 }] });
        expect(fromConditions('equals', undefined, 'and', undefined, undefined)).toBeUndefined();
    });

    it('ADR-0009, FL-14: a two-clause filter in force reopens as its two conditions', () => {
        const current: FilterSpec = { clauses: [{ operator: 'greaterThan', value: 10 }, { operator: 'lessThan', value: 2 }], combinator: 'or' };
        expect(initialOperator('number', current, false)).toBe('greaterThan');
        expect(initialOperand(current)).toBe(10);
        expect(initialSecond(current)).toEqual({ operator: 'lessThan', operand: 2, combinator: 'or' });
        expect(initialSecond(inList('Beta'))).toEqual({ operator: undefined, operand: undefined, combinator: 'and' });
    });
});
