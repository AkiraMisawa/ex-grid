import { useMemo, useState, type ReactElement } from 'react';
import { ExSheet } from '@ex-grid/sheet-react';
import { OwnListChrome } from './OwnListChrome';
import { OwnListPanel } from './OwnListPanel';
import { quarterlyPnl } from './QuarterlyPnl';

// Your own Chrome's toolbar lists, drawn beside the Sheet and on its slot (OwnListChrome).
export default function ToolbarOwnList(): ReactElement {
    const opened = useMemo(quarterlyPnl, []);
    const [chrome] = useState(() => new OwnListChrome());
    return (
        <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
            <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                <ExSheet document={opened} showToolbar chrome={{ toolbarItem: chrome.toolbarItem }}
                    viewportHeight={320} viewportWidth="stretch" />
            </div>
            <div style={{ flex: '0 0 200px' }}>
                <OwnListPanel chrome={chrome} />
            </div>
        </div>
    );
}
