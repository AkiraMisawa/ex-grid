import { useMemo, useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { ExBoard } from '@ex-grid/board-react';
import { answer, told, toolCall } from './BoardToolCall';

export default function BoardToolCallBuiltin(): ReactElement {
    const [call, setCall] = useState(toolCall);
    // What the tool answers the model: the Board said in words, or one sentence naming the path and the reason.
    const [answered, setAnswered] = useState(() => answer(toolCall));
    // The tables the answer bound, made once per answer: a new object each render would bind the Board afresh.
    const data = useMemo(() => answered.accepted ? Object.fromEntries(answered.bound.data) : undefined, [answered]);
    return (
        <>
            <p style={{ fontSize: 12 }}>The capability said of <code>h-7f3a</code>: <code>{told}</code></p>
            <textarea rows={8} style={{ width: '100%', fontFamily: 'monospace', fontSize: 12 }} value={call}
                      onChange={event => { setCall(event.target.value); }} onBlur={() => { setAnswered(answer(call)); }} />
            <p role="status" style={{ fontSize: 13 }}>{answered.text}</p>
            {answered.accepted && data !== undefined
                ? <ExBoard board={answered.board} data={data} width={900} temporal={Temporal} />
                : null}
        </>
    );
}
