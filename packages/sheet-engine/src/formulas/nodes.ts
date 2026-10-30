// The nodes of a parsed Formula. Its text is the Formula's, kept by the Entry (ADR-0047).
import { columnCount, rowCount } from '../cell-address';
import type { ErrorValue } from '../value';
import type { FunctionDefinition } from './function-definition';
import { Reference } from './reference';

/** A node of a parsed Formula. */
export type Node =
    | NumberNode | TextNode | BooleanNode | ErrorNode | MissingNode | ReferenceNode | StructuredReferenceNode | NameNode
    | LetNode | IntersectionNode | FunctionNode | ParenthesesNode | UnaryNode | PercentNode | BinaryNode;

/** What every node can say of itself, statically. */
abstract class NodeBase {
    /** Every Reference this node reads, statically: the dependency graph's edges. */
    *references(): Generator<Reference> {
        // A leaf reads no Reference.
    }

    /** Every structured reference this node reads, statically: a Linked Table's readers (ADR-0049). */
    *structuredReferences(): Generator<StructuredReferenceNode> {
        // A leaf reads no Linked Table.
    }

    /** Every function call this node makes, statically, its arguments' calls included: `TODAY`'s readers (ADR-0121). */
    *calls(): Generator<FunctionNode> {
        // A leaf makes no call.
    }

    /**
     * How deep a walk of this node goes, counted in nodes, where a walk that reads an operator's chain loops
     * along it rather than descending: a leaf is 1, a node one more than its deepest child, except a
     * `BinaryNode`'s left operand, which stands at the operator's own depth; and a name a `LET` binds one more
     * than its value. The parser refuses a Formula deeper than its `mostNesting`.
     */
    get depth(): number {
        return 1;
    }
}

export class NumberNode extends NodeBase {
    readonly kind = 'number';
    readonly number: number;

    constructor(number: number) {
        super();
        this.number = number;
    }
}

export class TextNode extends NodeBase {
    readonly kind = 'text';
    readonly text: string;

    constructor(text: string) {
        super();
        this.text = text;
    }
}

export class BooleanNode extends NodeBase {
    readonly kind = 'boolean';
    readonly value: boolean;

    constructor(value: boolean) {
        super();
        this.value = value;
    }
}

export class ErrorNode extends NodeBase {
    readonly kind = 'error';
    readonly error: ErrorValue;

    constructor(error: ErrorValue) {
        super();
        this.error = error;
    }
}

/** An argument left empty, as in `IF(A1,,1)`. */
export class MissingNode extends NodeBase {
    readonly kind = 'missing';

    static readonly instance = new MissingNode();

    private constructor() {
        super();
    }

    /**
     * Whether an argument is left empty: written empty, or a `LET` name bound to a value left empty, as in
     * `LET(x,,IF(A1,x,1))`. Such a name reads as the empty argument it is bound to would read in its place
     * (ADR-0181; LET-107, run 17).
     */
    static isEmpty(argument: Node): boolean {
        if (argument.kind === 'missing') return true;
        if (argument.kind === 'name' && argument.binding !== undefined) return MissingNode.isEmpty(argument.binding.value);
        return false;
    }
}

export class ReferenceNode extends NodeBase {
    readonly kind = 'reference';
    readonly reference: Reference;

    constructor(reference: Reference) {
        super();
        this.reference = reference;
    }

    override *references(): Generator<Reference> {
        yield this.reference;
    }
}

/** A structured reference into a Linked Table, `Table[Column]` (ADR-0049). */
export class StructuredReferenceNode extends NodeBase {
    readonly kind = 'structuredReference';
    readonly table: string;
    readonly column: string;

    constructor(table: string, column: string) {
        super();
        this.table = table;
        this.column = column;
    }

    override *structuredReferences(): Generator<StructuredReferenceNode> {
        yield this;
    }
}

/** One name a `LET` binds, and the value it is bound to (ADR-0181). */
export class LetBinding {
    /** The name as typed; names compare without regard to case. */
    readonly name: string;
    /** The value's syntax, computed when the name is first read, and at most once (ADR-0181); set by the parser once read. */
    value: Node = MissingNode.instance;

    constructor(name: string) {
        this.name = name;
    }
}

/**
 * A name that is not a function call, a Reference or a boolean. ExSheet defines no names of its own, so it is
 * `#NAME?` — unless a `LET` around it binds it (ADR-0181).
 */
