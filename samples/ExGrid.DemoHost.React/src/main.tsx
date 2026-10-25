// The React DemoHost (ADR-0350 sections 4 and 13): the Script Core under React, serving the Blazor hosts'
// page catalogue at the same paths, so layer 3 drives all three alike.
import { StrictMode, useEffect, useState, type ReactElement } from 'react';
import { createRoot } from 'react-dom/client';
import '../../ExGrid.DemoPages/wwwroot/app.css';
import '../../../src/ExGrid/wwwroot/ex-grid.min.css';
import { Home } from './pages/home';
import { Virtual } from './pages/virtual';
import { Wide } from './pages/wide';
import { navigateTo, Router } from './router';

declare global {
    interface Window {
        /** How layer 3 moves the app to another page without a load. */
        exDemo: { readonly navigateTo: (href: string) => void };
    }
}

window.exDemo = Object.freeze({ navigateTo });

const pages = { '': Home, virtual: Virtual, wide: Wide };

/** Present once the page handles events, as Routes.razor's marker is: at once under React. */
function Interactive(): ReactElement | null {
    const [interactive, setInteractive] = useState(false);
    useEffect(() => { setInteractive(true); }, []);
    return interactive ? <span id="demo-interactive" hidden /> : null;
}

const app = document.getElementById('app');
if (app === null) throw new Error('index.html has no #app to mount the host in.');
createRoot(app).render(
    <StrictMode>
        <Router pages={pages} />
        <Interactive />
    </StrictMode>,
);
