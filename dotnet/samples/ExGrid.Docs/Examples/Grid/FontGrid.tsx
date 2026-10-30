import type { ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { columns, source } from './FontGridParts';

// The bare grid paints ExGrid's own font, system-ui, and decides #### with the widths ExGrid measured for it: there
// is nothing to set.
export default function FontGrid(): ReactElement {
    return <ExGrid source={source} columns={columns} density="standard" viewportHeight={300} viewportWidth="stretch" />;
}
