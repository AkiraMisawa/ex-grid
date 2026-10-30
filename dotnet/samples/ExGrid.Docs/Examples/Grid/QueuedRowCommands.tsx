import type { ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { columns, declaredKeys, useQueuedRowCommands } from './QueuedRowCommandsDesk';

export default function QueuedRowCommands(): ReactElement {
    const { source, trace, onDeclaredKey, onRelease, onRemove, onRestore, onAction, onRefused } = useQueuedRowCommands();
    return (
        <>
            <p style={{ margin: '0 0 12px', fontSize: 14 }}>Control+B starts a Consumer operation. Release completes it. Replace the Window while commands wait.</p>
            <div className="bar">
                <button type="button" onClick={onRelease}>Release</button>
                <button type="button" onClick={onRemove}>Remove first two rows</button>
                <button type="button" onClick={onRestore}>Restore rows</button>
            </div>
            <ExGrid source={source} columns={columns} rowHeight={24} viewportHeight={220} viewportWidth="stretch"
                declaredKeys={declaredKeys} onDeclaredKey={onDeclaredKey} onAction={onAction} onRefused={onRefused} />
            <p role="status" style={{ margin: '12px 0 0', fontSize: 14 }}>{trace}</p>
        </>
    );
}
