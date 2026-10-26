// The shape of the array a refused Formula would spill, where its text alone settles it, whatever Values it reads
// (C#'s KnownShape.cs; 2026-10-13, decided with the user: a refused Anchor whose array shape is known spills
// refusals over that shape). Excel's array has that shape whatever the refused Values are, so each cell of it reads
// as refused; where the shape could depend on a Value, nothing is known and nothing spills.
//
// Known: a constant (one Value); a Reference the text names to a rectangle of this Sheet, not a Spill Range (`A1#`)
// and not one that names another Sheet or a Linked Table; an operator over those, which Excel applies to each Value
// and pairs by the larger extent, as the evaluator does; `TRANSPOSE` of a known shape; and a call whose every
// argument is one Value, of a function that cannot give a Reference or an array from one Value (not `SEQUENCE`, nor
// `INDIRECT`, `OFFSET` and the others that can give a Reference). Not known, among the rest: `FILTER` and `UNIQUE`,
// whose extent depends on the Values; `SORT` and `SORTBY`, which may answer one Error Value for an argument they
// refuse; `LET`; and a function given an array where it wants one Value (ADR-0125).
import { equalsIgnoreCase } from '../dotnet';
import type { Node } from './nodes';
import type { Reference } from './reference';
import { ValueArray } from './value-array';

/** An array's extent. */
export interface Shape {
    readonly rows: number;
    readonly columns: number;
}

const one: Shape = { rows: 1, columns: 1 };

const pairedOperators = new Set(['+', '-', '*', '/', '^', '&', '=', '<>', '<', '>', '<=', '>=']);

/** The shape, or `undefined` where the text does not settle it. */
export function knownShape(node: Node, isLocal: (reference: Reference) => boolean): Shape | undefined {
    switch (node.kind) {
        case 'number':
        case 'text':
        case 'boolean':
        case 'error':
        case 'missing':
            return one;
        case 'reference': {
            const reference = node.reference;
            if (reference.spilled || reference.isThreeD || !isLocal(reference)) return undefined;
            const area = reference.area;
            return area.rows * area.columns <= ValueArray.mostCells ? { rows: area.rows, columns: area.columns } : undefined;
        }
        case 'parentheses':
            return knownShape(node.inner, isLocal);
        case 'unary':
        case 'percent':
            return knownShape(node.operand, isLocal);
        case 'binary': {
            // Along the chain of left operands in a loop, left to right: a long chain does not deepen the stack.
            const spine = node.spine;
            let shape: Shape | undefined = knownShape(node.leftmost, isLocal);
            if (shape === undefined) return undefined;
            for (const binary of spine) {
                if (!pairedOperators.has(binary.operator)) return undefined;
                const right = knownShape(binary.right, isLocal);
                if (right === undefined) return undefined;
                const rows: number = shape.rows === 1 ? right.rows : right.rows === 1 ? shape.rows : Math.max(shape.rows, right.rows);
                const columns: number = shape.columns === 1 ? right.columns : right.columns === 1 ? shape.columns : Math.max(shape.columns, right.columns);
                if (rows * columns > ValueArray.mostCells) return undefined;
                shape = { rows, columns };
            }
            return shape;
        }
        case 'function': {
            const definition = node.function;
            if (definition === undefined) return one; // #NAME?, one Value
            const first = node.arguments[0];
            if (equalsIgnoreCase(node.name, 'TRANSPOSE') && node.arguments.length === 1 && first !== undefined) {
                const inner = knownShape(first, isLocal);
                return inner === undefined ? undefined : { rows: inner.columns, columns: inner.rows };
            }
            if (definition.returnsReference || equalsIgnoreCase(node.name, 'SEQUENCE')) return undefined;
            return node.arguments.every(a => isOne(knownShape(a, isLocal))) ? one : undefined;
        }
        case 'name':
        case 'let':
        case 'intersection':
        case 'structuredReference':
            return undefined;
        default:
            throw new Error(`Internal: ${String(node)} is no kind of node.`);
    }
}

function isOne(shape: Shape | undefined): boolean {
    return shape !== undefined && shape.rows === 1 && shape.columns === 1;
}
