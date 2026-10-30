import type { ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { answer, columns, source } from './NumericHeaderCopy';

export default function NumericHeaders(): ReactElement {
    return <ExGrid source={source} columns={columns} copyAnswer={answer} viewportHeight={180} viewportWidth="stretch" />;
}
