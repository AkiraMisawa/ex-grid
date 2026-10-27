import type { ReactElement } from 'react';
import { MantineExGrid } from '@ex-grid/mantine';
import { answer, columns, source } from './NumericHeaderCopy';

export default function NumericHeadersMantine(): ReactElement {
    return <MantineExGrid withBorder source={source} columns={columns} copyAnswer={answer} viewportHeight={180} viewportWidth="stretch" />;
}
