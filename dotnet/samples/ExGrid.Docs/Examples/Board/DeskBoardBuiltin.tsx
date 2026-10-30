import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import type { BoardDiff } from '@ex-grid/board';
import { ExBoard } from '@ex-grid/board-react';
import { deskBoardJson, layOver, readDeskBoard, type DeskBoardReading } from './DeskBoard';

export default function DeskBoardBuiltin(): ReactElement {
    const [json, setJson] = useState(deskBoardJson);
    const [read, setRead] = useState<DeskBoardReading>(() => readDeskBoard(deskBoardJson));
    const [laid, setLaid] = useState<BoardDiff | undefined>(undefined);
    const [refusal, setRefusal] = useState<string | undefined>(read.refusal);
    // What the host hands on to the workflow's next step, and what it keeps for this user: here, only shown.
    const [raised, setRaised] = useState<string | undefined>(undefined);
    const [diff, setDiff] = useState<string | undefined>(undefined);

    const draw = (): void => {
        const next = readDeskBoard(json);
        setLaid(undefined);
        setRead(next);
        setRefusal(next.refusal);
    };

    // Next time: the Board as written now, with the user's changes read back and laid over it.
    const drawAgain = (kept: string): void => {
        const next = readDeskBoard(json);
        setRead(next);
        if (next.refusal !== undefined) {
            setRefusal(next.refusal);
            return;
        }
        const over = layOver(kept);
        setLaid(over.diff);
        setRefusal(over.refusal);
    };

    return (
        <>
            <textarea rows={10} style={{ width: '100%', fontFamily: 'monospace', fontSize: 12 }} value={json}
                      onChange={event => { setJson(event.target.value); }} />
            <div style={{ margin: '8px 0' }}>
                <button type="button" onClick={draw}>Draw</button>
                <button type="button" disabled={diff === undefined} onClick={() => { if (diff !== undefined) drawAgain(diff); }}>Draw with my changes</button>
                <span role="status" style={{ marginLeft: 8 }}>{refusal}</span>
            </div>
            {read.board === undefined ? null : (
                <>
                    <ExBoard board={read.board} data={read.data} width={900} temporal={Temporal} diff={laid}
                             onOutput={output => { setRaised(output.toJson()); }} onDiff={changed => { setDiff(changed.toJson()); }} />
                    <pre role="log" style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>{raised}</pre>
                    <pre role="log" style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>{diff}</pre>
                </>
            )}
        </>
    );
}
