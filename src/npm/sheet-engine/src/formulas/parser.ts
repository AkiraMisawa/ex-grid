// Reads a Formula in Excel's invariant syntax (ADR-0047) into nodes, with Excel's operator precedence, from the
// loosest binding: comparison (= <> < > <= >=), &, + -, * /, ^, %, then negation — so -2^2 is 4 and 2^3^2 is 64,
// as in Excel. Anything it cannot read is refused with a FormulaSyntaxError, as Excel refuses such a Formula;
// that includes the union (,) operator, array constants, a range operator between anything but two addresses,
// and the intersection (space) operator between two References; and, as Excel refuses it, a call that gives an
// argument Excel takes only as a Reference something that can never be one. An intersection with a name in it is
// read (ADR-0047, second run): ExSheet defines no names, so it is #NAME? whatever the other side is.
//
// LET's names (ADR-0181), as Excel's keyboard accepts them: a name is seen by the values bound after it and by
// its own LET's calculation, and an inner LET may bind it again; the parser resolves each name to its binding
// where it stands, so a name read before its binding, or inside its own value, resolves to none and is #NAME?.
import { equalsIgnoreCase, ignoreCaseKey, isAsciiDigit, isLetter, toUpperInvariant } from '../dotnet';
import { FormulaSyntaxError } from '../formula-syntax-error';
import { elementAt } from '../must';
import type { FunctionDefinition } from './function-definition';
import { findFunction } from './function-library';
import { firstWide, tokenize, wideNameOutsideLet, type Token } from './lexer';
import {
    BinaryNode, BooleanNode, ErrorNode, FunctionNode, IntersectionNode, LetBinding, LetNode, MissingNode, NameNode, NumberNode,
    ParenthesesNode, PercentNode, ReferenceNode, StructuredReferenceNode, TextNode, UnaryNode, type Node,
} from './nodes';

/**
 * The most levels of nested functions a Formula holds, a `LET` counted as one: 64, Excel's own limit, past which
 * Excel refuses the Formula on entry.
 */
export const mostNestedFunctions = 64;

/**
 * The deepest Formula the engine reads (`Node.depth`): parentheses, signs, `%`, functions, the right operands of
 * operators and the `LET` names read, each counted as a level, while a chain of operators down their left
 * operands is not. Deeper, a walk of the Formula could exhaust the stack, so the Formula is refused on entry.
 */
export const mostNesting = 256;

/** The longest name Excel's keyboard took: 249 characters (LET-ADDITIONAL-002; 250 refused, LET-KEYS-013). */
const longestLetName = 249;

/** The most names one `LET` binds: 126 (LET-029; a 127th is refused, LET-030). */
const mostLetNames = 126;

/** One `LET` being read: the names bound so far, in scope, and every name it binds, later ones included. */
interface LetScope {
    readonly bound: LetBinding[];
    /** Every name it binds, as `ignoreCaseKey` keys them. */
    readonly names: Set<string>;
}

/** Parses a Formula; `formula` starts with `=`. */
export function parseFormula(formula: string): Node {
    return parseFormulaWithTokens(formula).node;
}

/** Parses a Formula, and gives the tokens it was read from. */
export function parseFormulaWithTokens(formula: string): { node: Node; tokens: readonly Token[] } {
    if (formula.length === 0 || formula.charAt(0) !== '=') throw new FormulaSyntaxError(formula, 0, "a Formula begins with '='.");
    const parser = new Parser(formula, tokenize(formula, 1));
    return { node: parser.parse(), tokens: parser.tokens };
}

class Parser {
    readonly #formula: string;
    readonly tokens: readonly Token[];
    #at = 0;
    /** The `LET`s being read, the innermost last: the scopes a name is resolved in (ADR-0181). */
    readonly #lets: LetScope[] = [];
    /**
     * The names of another script than ASCII that no scope bound where they stand, with the `LET`s around them.
     * Each must be a name one of those `LET`s binds, later in its text, or the Formula is refused (ADR-0181).
     */
    #wideNames: { readonly name: NameNode; readonly around: readonly LetScope[] }[] = [];
    /** How many parentheses, signs and calls the parser is inside: its own recursion, kept within `mostNesting`. */
    #nesting = 0;
    /** How many calls the parser is inside, kept within `mostNestedFunctions`. */
    #functions = 0;

    constructor(formula: string, tokens: readonly Token[]) {
        this.#formula = formula;
        this.tokens = tokens;
    }

