import { useState, type ReactElement } from 'react';
import { ExBoard } from '@ex-grid/board-react';
import type { Snapshot } from '@ex-grid/data';
import { serverBoard, serverSources } from './ServerBoard';

const noData: Readonly<Record<string, Snapshot>> = {};

// The rows are fetched a page at a time; a selected row hands the server its desk, and the pivot is asked again.
export default function ServerBoardBuiltin(): ReactElement {
    // Made once: a new source is a new binding, and every view is asked again.
    const [sources] = useState(serverSources);
    return <ExBoard board={serverBoard} data={noData} sources={sources} width={900} />;
}
