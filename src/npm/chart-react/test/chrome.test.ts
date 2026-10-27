// @vitest-environment happy-dom
// The chart slots' contract run against a Chrome of plain controls (ADR-0390 section 7).
import { chartSlotContract } from './chart-slot-contract';
import { plainChrome } from './plain-chrome';

chartSlotContract('a Chrome of plain controls', plainChrome);
