import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import type { BoardDiff } from '@ex-grid/board';
import { ExBoard, type ExBoardChrome } from '@ex-grid/board-react';
import { muiChartChrome } from '@ex-grid/chart-mui';
import { muiChrome } from '@ex-grid/mui';
import { muiPivotChrome } from '@ex-grid/pivot-mui';
import { Alert, Button, Paper, Stack, TextField } from '@mui/material';
import { deskBoardJson, layOver, readDeskBoard, type DeskBoardReading } from './DeskBoard';

// ExBoard has no Wrapper: each kind of view takes its product's MUI Chrome, made once, outside the component.
const chrome: ExBoardChrome = { grid: muiChrome, pivot: muiPivotChrome, chart: muiChartChrome };

// Under a ThemeProvider whose theme is made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css,
// @ex-grid/pivot-mui/styles.css and @ex-grid/chart-mui/styles.css loaded: the Board in an element of the Wrappers'
// classes, which map the views' colours onto the theme, its menus, panels and toolbars MUI's components.
export default function DeskBoardMui(): ReactElement {
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
            <TextField label="Board" multiline rows={10} fullWidth slotProps={{ htmlInput: { style: { fontFamily: 'monospace', fontSize: 12 } } }} value={json}
                       onChange={event => { setJson(event.target.value); }} />
            <Stack direction="row" spacing={1} sx={{ my: 1, alignItems: 'center' }}>
                <Button variant="contained" onClick={draw}>Draw</Button>
                <Button variant="outlined" disabled={diff === undefined} onClick={() => { if (diff !== undefined) drawAgain(diff); }}>Draw with my changes</Button>
                {refusal === undefined ? null : <Alert role="status" severity="warning" sx={{ py: 0 }}>{refusal}</Alert>}
            </Stack>
            {read.board === undefined ? null : (
                <>
                    <Paper variant="outlined" className="mui-ex-grid mui-ex-pivot mui-ex-chart">
                        <ExBoard board={read.board} data={read.data} width={900} temporal={Temporal} diff={laid} chrome={chrome}
                                 onOutput={output => { setRaised(output.toJson()); }} onDiff={changed => { setDiff(changed.toJson()); }} />
                    </Paper>
                    <pre role="log" style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>{raised}</pre>
                    <pre role="log" style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>{diff}</pre>
                </>
            )}
        </>
    );
}
