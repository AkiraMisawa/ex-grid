import type { ReactElement } from 'react';
import { demoPages } from './demo-pages';

/** The navigation bar every page carries: the way back to the index, and the page's own name. */
export function DemoNav({ route }: { readonly route: string }): ReactElement {
    const page = demoPages.find(entry => entry.route === route);
    return (
        <nav className="demo-nav">
            <a href="/">All pages</a>
            {page === undefined ? null : <span aria-current="page">{page.title}</span>}
        </nav>
    );
}
