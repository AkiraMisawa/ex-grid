import type { ReactElement } from 'react';
import { ExSheet } from '@ex-grid/sheet-react';

// Ten rows by ten columns, A1:J10: nothing lies past them, and every move stops at their edge.
const size = { rows: 10, columns: 10 };

export default function SmallSheet(): ReactElement {
    return <ExSheet sheetSize={size} viewportHeight={360} viewportWidth="stretch" showSheetTabs={false} />;
}
