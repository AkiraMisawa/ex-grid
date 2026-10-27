// The frame a React Example runs in (ADR-0350 section 12, ADR-0110): the Docs Site opens this page with the
// Example's path and the site's scheme in its address, ?example=Sheet/FirstSheet&scheme=dark, and it runs that one
// Example. An Example is a .tsx file beside the Blazor one under ExGrid.Docs/Examples, whose default export is the
// component; the site shows the same file as the Example's code.
import { StrictMode, type ComponentType } from 'react';
import { createRoot } from 'react-dom/client';
import '../../../src/ExGrid/wwwroot/ex-grid.min.css';
import '../../../src/ExSheet/wwwroot/ex-sheet.min.css';
import './frame.css';

const examples = import.meta.glob<{ readonly default: ComponentType }>('../../ExGrid.Docs/Examples/**/*.tsx');
const prefix = '../../ExGrid.Docs/Examples/';

const query = new URLSearchParams(location.search);
const name = query.get('example');
const scheme = query.get('scheme');

// The site's scheme, declared for the whole document as the site declares it, so the grid's Visual Tokens follow.
if (scheme !== 'light' && scheme !== 'dark') {
    throw new Error(`The scheme is '${String(scheme)}': open this page with ?scheme=light or ?scheme=dark, as the site does.`);
}
document.documentElement.style.colorScheme = scheme;

// Roboto, as the site serves it to its own pages, from the site's own files.
const font = document.createElement('link');
font.rel = 'stylesheet';
font.href = new URL('../fonts/roboto/roboto.css', location.href).href;
document.head.append(font);

const app = document.getElementById('app');
if (app === null) throw new Error('The frame has no #app to run the Example in.');
if (name === null) throw new Error('No Example is named: open this page with ?example=<Folder>/<Name>.');
const load = examples[`${prefix}${name}.tsx`];
if (load === undefined) throw new Error(`There is no React Example ${name}: it is a .tsx file under ExGrid.Docs/Examples.`);
const { default: Example } = await load();
document.title = `${name} — ExGrid Example for React`;
createRoot(app).render(<StrictMode><Example /></StrictMode>);

// The site's frame takes the Example's height, set from here: the frame's page is the site's own origin, so it
// reaches the element it runs in, and the site renders no height that a render of its own would take back. The
// browser tells when the height changes; nothing is measured on a timer.
// Opened on its own, outside a frame, the page has nothing to size.
const frame = window.frameElement;
if (frame !== null) {
    new ResizeObserver(() => {
        // Rounded up: a frame a fraction too short would scroll inside.
        frame.setAttribute('style', `height: ${String(Math.ceil(app.getBoundingClientRect().height))}px`);
    }).observe(app);
}
