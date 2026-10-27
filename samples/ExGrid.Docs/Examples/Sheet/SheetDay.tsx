import { useMemo, useState, type ReactElement } from 'react';
import { ExSheet } from '@ex-grid/sheet-react';
import { dayAndTime } from './DayAndTime';

const zones = ['Asia/Tokyo', 'Europe/London', 'America/New_York', 'Pacific/Kiritimati'];
const reportDay = { year: 2026, month: 3, day: 31 };

export default function SheetDay(): ReactElement {
    const document = useMemo(dayAndTime, []);
    const [zone, setZone] = useState('');
    const [fixed, setFixed] = useState(false);
    // The day is the user's, in the browser's zone, unless you name another.
    return (
        <>
            <div className="bar">
                <label>
                    Time zone{' '}
                    <select value={zone} onChange={event => { setZone(event.target.value); }}>
                        <option value="">The browser's</option>
                        {zones.map(name => <option key={name} value={name}>{name}</option>)}
                    </select>
                </label>
                <label><input type="checkbox" checked={fixed} onChange={event => { setFixed(event.target.checked); }} /> Fix the day at 31 March 2026</label>
            </div>
            <ExSheet document={document} timeZone={zone === '' ? undefined : zone} today={fixed ? reportDay : undefined}
                viewportHeight={200} viewportWidth="stretch" />
        </>
    );
}
