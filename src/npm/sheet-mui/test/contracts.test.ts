// @vitest-environment happy-dom
// The MUI Chrome keeps the contract every Wrapper keeps with ExSheet's own slots (ADR-0390 section 7): the same tests
// ExSheet for React runs against a Chrome of plain controls, run here against MUI's components inside a ThemeProvider
// whose theme has CSS variables, as a Consumer's page renders them. The grid's slots are ExGrid's MUI Chrome, which
// @ex-grid/mui holds to their contract.
import { createElement, type ReactNode } from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import type { ContractHost } from '../../react/test/contract-host';
import { formatCellsSlotContract, sheetTabSlotContract, toolbarItemSlotContract } from '../../sheet-react/test/sheet-slot-contract';
import { muiSheetChrome } from '../src/chrome';

const theme = createTheme({ cssVariables: true });
const host: ContractHost = { wrapper: ({ children }: { readonly children: ReactNode }) => createElement(ThemeProvider, { theme }, children) };

toolbarItemSlotContract('the MUI Chrome', muiSheetChrome, host);
formatCellsSlotContract('the MUI Chrome', muiSheetChrome, host);
sheetTabSlotContract('the MUI Chrome', muiSheetChrome, host);
