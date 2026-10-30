import type { ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { useEditingDesk } from './EditingDesk';

export default function EditingGrid(): ReactElement {
    const desk = useEditingDesk();
    return (
        <>
            <div className="bar">
                <button type="button" disabled={!desk.canUndo} onClick={desk.undo}>Undo</button>
                <button type="button" disabled={!desk.canRedo} onClick={desk.redo}>Redo</button>
                <span>{desk.message}</span>
            </div>
            <ExGrid source={desk.source} columns={desk.columns} pinnedColumnCount={1} showFillHandle cellState={desk.paint.cellState}
                cellMessage={desk.paint.cellMessage} onEdit={desk.onEdit} onPaste={desk.onPaste} onFill={desk.onFill} onClear={desk.onClear}
                onRefused={desk.onRefused} onUndo={desk.undo} onRedo={desk.redo} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
