import type { ReactElement } from 'react';
import { demoPages } from '../demo-pages';

/** The index: every page of this host, by theme. */
export function Home(): ReactElement {
    const groups = [...new Set(demoPages.map(page => page.group))];
    return (
        <>
            <nav className="demo-nav">
                <span aria-current="page">All pages</span>
            </nav>
            <h1>ExGrid demo host — React</h1>
            <p>
                The pages the Script Core draws under React, from the same catalogue as the Blazor hosts. Each is
                what the browser tests drive on this host.
            </p>
            <div id="demo-index">
                {groups.map(group => (
                    <section className="demo-index-group" key={group}>
                        <h2>{group}</h2>
                        <ul>
                            {demoPages.filter(page => page.group === group).map(page => (
                                <li key={page.route}>
                                    <a href={page.route}>{page.title}</a>
                                    <span className="demo-index-description">{page.description}</span>
                                </li>
                            ))}
                        </ul>
                    </section>
                ))}
            </div>
        </>
    );
}
