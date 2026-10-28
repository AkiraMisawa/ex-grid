import { useEffect, useRef, useState, type ReactElement } from 'react';
import { createPivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { fetchFrom, SalesServer } from './SalesServer';

const firstLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Region'],
    values: [{ field: 'Units', aggregation: 'sum', numberFormat: '#,##0' }],
});

export default function LiveServer(): ReactElement {
    const [cutOff, setCutOff] = useState(false);
    // Read by the source when it asks, so the source is made once.
    const reachable = useRef(true);
    // Cut off, the next question fails, as an unreachable server's does: ExPivot keeps the report it has as a Stale
    // Report, says so, and mutes its numbers until Retry.
    const [{ server, source }] = useState(() => {
        const made = new SalesServer();
        return { server: made, source: fetchFrom(made, made.fields, () => reachable.current) };
    });
    const [layout, setLayout] = useState(firstLayout);

    // The server amends a few sales every second and pushes each new Source Version: the source hands the notice on,
    // and ExPivot asks the report on screen again.
    useEffect(() => {
        server.startTrading(1000);
        return () => { server.dispose(); };
    }, [server]);

    const toggle = (): void => {
        reachable.current = cutOff;
        setCutOff(!cutOff);
    };

    return (
        <>
            <p>
                <button type="button" onClick={toggle}>{cutOff ? 'Reconnect the server' : 'Cut the server off'}</button>
            </p>
            <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />
        </>
    );
}
