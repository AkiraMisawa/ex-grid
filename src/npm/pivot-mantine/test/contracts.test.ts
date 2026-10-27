// @vitest-environment happy-dom
// The Mantine pivot Chrome keeps the contract every Wrapper keeps with ExPivot for React's slots (ADR-0390): the
// same tests the React adapter runs against a Chrome of plain controls, run here against Mantine's components inside
// a MantineProvider, as a Consumer's page renders them.
import { createElement, type ReactNode } from 'react';
import { MantineProvider } from '@mantine/core';
import { mantineChrome } from '@ex-grid/mantine';
import { pivotSlotContract } from '../../pivot-react/test/pivot-slot-contract';
import { mantinePivotChrome } from '../src/pivot-chrome';

pivotSlotContract('the Mantine pivot Chrome', { ...mantinePivotChrome, grid: mantineChrome }, {
    wrapper: ({ children }: { readonly children: ReactNode }) => createElement(MantineProvider, null, children),
});
