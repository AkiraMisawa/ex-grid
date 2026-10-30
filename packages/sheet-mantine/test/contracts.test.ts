// @vitest-environment happy-dom
// The Mantine Chrome keeps the contract every Wrapper keeps with ExSheet's own slots (ADR-0390 section 7): the same
// tests ExSheet for React runs against a Chrome of plain controls, run here against Mantine's components inside a
// MantineProvider, as a Consumer's page renders them. The grid's slots are ExGrid's Mantine Chrome, which
// @ex-grid/mantine holds to their contract. The provider is told it runs under test, as Mantine asks: a page laid out
// by no browser measures every element at 0 by 0, which a Popover otherwise reads as its target scrolled away.
import { createElement, type ReactNode } from 'react';
import { MantineProvider } from '@mantine/core';
import type { ContractHost } from '../../react/test/contract-host';
import { formatCellsSlotContract, sheetTabSlotContract, toolbarItemSlotContract } from '../../sheet-react/test/sheet-slot-contract';
import { mantineSheetChrome } from '../src/chrome';

const host: ContractHost = { wrapper: ({ children }: { readonly children: ReactNode }) => createElement(MantineProvider, { env: 'test' }, children) };

toolbarItemSlotContract('the Mantine Chrome', mantineSheetChrome, host);
formatCellsSlotContract('the Mantine Chrome', mantineSheetChrome, host);
sheetTabSlotContract('the Mantine Chrome', mantineSheetChrome, host);
