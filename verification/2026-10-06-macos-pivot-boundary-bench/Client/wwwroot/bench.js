// Measurement-only JS. Each sample observes a completed Blazor render and two rAFs.
window.bench = {
  ready: false,
  attach(reference) { this.reference = reference; this.ready = true; },
  async prepare(mode, leaves, records) { return this.reference.invokeMethodAsync('Prepare', mode, leaves, records); },
  run(action, batch = 1, visible = true) {
    const start = performance.now();
    this.start = start;
    this.longTasks = [];
    this.observer = new PerformanceObserver(list => this.longTasks.push(...list.getEntries().map(e => ({ start: e.startTime, duration: e.duration }))));
    this.observer.observe({ type: 'longtask' });
    return new Promise((resolve, reject) => {
      this.resolve = resolve;
      this.reference.invokeMethodAsync('Run', action, batch, visible).catch(reject);
    });
  },
  complete(result) {
    const rendered = performance.now();
    requestAnimationFrame(() => requestAnimationFrame(() => {
      this.observer.disconnect();
      this.resolve({ ...result, actionToFrameMs: performance.now() - this.start,
        renderToFrameMs: performance.now() - rendered, longTasks: this.longTasks });
    }));
  },
  verify() { return this.reference.invokeMethodAsync('Verify'); },
  windowHash() { return this.reference.invokeMethodAsync('WindowHash'); },
  memory() { return this.reference.invokeMethodAsync('Memory'); }
};