    parse(): Node {
        if (this.#peek().kind === 'end') throw new FormulaSyntaxError(this.#formula, 1, 'the Formula is empty.');
        // Outside a LET, a name of another script is refused where it stands, before anything after it (ADR-0181).
        const wide = wideNameOutsideLet(this.tokens);
        if (wide !== undefined) throw this.#notSyntax(wide.position + firstWide(wide.text));
        const node = this.#parseComparison();
        if (this.#peek().kind !== 'end') throw this.#unexpected();
        this.#bounded(node, this.#peek());
        this.#refuseUnboundWideNames();
        return node;
    }

    #peek(): Token {
        return elementAt(this.tokens, this.#at);
    }

    #next(): Token {
        return elementAt(this.tokens, this.#at++);
    }

    #peekOperator(...ops: string[]): boolean {
        return this.#peek().kind === 'operator' && ops.includes(this.#peek().text);
    }

    /**
     * For `RAND` and `RANDBETWEEN`: how many calls of either stand before this one in the Formula's text — its
     * place among the Formula's draws (ADR-0180). 0 for any other function.
     */
    #drawPlace(name: Token): number {
        if (!isDraw(name.text)) return 0;
        let place = 0;
        for (let i = 0; elementAt(this.tokens, i).position < name.position; i++) {
            const t = elementAt(this.tokens, i);
            const after = elementAt(this.tokens, i + 1);
            if (t.kind === 'name' && isDraw(t.text) && after.kind === 'leftParenthesis' && !after.afterSpace) place++;
        }
        return place;
    }

