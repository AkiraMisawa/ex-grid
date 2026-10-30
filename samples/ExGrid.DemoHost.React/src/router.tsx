// The host's router: a page per path, as Routes.razor routes the Blazor hosts. A link inside the app is
// followed without a load, as Blazor's are, and `exDemo.navigateTo` is the counterpart of
// `Blazor.navigateTo` that layer 3 drives the app with (tests/browser/fixtures.mjs).
import { useEffect, useSyncExternalStore, type ReactElement } from 'react';

const listeners = new Set<() => void>();

function subscribe(listener: () => void): () => void {
    listeners.add(listener);
    window.addEventListener('popstate', listener);
    return () => {
        listeners.delete(listener);
        window.removeEventListener('popstate', listener);
    };
}

/** Where the app is: the path and the query, as the page reads them. */
function location(): string {
    return window.location.pathname + window.location.search;
}

/** Goes to `href` inside the app, without a load. */
export function navigateTo(href: string): void {
    const target = new URL(href, window.location.href);
    if (target.origin !== window.location.origin) {
        window.location.assign(target.href);
        return;
    }
    window.history.pushState(null, '', target.href);
    for (const listener of listeners) listener();
}

/** The query's value for `name`, or `undefined` where it is not given. */
export function useQuery(name: string): string | undefined {
    const search = useSyncExternalStore(subscribe, () => window.location.search);
    return new URLSearchParams(search).get(name) ?? undefined;
}

/** The page the path names. */
export function Router({ pages }: { readonly pages: Readonly<Record<string, () => ReactElement | null>> }): ReactElement {
    const at = useSyncExternalStore(subscribe, location);
    // A press on a link inside the app is followed here, as Blazor's router follows it.
    useEffect(() => {
        const onClick = (event: MouseEvent): void => {
            if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
            const link = event.target instanceof Element ? event.target.closest('a') : null;
            if (link === null || link.target !== '' || link.hasAttribute('download')) return;
            const target = new URL(link.href, window.location.href);
            if (target.origin !== window.location.origin) return;
            event.preventDefault();
            navigateTo(target.href);
        };
        document.addEventListener('click', onClick);
        return () => { document.removeEventListener('click', onClick); };
    }, []);
    const path = new URL(at, window.location.origin).pathname.replace(/^\/+|\/+$/g, '');
    const Page = pages[path];
    if (Page === undefined) return <p>No page is at /{path}.</p>;
    // Keyed by its path, so a page left is unmounted and the next one is a new instance, as Blazor's
    // router disposes the last page's components.
    return <Page key={path} />;
}
