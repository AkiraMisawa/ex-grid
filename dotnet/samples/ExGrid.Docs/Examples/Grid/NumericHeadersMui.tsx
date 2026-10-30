import type { ReactElement } from 'react';
import { MuiExGrid } from '@ex-grid/mui';
import { answer, columns, source } from './NumericHeaderCopy';

export default function NumericHeadersMui(): ReactElement {
    return <MuiExGrid source={source} columns={columns} copyAnswer={answer} viewportHeight={180} viewportWidth="stretch" />;
}
