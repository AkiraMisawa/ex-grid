// The chart's one listener: it tells .NET which date the pointer is over, only when that changes
// and when the pointer leaves — never per mousemove, never a measurement (ADR-0029's shape). The
// report carries the drawing's version, so a report taken against an older drawing is dropped.
export function attach(root, dotnet) {
  let last = null;
  const report = (version, band) => {
    const key = `${version}:${band}`;
    if (key === last) return;
    last = key;
    dotnet.invokeMethodAsync('OnBand', version, band);
  };
  const move = e => {
    const band = e.target.closest?.('[data-ex-chart-band]');
    const svg = root.querySelector('[data-ex-chart-version]');
    if (!svg) return;
    report(Number(svg.dataset.exChartVersion), band ? Number(band.dataset.exChartBand) : -1);
  };
  const leave = () => {
    const svg = root.querySelector('[data-ex-chart-version]');
    report(svg ? Number(svg.dataset.exChartVersion) : -1, -1);
  };
  root.addEventListener('mousemove', move);
  root.addEventListener('mouseleave', leave);
  return {
    dispose() {
      root.removeEventListener('mousemove', move);
      root.removeEventListener('mouseleave', leave);
    },
  };
}
