// What a Wrapper's contract run needs around the grid (ADR-0390 section 7): a design system whose components read a
// provider of its own, Mantine's MantineProvider, is rendered inside it, as a Consumer's page renders it.
import type { ComponentType, ReactNode } from 'react';

/** The element a contract renders its ExGrid inside, where the Wrapper's components need one. */
export interface ContractHost {
    /** Wraps the rendered grid, as `@testing-library/react`'s `wrapper` does. */
    readonly wrapper?: ComponentType<{ readonly children: ReactNode }>;
}
