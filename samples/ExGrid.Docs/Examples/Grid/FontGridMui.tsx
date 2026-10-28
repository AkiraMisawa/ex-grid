import type { ReactElement } from 'react';
import { MuiExGrid } from '@ex-grid/mui';
import { columns, source, systemFont } from './FontGridParts';

// The system font in place of Roboto, with the widths measured for it.
export default function FontGridMui(): ReactElement {
    return <MuiExGrid font={systemFont} source={source} columns={columns} viewportHeight={300} viewportWidth="stretch" />;
}