export class NameNode extends NodeBase {
    readonly kind = 'name';
    readonly name: string;
    /** Where the name stands in the Formula's text. */
    readonly position: number;
    /** The `LET` binding the name reads, as the text's scopes resolve it; `undefined` for none (`#NAME?`). */
    readonly binding: LetBinding | undefined;
    readonly #depth: number;

    constructor(name: string, position: number, binding: LetBinding | undefined) {
        super();
        this.name = name;
        this.position = position;
        this.binding = binding;
        // Reading a bound name computes its value there, so it is one deeper than the value.
        this.#depth = binding === undefined ? 1 : binding.value.depth + 1;
    }

    override get depth(): number {
        return this.#depth;
    }
}

/**
 * `LET(name1, value1, …, calculation)` (ADR-0181): names bound inside one Formula, each seen by the values
 * after it and by the calculation. Its References, Linked Tables and calls are every one its text names, the
 * bindings' whether they are read or not.
 */
export class LetNode extends NodeBase {
    readonly kind = 'let';
    readonly bindings: readonly LetBinding[];
    readonly calculation: Node;
    readonly #depth: number;

    constructor(bindings: readonly LetBinding[], calculation: Node) {
        super();
        this.bindings = bindings;
        this.calculation = calculation;
        this.#depth = Math.max(Math.max(0, ...bindings.map(b => b.value.depth)), calculation.depth) + 1;
    }

