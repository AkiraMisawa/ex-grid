// The React DemoHost (ADR-0350 sections 4 and 13): the Script Core under React, serving the Blazor hosts'
// page catalogue at the same paths, so layer 3 drives all three alike.
import { StrictMode, useEffect, useState, type ReactElement } from 'react';
import { createRoot } from 'react-dom/client';
import '../../ExGrid.DemoPages/wwwroot/app.css';
import '../../../src/ExGrid/wwwroot/ex-grid.min.css';
import '../../../src/ExChart/wwwroot/ex-chart.min.css';
import '../../../src/ExPivot/wwwroot/ex-pivot.min.css';
import '../../../src/ExSheet/wwwroot/ex-sheet.min.css';
import { Cells } from './pages/cells';
import { Chart } from './pages/chart';
import { ChartBridge } from './pages/chart-bridge';
import { ChartCandles } from './pages/chart-candles';
import { Features } from './pages/features';
import { Fetch } from './pages/fetch';
import { FilterPanel } from './pages/filter-panel';
import { Home } from './pages/home';
import { Scatter } from './pages/scatter';
import { Surface } from './pages/surface';
import { Pivot } from './pages/pivot';
import { SheetDemo } from './pages/sheet';
import { Marks } from './pages/marks';
import { Virtual } from './pages/virtual';
import { Wide } from './pages/wide';
import { navigateTo, Router } from './router';
import { BinomialLatticeShowcase } from './showcases/binomial-lattice';
import { CdsSpreadShowcase } from './showcases/cds-spread';
import { EquityIndexShowcase } from './showcases/equity-index';
import { ExposureShowcase } from './showcases/exposure';
import { FxVolatilityShowcase } from './showcases/fx-volatility';

declare global {
    interface Window {
        /** How layer 3 moves the app to another page without a load. */
        exDemo: { readonly navigateTo: (href: string) => void };
    }
}

window.exDemo = Object.freeze({ navigateTo });

const pages = { '': Home, virtual: Virtual, wide: Wide, fetch: Fetch, features: Features, 'filter-panel': FilterPanel, cells: Cells, chart: Chart, 'chart-bridge': ChartBridge, 'chart-candles': ChartCandles, scatter: Scatter, surface: Surface,
    'chart-showcase/exposure': ExposureShowcase, 'chart-showcase/cds-spread': CdsSpreadShowcase,
    'chart-showcase/fx-volatility': FxVolatilityShowcase, 'chart-showcase/equity-index': EquityIndexShowcase,
    'chart-showcase/binomial-lattice': BinomialLatticeShowcase,
    pivot: Pivot,
    sheet: SheetDemo,
    marks: Marks,
};

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
