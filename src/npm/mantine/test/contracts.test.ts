// @vitest-environment happy-dom
// The Mantine Chrome keeps the contract every Wrapper keeps with ExGrid for React's slots (ADR-0390 section 7): the
// same tests the React adapter runs against a Chrome of plain controls, run here against Mantine's components inside
// a MantineProvider, as a Consumer's page renders them.
import { createElement, type ReactNode } from 'react';
import { MantineProvider } from '@mantine/core';
import { formulaBarSlotContract } from '../../react/test/bar-slot-contract';
import { cellEditorSlotContract } from '../../react/test/editor-slot-contract';
import { filterPanelSlotContract } from '../../react/test/filter-slot-contract';
import { findPanelSlotContract } from '../../react/test/find-slot-contract';
import { menuSlotContract } from '../../react/test/menu-slot-contract';
import { shownSlotContract } from '../../react/test/shown-slot-contract';
import type { ContractHost } from '../../react/test/contract-host';
import { mantineChrome } from '../src/chrome';

const host: ContractHost = { wrapper: ({ children }: { readonly children: ReactNode }) => createElement(MantineProvider, null, children) };

menuSlotContract('the Mantine Chrome', mantineChrome, host);
filterPanelSlotContract('the Mantine Chrome', mantineChrome, 'OK', 'input[type=search]', host);
findPanelSlotContract('the Mantine Chrome', mantineChrome, 'Replace All', host);
shownSlotContract('the Mantine Chrome', mantineChrome, host);
cellEditorSlotContract('the Mantine Chrome', mantineChrome, host);
formulaBarSlotContract('the Mantine Chrome', mantineChrome, host);
