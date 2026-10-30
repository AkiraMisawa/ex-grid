import { Value } from '@ex-grid/sheet-engine';

/** A cost centre from the application's ledger: its budget for the year and what has been booked against it. */
export interface CostCentre {
    readonly code: string;
    readonly department: string;
    readonly budget: number;
    readonly actual: number;
}

/** The Linked Table's name, as Formulas read it. */
export const costCentresTable = 'CostCentres';

/** The table's columns, in the order every row is pushed. */
export const costCentreColumns: readonly string[] = ['Code', 'Department', 'Budget', 'Actual'];

/** The column a row is read by: no code appears twice. */
export const costCentreKey = 'Code';

/** The cost centre as the table's row: one Value per column, in order. */
export function costCentreRow(centre: CostCentre): readonly Value[] {
    return [Value.fromText(centre.code), Value.fromText(centre.department), Value.fromNumber(centre.budget), Value.fromNumber(centre.actual)];
}

/** The ledger as it opens: invented figures, the same on every visit. */
export function costCentres(): readonly CostCentre[] {
    return [
        { code: 'CC-1100', department: 'Finance', budget: 1_240_000, actual: 1_198_450 },
        { code: 'CC-1200', department: 'Legal', budget: 685_000, actual: 712_300 },
        { code: 'CC-2300', department: 'Operations', budget: 3_420_000, actual: 3_286_900 },
        { code: 'CC-3100', department: 'Technology', budget: 2_950_000, actual: 3_104_750 },
        { code: 'CC-4100', department: 'Marketing', budget: 1_580_000, actual: 1_402_200 },
        { code: 'CC-5200', department: 'People', budget: 920_000, actual: 897_600 },
    ];
}
