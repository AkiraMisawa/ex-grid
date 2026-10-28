import { useEffect, useRef, useState, type ReactElement } from 'react';
import { Button, Stack } from '@mui/material';
import { createPivotLayout } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { fetchFrom, SalesServer } from './SalesServer';

const firstLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Region'],
    values: [{ field: 'Units', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function LiveServerMui(): ReactElement {
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

    // From the state it changes, so two presses before a render cut off and reconnect.
    const toggle = (): void => {
        reachable.current = !reachable.current;
        setCutOff(!reachable.current);
    };

    return (
        <>
            <Stack direction="row" sx={{ mb: 1.5 }}>
                <Button size="small" variant="outlined" onClick={toggle}>{cutOff ? 'Reconnect the server' : 'Cut the server off'}</Button>
            </Stack>
            <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />
        </>
    );
}
