import type { ReactElement } from 'react';
import { MuiExGrid } from '@ex-grid/mui';
import { cellAppearance, cellMessage, columns, source } from './DraftFillsParts';

// A Fill per draft, split diagonally where both drafts changed the cell. The colours are the Consumer's, not the theme's.
export default function DraftFillsMui(): ReactElement {
    return (
        <MuiExGrid source={source} columns={columns} cellAppearance={cellAppearance} cellMessage={cellMessage} pinnedColumnCount={1}
            viewportHeight={300} viewportWidth="stretch" />
    );
}
