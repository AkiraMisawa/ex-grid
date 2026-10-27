// @vitest-environment happy-dom
// The MUI Chrome keeps the contract every Wrapper keeps with ExGrid for React's slots (ADR-0390 section 7): the same
// tests the React adapter runs against a Chrome of plain controls, run here against MUI's components inside a
// ThemeProvider, as a Consumer's page renders them.
import { createElement, type ReactNode } from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { formulaBarSlotContract } from '../../react/test/bar-slot-contract';
import { completionSlotContract } from '../../react/test/completion-slot-contract';
import { cellEditorSlotContract } from '../../react/test/editor-slot-contract';
import { filterPanelSlotContract } from '../../react/test/filter-slot-contract';
import { findPanelSlotContract } from '../../react/test/find-slot-contract';
import { menuSlotContract } from '../../react/test/menu-slot-contract';
import { shownSlotContract } from '../../react/test/shown-slot-contract';
import type { ContractHost } from '../../react/test/contract-host';
import { muiChrome } from '../src/chrome';

const theme = createTheme({ cssVariables: true });
const host: ContractHost = { wrapper: ({ children }: { readonly children: ReactNode }) => createElement(ThemeProvider, { theme }, children) };

menuSlotContract('the MUI Chrome', muiChrome, host);
filterPanelSlotContract('the MUI Chrome', muiChrome, 'OK', 'input[type=search]', host);
findPanelSlotContract('the MUI Chrome', muiChrome, 'Replace All', host);
shownSlotContract('the MUI Chrome', muiChrome, host);
cellEditorSlotContract('the MUI Chrome', muiChrome, host);
formulaBarSlotContract('the MUI Chrome', muiChrome, host);
completionSlotContract('the MUI Chrome', muiChrome, host);
