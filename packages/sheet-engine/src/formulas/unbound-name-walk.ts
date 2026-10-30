// The names a parsed Formula reads that nothing in it binds (C#'s UnboundNameWalk.cs; `Entry.unboundNames`):
// every name node of the tree the parser has already built, less those a `LET` binds and those a `LAMBDA` takes as
// parameters, whether or not the engine declares `LAMBDA`. Today it does not (a call is `#NAME?`), but Excel's
// workbooks write it, and its parameters are no names a workbook defines (ADR-0330 §4): `LAMBDA(x, y, x+y)` binds
// `x` and `y` in its last argument, and its other arguments are the parameters themselves.
import { equalsIgnoreCase } from '../dotnet';
import { firstWide } from './lexer';
import type { Node } from './nodes';

/** The unbound names of a Formula, in the text's order, each once without regard to case. */
export function unboundNames(formula: Node): readonly string[] {
    const found: string[] = [];
    walk(formula, [], found);
    return distinctIgnoringCase(found);
}

/** The texts, each once without regard to case, the first spelling kept, in their order. */
export function distinctIgnoringCase(texts: readonly string[]): string[] {
    const kept: string[] = [];
    for (const text of texts) if (!kept.some(k => equalsIgnoreCase(k, text))) kept.push(text);
    return kept;
}

function walk(node: Node, parameters: readonly string[], found: string[]): void {
    switch (node.kind) {
        case 'name':
            if (node.binding === undefined && firstWide(node.name) < 0 && !parameters.some(p => equalsIgnoreCase(p, node.name))) found.push(node.name);
            return;
        case 'let':
            for (const binding of node.bindings) walk(binding.value, parameters, found);
            walk(node.calculation, parameters, found);
            return;
        case 'function': {
            const args = node.arguments;
            const last = args.at(-1);
            if (equalsIgnoreCase(node.name, 'LAMBDA') && last !== undefined) {
                const inner = [...parameters];
                for (let i = 0; i < args.length - 1; i++) {
                    const argument = args[i] as Node; // oxlint-disable-line typescript/no-unsafe-type-assertion -- i is within the arguments
                    if (argument.kind === 'name') inner.push(argument.name);
                    else walk(argument, parameters, found);
                }
                walk(last, inner, found);
                return;
            }
            for (const argument of args) walk(argument, parameters, found);
            return;
        }
        case 'intersection':
            walk(node.left, parameters, found);
            walk(node.right, parameters, found);
            return;
        case 'binary':
            // Along the chain of left operands in a loop: a chain of thousands of operators does not deepen the stack.
            for (const operand of node.operands()) walk(operand, parameters, found);
            return;
        case 'parentheses':
            walk(node.inner, parameters, found);
            return;
        case 'unary':
        case 'percent':
            walk(node.operand, parameters, found);
            return;
        case 'number':
        case 'text':
        case 'boolean':
        case 'error':
        case 'missing':
        case 'reference':
        case 'structuredReference':
            return;
        default:
            throw new Error(`Internal: ${String(node)} is no kind of node.`);
    }
}
