import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import type { BoardDiff } from '@ex-grid/board';
import { ExBoard, type ExBoardChrome } from '@ex-grid/board-react';
import { mantineChartChrome } from '@ex-grid/chart-mantine';
import { mantineChrome } from '@ex-grid/mantine';
import { mantinePivotChrome } from '@ex-grid/pivot-mantine';
import { Alert, Button, Group, Paper, Textarea } from '@mantine/core';
import { deskBoardJson, layOver, readDeskBoard, type DeskBoardReading } from './DeskBoard';

// ExBoard has no Wrapper: each kind of view takes its product's Mantine Chrome, made once, outside the component.
const chrome: ExBoardChrome = { grid: mantineChrome, pivot: mantinePivotChrome, chart: mantineChartChrome };

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css,
// @ex-grid/pivot-mantine/styles.css and @ex-grid/chart-mantine/styles.css loaded: the Board in an element of the
// Wrappers' classes, which map the views' colours onto the theme, its menus, panels and toolbars Mantine's components.
export default function DeskBoardMantine(): ReactElement {
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
            <Textarea label="Board" autosize minRows={10} maxRows={10} styles={{ input: { fontFamily: 'monospace', fontSize: 12 } }} value={json}
                      onChange={event => { setJson(event.currentTarget.value); }} />
            <Group my="xs">
                <Button onClick={draw}>Draw</Button>
                <Button variant="outline" disabled={diff === undefined} onClick={() => { if (diff !== undefined) drawAgain(diff); }}>Draw with my changes</Button>
                {refusal === undefined ? null : <Alert role="status" color="yellow" py={4}>{refusal}</Alert>}
            </Group>
            {read.board === undefined ? null : (
                <>
                    <Paper className="mantine-ex-grid mantine-ex-pivot mantine-ex-chart">
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
