import { useMemo, type ReactElement } from 'react';
import { ExSheet } from '@ex-grid/sheet-react';
import { diceRolls } from './DiceRolls';

export default function SeededDraws(): ReactElement {
    // The seed is yours to keep: the same seed and the same changes give the same draws whenever the document is opened.
    const document = useMemo(diceRolls, []);
    return <ExSheet document={document} seed={20261011n} viewportHeight={360} viewportWidth="stretch" />;
}
