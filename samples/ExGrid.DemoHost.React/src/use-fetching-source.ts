// A source a page makes is the page's to dispose (ADR-0025): the grid never owns one.
import { useEffect, useState, type DependencyList } from 'react';
import type { FetchingGridSource } from '@ex-grid/core';

/**
 * A source made when the page mounts, or when `deps` change, and disposed when the page leaves or makes
 * another. Made in an effect rather than during render, so StrictMode's second mount gets a source of its
 * own and never one its first unmount disposed; until then there is none, and the page draws no grid.
 */
export function useFetchingSource<Row>(make: () => FetchingGridSource<Row>, deps: DependencyList): FetchingGridSource<Row> | undefined {
    const [source, setSource] = useState<FetchingGridSource<Row>>();
    useEffect(() => {
        const made = make();
        setSource(made);
        return () => { made.dispose(); };
        // The page names what the source is made from in `deps`; `make` is a new closure on every render.
    }, deps);
    return source;
}
