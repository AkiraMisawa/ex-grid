import { useState, type ReactElement } from 'react';
import { ExBoard } from '@ex-grid/board-react';
import { chartSpec, readChartSpecBoard } from './ChartSpecBoard';

export default function ChartSpecBoardBuiltin(): ReactElement {
    const [spec, setSpec] = useState(chartSpec);
    const [read, setRead] = useState(() => readChartSpecBoard(chartSpec));
    // Edit the spec and leave the field: a column the table does not have is refused by the spec's path.
    return (
        <>
            <textarea rows={6} style={{ width: '100%', fontFamily: 'monospace', fontSize: 12 }} value={spec}
                      onChange={event => { setSpec(event.target.value); }} onBlur={() => { setRead(readChartSpecBoard(spec)); }} />
            <p role="status">{read.refusal}</p>
            {read.board === undefined ? null : <ExBoard board={read.board} data={read.data} width={700} />}
        </>
    );
}
