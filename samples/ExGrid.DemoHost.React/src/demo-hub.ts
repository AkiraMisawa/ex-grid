// The demo API server's hub (ADR-0069): what it says on each committed change to the trades, heard here. SignalR
// is the demo's dependency, not a library's: a page passes on what it heard, and neither ExGrid nor ExPivot
// learns how the notice arrived.
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { apiUrl } from './demo-api';

/** What a page listens for on the hub; ITradeNotices on the server names the messages. */
export interface DemoHubNotices {
    /** The data moved on to this Source Version. */
    readonly versionChanged?: (version: string) => void;
    /** Moving on to this Source Version changed, added or removed these trades, by their Record Keys in ordinal
     * order, and of those added the ones booked or put back by a reset. */
    readonly tradesChanged?: (version: string, tradeIds: readonly string[], bookedIds: readonly string[]) => void;
    /** The connection dropped and opened again: whatever the hub said meanwhile was not heard, so the data may have
     * moved on unsaid. */
    readonly reconnected?: () => void;
    /** The connection closed for good, the client having given up reconnecting: nothing the hub says is heard from
     * now. Not told when the page stops the connection itself. */
    readonly lost?: (error: Error) => void;
}

/** A connection to the hub, which a page owns and stops as it goes. */
export interface DemoHub {
    /** Settles once the connection is open, or rejects with why it could not open. */
    readonly started: Promise<void>;
    /** Closes the connection, once it has opened or failed to: a connection cut off while it opens is an error
     * in the browser's console. */
    stop(): Promise<void>;
}

/** Connects to /hubs/trades and hears `notices` until stopped. A page only listens: it calls nothing on the hub,
 * and everything it could ask is on the HTTP API. */
export function connectDemoHub(notices: DemoHubNotices): DemoHub {
    const connection = new HubConnectionBuilder()
        .withUrl(apiUrl('hubs/trades'))
        .withAutomaticReconnect()
        // The client's own information lines would be console noise a layer-3 run reads as a failure.
        .configureLogging(LogLevel.Warning)
        .build();
    const { versionChanged, tradesChanged, reconnected, lost } = notices;
    if (versionChanged !== undefined) connection.on('VersionChanged', versionChanged);
    if (tradesChanged !== undefined) connection.on('TradesChanged', tradesChanged);
    if (reconnected !== undefined) connection.onreconnected(() => { reconnected(); });
    // A connection the page stops closes without an error.
    if (lost !== undefined) connection.onclose(error => { if (error !== undefined) lost(error); });
    const started = connection.start();
    return {
        started,
        stop: async () => {
            try {
                await started;
            } catch { // oxlint-disable-line ex-grid/no-catch -- a connection that never opened has nothing to close; whoever awaits `started` says why
                return;
            }
            await connection.stop();
        },
    };
}
