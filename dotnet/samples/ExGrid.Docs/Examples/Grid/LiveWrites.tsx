import type { ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { columns, feedKeys, useLiveWrites } from './LiveWritesDesk';

export default function LiveWrites(): ReactElement {
    const { source, cellChangedAt, feed, result, advancePrices, onEdit, onOverwriteNotice, onAction } = useLiveWrites();
    return (
        <>
            <div className="bar">
                <button type="button" onClick={advancePrices}>Advance prices (F9)</button>
                <span>{feed}</span>
            </div>
            <ExGrid source={source} columns={columns} cellChangedAt={cellChangedAt} onEdit={onEdit} onOverwriteNotice={onOverwriteNotice}
                onAction={onAction} declaredKeys={feedKeys} onDeclaredKey={advancePrices} viewportHeight={150} viewportWidth="stretch" />
            <p role="status" style={{ margin: '12px 0 0', fontSize: 14 }}>{result}</p>
        </>
    );
}
