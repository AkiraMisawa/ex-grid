// @vitest-environment happy-dom
// The MUI pivot Chrome keeps the contract every Wrapper keeps with ExPivot for React's slots (ADR-0390 section 7): the
// same tests the React adapter runs against a Chrome of plain controls, run here against MUI's components inside a
// ThemeProvider whose theme has CSS variables, as a Consumer's page renders them.
import { createElement, type ReactNode } from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { muiChrome } from '@ex-grid/mui';
import { pivotSlotContract } from '../../pivot-react/test/pivot-slot-contract';
import { muiPivotChrome } from '../src/pivot-chrome';

const theme = createTheme({ cssVariables: true });

pivotSlotContract('the MUI pivot Chrome', { ...muiPivotChrome, grid: muiChrome }, {
    wrapper: ({ children }: { readonly children: ReactNode }) => createElement(ThemeProvider, { theme }, children),
});
