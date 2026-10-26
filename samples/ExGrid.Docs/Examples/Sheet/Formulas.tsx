import { useMemo, type ReactElement } from 'react';
import { ExSheet } from '@ex-grid/sheet-react';
import { regionalSales } from './RegionalSales';

export default function Formulas(): ReactElement {
    const document = useMemo(regionalSales, []);
    return <ExSheet document={document} pinnedColumnCount={1} viewportHeight={360} viewportWidth="stretch" />;
}