    #unexpected(): FormulaSyntaxError {
        const peek = this.#peek();
        if (peek.kind === 'end') return new FormulaSyntaxError(this.#formula, peek.position, 'the Formula ends too early.');
        // A name of another script that no LET around it binds is refused as the lexer refused one (ADR-0181).
        if (peek.kind === 'name') {
            const wide = firstWide(peek.text);
            if (wide >= 0 && this.#resolve(peek.text) === undefined) return this.#notSyntax(peek.position + wide);
        }
        return new FormulaSyntaxError(this.#formula, peek.position, `'${peek.text}' is not expected here.`);
    }

    /**
     * An expression: operands joined by binary operators, read with a stack of operators rather than a method for
     * each level of precedence. An operator applies every operator before it that binds at least as tightly,
     * which makes each associate to the left, as in Excel.
     */
    #parseComparison(): Node {
        const operands: Node[] = [this.#parseUnary()];
        const operators: Token[] = [];
        const apply = (): void => {
            const op = pop(operators);
            const right = pop(operands);
            const left = pop(operands);
            operands.push(this.#bounded(new BinaryNode(op.text, left, right), op));
        };
        for (;;) {
            if (this.#peek().kind !== 'operator') break;
            const level = precedence(this.#peek().text);
            if (level <= 0) break;
            while (operators.length > 0 && precedence(elementAt(operators, operators.length - 1).text) >= level) apply();
            operators.push(this.#next());
            operands.push(this.#parseUnary());
        }
        while (operators.length > 0) apply();
        return elementAt(operands, 0);
    }

    /**
     * An operand: signs, then an intersection or a primary, then any `%`. Negation binds tighter than `^` in
     * Excel, and `%` tighter than negation. A run of signs is read in a loop, not by recursion.
     */
    #parseUnary(): Node {
        const signs: Token[] = [];
        while (this.#peekOperator('-', '+')) {
            const sign = this.#next();
            signs.push(sign);
            this.#enter(sign);
        }
        let node = this.#parseIntersection();
        while (this.#peekOperator('%')) node = this.#bounded(new PercentNode(node), this.#next());
        for (let i = signs.length - 1; i >= 0; i--) {
            const sign = elementAt(signs, i);
            node = this.#bounded(new UnaryNode(sign.text === '-' ? '-' : '+', node), sign);
            this.#nesting--;
        }
        return node;
    }

    /**
     * The intersection operator, whitespace between two References or names, which binds tighter than `%` and
     * negation in Excel. It is read only when a name stands on one side of it; between two References it is
     * refused by name, as an operator the engine does not implement (ADR-0047, third run, TYPED-054).
     */
    #parseIntersection(): Node {
        let node = this.#parsePrimary();
        while ((node.kind === 'name' || node.kind === 'reference' || node.kind === 'intersection') && this.#peek().afterSpace
            && this.#isIntersectionOperand(this.#at)) {
            const at = this.#peek();
            const right = this.#parsePrimary();
            // A LET name stands for what it is bound to, which may be a range: Excel would intersect them (ADR-0181).
            if (rangeLike(node) && rangeLike(right)) {
                throw new FormulaSyntaxError(this.#formula, at.position, 'the intersection operator (a space between two References) is not implemented.',
                    { isUnimplemented: true });
            }
            node = this.#bounded(new IntersectionNode(node, right), at);
        }
        return node;
    }

    /** Whether the token at `index` is a Reference or a name that is not a function call nor a boolean. */
    #isIntersectionOperand(index: number): boolean {
        const t = elementAt(this.tokens, index);
        if (t.kind === 'reference') return true;
        if (t.kind !== 'name') return false;
        const after = elementAt(this.tokens, index + 1);
        if (after.kind === 'leftParenthesis' && !after.afterSpace) return false;
        return !equalsIgnoreCase(t.text, 'TRUE') && !equalsIgnoreCase(t.text, 'FALSE');
    }

    #parsePrimary(): Node {
        const t = this.#peek();
        switch (t.kind) {
            case 'number':
                this.#next();
                return new NumberNode(t.number);
            case 'text':
                this.#next();
                return new TextNode(t.text);
            case 'error':
                this.#next();
                if (t.error === undefined) throw new Error('Internal: an error token holds its Error Value.');
                return new ErrorNode(t.error);
            case 'reference':
                this.#next();
                if (t.reference === undefined) throw new Error('Internal: a reference token holds its Reference.');
                return new ReferenceNode(t.reference);
            case 'structuredReference':
                this.#next();
                if (t.tableName === undefined) throw new Error('Internal: a structured reference token holds its table.');
                return new StructuredReferenceNode(t.tableName, t.text);
            case 'leftParenthesis': {
                this.#enter(this.#next());
                const inner = this.#parseComparison();
                if (this.#peek().kind !== 'rightParenthesis') throw this.#unexpected();
                this.#nesting--;
                return this.#bounded(new ParenthesesNode(inner), this.#next());
            }
            case 'name': {
                this.#next();
                if (this.#peek().kind === 'leftParenthesis' && !this.#peek().afterSpace) return this.#parseCall(t);
                if (equalsIgnoreCase(t.text, 'TRUE')) return new BooleanNode(true);
                if (equalsIgnoreCase(t.text, 'FALSE')) return new BooleanNode(false);
                const name = this.#bounded(new NameNode(t.text, t.position, this.#resolve(t.text)), t);
                if (name.binding === undefined && firstWide(t.text) >= 0) this.#wideNames.push({ name, around: this.#lets.toReversed() });
                return name;
            }
            case 'operator':
            case 'rightParenthesis':
            case 'comma':
            case 'end':
                throw this.#unexpected();
            default:
                throw new Error(`Internal: ${String(t.kind satisfies never)} is no kind of token.`);
        }
    }

    #parseCall(name: Token): Node {
        // A function's name is ASCII: one of another script is refused as it was before LET (ADR-0181).
        const wide = firstWide(name.text);
        if (wide >= 0) throw this.#notSyntax(name.position + wide);
        const definition = findFunction(name.text);
        this.#enterFunction(name);
        const call = definition?.name === 'LET' ? this.#parseLet(name) : this.#parseArguments(name, definition);
        this.#functions--;
        this.#nesting--;
        return this.#bounded(call, name);
    }

    /** Goes one level deeper into parentheses, a sign or a call, refusing the Formula once that is deeper than `mostNesting`. */
    #enter(at: Token): void {
        if (++this.#nesting > mostNesting) throw this.#tooDeep(at.position);
    }

    /** Goes into a call's arguments, refusing a 65th level of nested functions as Excel does (`mostNestedFunctions`). */
    #enterFunction(name: Token): void {
        if (this.#functions + 1 > mostNestedFunctions) {
            throw new FormulaSyntaxError(this.#formula, name.position,
                `functions are nested more than ${String(mostNestedFunctions)} levels deep; Excel refuses such a Formula too.`, { isBeyondLimit: true });
        }
        this.#functions++;
        this.#enter(name);
    }

    /** The node, or a refusal when it is deeper than `mostNesting` (`Node.depth`). */
    #bounded<T extends Node>(node: T, at: Token): T {
        if (node.depth > mostNesting) throw this.#tooDeep(at.position);
        return node;
    }

    #tooDeep(position: number): FormulaSyntaxError {
        return new FormulaSyntaxError(this.#formula, position,
            `the Formula is nested more than ${String(mostNesting)} levels deep (parentheses, signs, functions, and operators within operators); ExSheet cannot compute one so deep.`,
            { isBeyondLimit: true });
    }

    #parseArguments(name: Token, definition: FunctionDefinition | undefined): FunctionNode {
        this.#next(); // (
        const args: Node[] = [];
        if (this.#peek().kind === 'rightParenthesis') {
            this.#next();
        } else {
            for (;;) {
                args.push(this.#peek().kind === 'comma' || this.#peek().kind === 'rightParenthesis' ? MissingNode.instance : this.#parseComparison());
                if (this.#peek().kind === 'comma') {
                    this.#next();
                    continue;
                }
                if (this.#peek().kind === 'rightParenthesis') {
                    this.#next();
                    break;
                }
                throw this.#unexpected();
            }
        }

        if (definition === undefined) return new FunctionNode(name.text, args, undefined);
        // Excel refuses a call with too few or too many arguments when the Formula is entered.
        if (args.length < definition.minimumArguments) {
            throw new FormulaSyntaxError(this.#formula, name.position, `${definition.name} takes at least ${String(definition.minimumArguments)} argument(s).`);
        }
        if (args.length > definition.maximumArguments) {
            throw new FormulaSyntaxError(this.#formula, name.position, `${definition.name} takes at most ${String(definition.maximumArguments)} argument(s).`);
        }
        if (definition.inPairs && (args.length - definition.pairOffset) % 2 !== 0) {
            throw new FormulaSyntaxError(this.#formula, name.position, `${definition.name} takes its arguments in pairs.`);
        }
        const only = definition.referenceOnly;
        if (only !== undefined) {
            for (let i = only.first; i <= only.last && i < args.length; i++) {
                if (neverAReference(elementAt(args, i))) {
                    throw new FormulaSyntaxError(this.#formula, name.position, `${definition.name} takes only a Reference as its argument ${String(i + 1)}.`);
                }
            }
        }
        return new FunctionNode(definition.name, args, definition, this.#drawPlace(name));
    }

    /** The binding a name reads where it stands: the innermost `LET` that has bound it so far; `undefined` for none. */
    #resolve(name: string): LetBinding | undefined {
        for (let s = this.#lets.length - 1; s >= 0; s--) {
            const binding = elementAt(this.#lets, s).bound.find(b => equalsIgnoreCase(b.name, name));
            if (binding !== undefined) return binding;
        }
        return undefined;
    }

    /**
     * `LET(name1, value1, …, calculation)`: each value read with the names bound before it in scope, the
     * calculation with all of them. A value left empty is bound as the empty argument it is (LET-107, run 17).
     * Refused on entry, as Excel refuses each: too few arguments, an even number of them, a missing calculation,
     * a name bound twice, a 127th name, and a name `letNameRefusal` refuses.
     */
    #parseLet(let_: Token): LetNode {
        this.#next(); // (
        const scope: LetScope = { bound: [], names: new Set() };
        this.#lets.push(scope);
        const args: Node[] = [];
        let named: LetBinding | undefined;
        if (this.#peek().kind === 'rightParenthesis') throw new FormulaSyntaxError(this.#formula, let_.position, 'LET takes at least 3 argument(s).');
        for (;;) {
            const start = this.#peek();
            const argument = this.#peek().kind === 'comma' || this.#peek().kind === 'rightParenthesis' ? MissingNode.instance : this.#parseComparison();
            const index = args.length;
            args.push(argument);
            if (this.#peek().kind === 'rightParenthesis') {
                this.#next();
                break;
            }
            if (this.#peek().kind !== 'comma') throw this.#unexpected();
            this.#next();
            if (index % 2 === 0) {
                // A name, with its value to follow.
                if (scope.names.size === mostLetNames) {
                    throw new FormulaSyntaxError(this.#formula, start.position, `a LET binds at most ${String(mostLetNames)} names.`);
                }
                const name = this.#letName(argument, start);
                const key = ignoreCaseKey(name);
                if (scope.names.has(key)) throw new FormulaSyntaxError(this.#formula, start.position, `'${name}' is bound twice in one LET.`);
                scope.names.add(key);
                named = new LetBinding(name);
            } else {
                if (named === undefined) throw new Error('Internal: a LET value follows its name.');
                named.value = argument;
                scope.bound.push(named);
                named = undefined;
            }
        }
        this.#lets.pop();
        if (args.length < 3) throw new FormulaSyntaxError(this.#formula, let_.position, 'LET takes at least 3 argument(s).');
        if (args.length % 2 === 0) throw new FormulaSyntaxError(this.#formula, let_.position, 'LET takes names and their values in pairs, then one calculation.');
        const calculation = elementAt(args, args.length - 1);
        if (calculation.kind === 'missing') throw new FormulaSyntaxError(this.#formula, let_.position, "LET's calculation is missing.");
        return new LetNode(scope.bound, calculation);
    }

    /** The argument as a `LET` name, or a refusal saying why it cannot be one. */
    #letName(argument: Node, start: Token): string {
        if (argument.kind !== 'name') {
            const refusal = argument.kind === 'reference' ? `'${start.text}' reads as a Reference, and cannot be a LET name.`
                : argument.kind === 'boolean' ? `${toUpperInvariant(start.text)} cannot be a LET name.`
                    : 'a LET name is expected here.';
            throw new FormulaSyntaxError(this.#formula, start.position, refusal);
        }
        const refusal = letNameRefusal(argument.name);
        if (refusal !== undefined) throw new FormulaSyntaxError(this.#formula, start.position, refusal);
        // Its own name is no unbound use of one.
        this.#wideNames = this.#wideNames.filter(w => w.name !== argument);
        return argument.name;
    }

    /**
     * Refuses a name of another script than ASCII that no `LET` around it binds, at its first such character, as
     * the lexer refused one before `LET` was admitted. One that a `LET` around it binds later in its text is read
     * before its binding, and is `#NAME?`, as an ASCII one is (ADR-0181).
     */
    #refuseUnboundWideNames(): void {
        for (const { name, around } of this.#wideNames) {
            if (around.some(scope => scope.names.has(ignoreCaseKey(name.name)))) continue;
            throw this.#notSyntax(name.position + firstWide(name.name));
        }
    }

    #notSyntax(at: number): FormulaSyntaxError {
        return new FormulaSyntaxError(this.#formula, at, `'${this.#formula.charAt(at)}' is not part of the formula syntax ExSheet reads.`);
    }
}

function isDraw(text: string): boolean {
    return equalsIgnoreCase(text, 'RAND') || equalsIgnoreCase(text, 'RANDBETWEEN');
}

/** Whether a node stands for a range on one side of an intersection: a Reference, or a name a `LET` binds. */
function rangeLike(node: Node): boolean {
    return node.kind === 'reference' || (node.kind === 'name' && node.binding !== undefined);
}

/**
 * How tightly a binary operator binds, from the loosest: comparison, `&`, `+ -`, `* /`, `^`; 0 for a token that
 * is no binary operator (`%`). Every one associates to the left, `^` too, as in Excel.
 */
function precedence(op: string): number {
    switch (op) {
        case '=': case '<>': case '<': case '>': case '<=': case '>=': return 1;
        case '&': return 2;
        case '+': case '-': return 3;
        case '*': case '/': return 4;
        case '^': return 5;
        default: return 0;
    }
}

/**
 * Whether an argument can never be a Reference, as Excel tells one when a Formula is entered: a number, a text
 * or a boolean, an operator's result (but parentheses around a Reference), or a call of a declared function that
 * never gives one, such as `SEQUENCE`.
 */
function neverAReference(argument: Node): boolean {
    switch (argument.kind) {
        case 'number':
        case 'text':
        case 'boolean':
        case 'binary':
        case 'unary':
        case 'percent':
            return true;
        case 'parentheses':
            return neverAReference(argument.inner);
        case 'function':
            return argument.function !== undefined && !argument.function.returnsReference;
        case 'error':
        case 'intersection':
        case 'let':
        case 'missing':
        case 'name':
        case 'reference':
        case 'structuredReference':
            return false;
        default:
            throw new Error(`Internal: ${String(argument)} is no kind of node.`);
    }
}

/**
 * Why a name cannot be a `LET` name, or `undefined` when it can (ADR-0181): it starts with a letter of any script
 * or `_`, goes on with letters, ASCII digits and `_`, and is at most 249 characters.
 */
function letNameRefusal(name: string): string | undefined {
    if (name.length > longestLetName) return `a LET name is at most ${String(longestLetName)} characters.`;
    const first = name.charCodeAt(0);
    let valid = isLetter(first) || first === 0x5f;
    for (let i = 1; i < name.length && valid; i++) {
        const c = name.charCodeAt(i);
        valid = isLetter(c) || isAsciiDigit(c) || c === 0x5f;
    }
    return valid ? undefined : `'${name}' cannot be a LET name: a name begins with a letter or _, and holds only letters, digits and _.`;
}

function pop<T>(stack: T[]): T {
    const top = stack.pop();
    if (top === undefined) throw new Error('Internal: the parser popped an empty stack.');
    return top;
}
