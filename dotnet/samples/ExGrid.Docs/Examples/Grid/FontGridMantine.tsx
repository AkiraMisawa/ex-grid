import type { ReactElement } from 'react';
import { MantineExGrid } from '@ex-grid/mantine';
import { columns, source, systemFont } from './FontGridParts';

// The system font in place of Mantine's own stack, with the widths measured for it.
export default function FontGridMantine(): ReactElement {
    return <MantineExGrid withBorder font={systemFont} source={source} columns={columns} viewportHeight={300} viewportWidth="stretch" />;
}
