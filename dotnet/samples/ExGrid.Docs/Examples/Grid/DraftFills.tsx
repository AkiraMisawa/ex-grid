import type { ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { cellAppearance, cellMessage, columns, source } from './DraftFillsParts';

// A Fill per draft, split diagonally where both drafts changed the cell.
export default function DraftFills(): ReactElement {
    return (
        <ExGrid source={source} columns={columns} cellAppearance={cellAppearance} cellMessage={cellMessage} pinnedColumnCount={1}
            viewportHeight={300} viewportWidth="stretch" />
    );
}
