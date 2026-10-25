// Layer 1: what the find panel walks and says, apart from any grid. The headers a step walks, in ADR-0333
// §3's order, and the sentence each outcome is worded in (ADR-0055, ADR-0332 §2), as the C# core's
// FindHeaders and FindPanelLabelIds.OutcomeText give them.
import { describe, expect, it } from 'vitest';
import { builtInCommandLabel } from '../src/chrome/command-labels';
import { findOutcomeText, type FindPanelRefusedCell } from '../src/chrome/find-panel-labels';
import { HeaderGroup, HeaderGroupLayout } from '../src/columns/header-groups';
import { columnHeaderId, findHeaders, matchingHeaders, sameHeaders } from '../src/finding/header-find';

const names = ['Book', 'Trader', 'Notional', 'Narrow', 'TradeDate'];
const columns = names.map(name => ({ name, header: name }));

describe('the headers a step walks (ADR-0333 §3)', () => {
    it('ADR-0333: the Header Group rows from the top, then the column header row, each left to right', () => {
        const layout = HeaderGroupLayout.resolve({
            // Declared right to left, and in two tiers: the walk reads them as they stand.
            groups: [
                new HeaderGroup({ label: 'Money', columns: ['Notional', 'Narrow'] }),
                new HeaderGroup({ label: 'Who', columns: ['Book', 'Trader'] }),
                new HeaderGroup({ label: 'Everything', columns: names, tier: 2 }),
            ],
            columnNames: names, pinnedCount: 0,
        });
        const walk = findHeaders(columns, layout);
        expect(walk.map(({ headerRow, text, firstColumn, lastColumn }) => [headerRow, text, firstColumn, lastColumn])).toEqual([
            [0, 'Everything', 0, 4],
            [1, 'Who', 0, 1],
            [1, 'Money', 2, 3],
            [2, 'Book', 0, 0], [2, 'Trader', 1, 1], [2, 'Notional', 2, 2], [2, 'Narrow', 3, 3], [2, 'TradeDate', 4, 4],
        ]);
        expect(matchingHeaders(walk, { text: 'trade', matchCase: false, wholeCell: false })).toEqual([4, 7]);
        expect(matchingHeaders(walk, { text: 'Trade', matchCase: true, wholeCell: true })).toEqual([]);
    });

    it('ADR-0333: a header is known by its column, or a group by its tier and first column, whatever it says', () => {
        const before = findHeaders(columns, HeaderGroupLayout.empty);
        const renamed = findHeaders(columns.map(column => ({ ...column, header: column.header.toUpperCase() })), HeaderGroupLayout.empty);
        expect(sameHeaders(before, renamed)).toBe(true);
        const moved = findHeaders(columns.toReversed(), HeaderGroupLayout.empty);
        expect(sameHeaders(before, moved)).toBe(false);
        expect(before[1]?.id).toBe(columnHeaderId('Trader'));
        expect(sameHeaders(before, before.slice(1))).toBe(false);
    });
});

const german = (id: string): string => (id === 'find-replaced' ? '{count} Zellen ersetzt.' : builtInCommandLabel(id));

describe('what the panel says (ADR-0055, ADR-0332 §2, ADR-0333)', () => {
    const words = builtInCommandLabel;

    it('ADR-0055: a match in a cell says nothing, the moved Focus shows it; every other outcome is worded', () => {
        expect(findOutcomeText({ kind: 'none' }, words)).toBe('');
        expect(findOutcomeText({ kind: 'found' }, words)).toBe('');
        expect(findOutcomeText({ kind: 'notFound' }, words)).toBe('No match');
        expect(findOutcomeText({ kind: 'orderChanged' }, words)).toBe('The rows moved; find again');
        expect(findOutcomeText({ kind: 'sourceChanged' }, words)).toBe('The data was replaced; find again');
        expect(findOutcomeText({ kind: 'foundInHeader', header: 'Trader' }, words)).toBe('Found in the header Trader');
        expect(findOutcomeText({ kind: 'replaced', count: 1 }, words)).toBe('Replaced 1 cell.');
        expect(findOutcomeText({ kind: 'replaced', count: 100 }, words)).toBe('Replaced 100 cells.');
    });

    it('ADR-0332 §2: a refused Replace names its first five cells, each with why, and counts the rest', () => {
        const cells: FindPanelRefusedCell[] = Array.from({ length: 7 }, (_, i) => ({
            rowNumber: i + 1, column: 'Notional', reason: i === 0 ? undefined : `'x${i}' is not a number. `,
        }));
        expect(findOutcomeText({ kind: 'replaceRefused', cells }, words)).toBe('Nothing was replaced. '
            + 'Row 1, Notional: this column cannot be edited. '
            + "Row 2, Notional: 'x1' is not a number. Row 3, Notional: 'x2' is not a number. "
            + "Row 4, Notional: 'x3' is not a number. Row 5, Notional: 'x4' is not a number. And 2 more.");
    });

    it('ADR-0055: the words are the Chrome\'s, by id, where the Consumer gives them', () => {
        expect(findOutcomeText({ kind: 'replaced', count: 3 }, german)).toBe('3 Zellen ersetzt.');
    });
});