    get #parts(): Node[] {
        return [...this.bindings.map(b => b.value), this.calculation];
    }

    override *references(): Generator<Reference> {
        for (const p of this.#parts) yield* p.references();
    }

    override *structuredReferences(): Generator<StructuredReferenceNode> {
        for (const p of this.#parts) yield* p.structuredReferences();
    }

    override *calls(): Generator<FunctionNode> {
        for (const p of this.#parts) yield* p.calls();
    }

    override get depth(): number {
        return this.#depth;
    }
}

/**
 * The intersection of two operands written with whitespace between them, one of them a name (`item one`):
 * ExSheet defines no names, so it is `#NAME?` (ADR-0047). The parser refuses an intersection of two References.
 */
export class IntersectionNode extends NodeBase {
    readonly kind = 'intersection';
    readonly left: Node;
    readonly right: Node;
    readonly #depth: number;

    constructor(left: Node, right: Node) {
        super();
        this.left = left;
        this.right = right;
        this.#depth = Math.max(left.depth, right.depth) + 1;
    }

    override *references(): Generator<Reference> {
        yield* this.left.references();
        yield* this.right.references();
    }

    override *calls(): Generator<FunctionNode> {
        yield* this.left.calls();
        yield* this.right.calls();
    }

    override get depth(): number {
        return this.#depth;
    }
}

export class FunctionNode extends NodeBase {
    readonly kind = 'function';
    /** The name: upper case for a declared function, as typed otherwise. */
    readonly name: string;
    readonly arguments: readonly Node[];
    /** The declared function, or `undefined` for a name outside the set (`#NAME?`). */
    readonly function: FunctionDefinition | undefined;
    /**
     * For `RAND` and `RANDBETWEEN`: which of the Formula's draws this call is, counted in the Formula's text
     * from 0 (ADR-0180). Two calls are two draws; one call read twice, as a `LET` binding is, is one.
     */
    readonly place: number;
    readonly #depth: number;

    constructor(name: string, args: readonly Node[], definition: FunctionDefinition | undefined, place = 0) {
        super();
        this.name = name;
        this.arguments = args;
        this.function = definition;
        this.place = place;
        this.#depth = Math.max(0, ...args.map(a => a.depth)) + 1;
    }

    override *references(): Generator<Reference> {
        for (const a of this.arguments) yield* a.references();
        yield* criteriaResultReferences(this);
    }

    override *structuredReferences(): Generator<StructuredReferenceNode> {
        for (const a of this.arguments) yield* a.structuredReferences();
    }

    override *calls(): Generator<FunctionNode> {
        yield this;
        for (const a of this.arguments) yield* a.calls();
    }

    override get depth(): number {
        return this.#depth;
    }
}

export class ParenthesesNode extends NodeBase {
    readonly kind = 'parentheses';
    readonly inner: Node;
    readonly #depth: number;

    constructor(inner: Node) {
        super();
        this.inner = inner;
        this.#depth = inner.depth + 1;
    }

    override *references(): Generator<Reference> {
        yield* this.inner.references();
    }

    override *structuredReferences(): Generator<StructuredReferenceNode> {
        yield* this.inner.structuredReferences();
    }

    override *calls(): Generator<FunctionNode> {
        yield* this.inner.calls();
    }

    override get depth(): number {
        return this.#depth;
    }
}

export class UnaryNode extends NodeBase {
    readonly kind = 'unary';
    readonly operator: '+' | '-';
    readonly operand: Node;
    readonly #depth: number;

    constructor(operator: '+' | '-', operand: Node) {
        super();
        this.operator = operator;
        this.operand = operand;
        this.#depth = operand.depth + 1;
    }

    override *references(): Generator<Reference> {
        yield* this.operand.references();
    }

    override *structuredReferences(): Generator<StructuredReferenceNode> {
        yield* this.operand.structuredReferences();
    }

    override *calls(): Generator<FunctionNode> {
        yield* this.operand.calls();
    }

    override get depth(): number {
        return this.#depth;
    }
}

export class PercentNode extends NodeBase {
    readonly kind = 'percent';
    readonly operand: Node;
    readonly #depth: number;

    constructor(operand: Node) {
        super();
        this.operand = operand;
        this.#depth = operand.depth + 1;
    }

    override *references(): Generator<Reference> {
        yield* this.operand.references();
    }

    override *structuredReferences(): Generator<StructuredReferenceNode> {
        yield* this.operand.structuredReferences();
    }

    override *calls(): Generator<FunctionNode> {
        yield* this.operand.calls();
    }

    override get depth(): number {
        return this.#depth;
    }
}

/**
 * A binary operator. Excel's operators associate to the left, so `=1+1+…+1` is a chain of these down their
 * left operands, as long as the Formula: a walk over one loops along that chain (`spine`, `operands`) and
 * descends only into the right operands, so a chain of thousands of operators never deepens the stack.
 */
export class BinaryNode extends NodeBase {
    readonly kind = 'binary';
    readonly operator: string;
    readonly left: Node;
    readonly right: Node;
    readonly #depth: number;

    constructor(operator: string, left: Node, right: Node) {
        super();
        this.operator = operator;
        this.left = left;
        this.right = right;
        // The left operand stands at this operator's depth: a walk loops along it.
        this.#depth = Math.max(left.depth, right.depth + 1);
    }

    override get depth(): number {
        return this.#depth;
    }

    override *references(): Generator<Reference> {
        for (const o of this.operands()) yield* o.references();
    }

    override *structuredReferences(): Generator<StructuredReferenceNode> {
        for (const o of this.operands()) yield* o.structuredReferences();
    }

    override *calls(): Generator<FunctionNode> {
        for (const o of this.operands()) yield* o.calls();
    }

    /**
     * This operator and those down its left operands, innermost (the first applied) first: the chain `1+2-3`
     * is the `+`, then the `-`. The innermost's left operand is `leftmost`.
     */
    get spine(): BinaryNode[] {
        const spine: BinaryNode[] = [this];
        for (let node = this.left; node.kind === 'binary'; node = node.left) spine.push(node);
        return spine.toReversed();
    }

    /** The first operand of the chain: the left operand of its innermost operator, which is no operator. */
    get leftmost(): Node {
        let node = this.left;
        while (node.kind === 'binary') node = node.left;
        return node;
    }

    /** The chain's operands in the text's order: `leftmost`, then each operator's right operand. */
    *operands(): Generator<Node> {
        const spine = this.spine;
        yield this.leftmost;
        for (const binary of spine) yield binary.right;
    }
}

/**
 * The Reference `SUMIF` and `AVERAGEIF` read beyond their written result range: the range the size of the
 * criteria range from the result range's top-left cell, as Excel reads it, so the dependency graph follows it.
 */
function* criteriaResultReferences(call: FunctionNode): Generator<Reference> {
    if (call.function === undefined || (call.name !== 'SUMIF' && call.name !== 'AVERAGEIF') || call.arguments.length !== 3) return;
    const criteria = literalReference(call.arguments[0]);
    const result = literalReference(call.arguments[2]);
    if (criteria === undefined || criteria.spilled || result === undefined || result.spilled) return;
    const a = criteria.area;
    const b = result.area;
    if (b.row1 + a.rows > rowCount || b.column1 + a.columns > columnCount) return;
    yield Reference.rectangle(result.sheetName, b.row1, b.column1, b.row1 + a.rows - 1, b.column1 + a.columns - 1, true, true, true, true)
        .with({ lastSheetName: result.lastSheetName });
}

/** The Reference a node writes literally, through parentheses; `undefined` for any other node. */
export function literalReference(node: Node | undefined): Reference | undefined {
    if (node === undefined) return undefined;
    if (node.kind === 'reference') return node.reference;
    if (node.kind === 'parentheses') return literalReference(node.inner);
    return undefined;
}
