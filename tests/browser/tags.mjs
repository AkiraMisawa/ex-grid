// Tags a test gives in its details, `test(title, { tag }, …)`, read by the harness's own tools as well as by tests.
// Kept apart from fixtures.mjs so a tool that only lists tests (shard-files.mjs) need not load the fixtures.

// A test far longer than a shard's share of tests allows for: the daily hunt sends its file's tests round its
// shards one at a time instead of keeping the file in one (shard-files.mjs).
export const SPREAD = '@spread';
