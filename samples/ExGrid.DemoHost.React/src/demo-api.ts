// The demo API server (ADR-0069), as DemoApiClient finds it on the Blazor hosts.

/** A path on the demo API server, at the page's own port plus 3000. */
export function apiUrl(path: string): string {
    const { protocol, hostname, port } = window.location;
    // A page served on its scheme's own port names none.
    const served = port === '' ? (protocol === 'https:' ? 443 : 80) : Number(port);
    return `${protocol}//${hostname}:${served + 3000}/${path}`;
}

/** What a page says when the demo API server does not answer, as DemoApiClient.NotAnswering says it. */
export function notAnswering(error: unknown): string {
    return `The demo API server is not answering: ${String(error)}`;
}
