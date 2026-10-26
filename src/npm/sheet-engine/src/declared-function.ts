// The declared set of functions as the typing aids see it (C#'s DeclaredFunction.cs; ADR-0047, ADR-0051, ADR-0058).
import type { ArgumentValue } from './formulas/function-definition';
import { allFunctions, findFunction } from './formulas/function-library';

/**
 * A function of the declared set (ADR-0047): each gives Excel's result, and a name outside the set is `#NAME?`.
 * What completion offers and what the argument hint shows (ADR-0051).
 */
export class DeclaredFunction {
    /** The name in upper case, such as `SUM`. */
    readonly name: string;

    /** The argument list as Excel documents it, optional ones in brackets: `number1, [number2], ...`. */
    readonly arguments: string;

    /** One sentence saying what it returns. */
    readonly description: string;

    static #all: readonly DeclaredFunction[] | undefined;

    private constructor(name: string, args: string, description: string) {
        this.name = name;
        this.arguments = args;
        this.description = description;
    }

    /** Every function the engine declares, in alphabetical order. */
    static get all(): readonly DeclaredFunction[] {
        DeclaredFunction.#all ??= allFunctions().map(f => new DeclaredFunction(f.name, f.arguments, f.description));
        return DeclaredFunction.#all;
    }

    /** The signature a hint shows: `SUM(number1, [number2], ...)`. */
    get signature(): string {
        return `${this.name}(${this.arguments})`;
    }

    /** The declared function of that name, in any case, or `undefined` when the name is not declared. */
    static find(name: string): DeclaredFunction | undefined {
        const found = findFunction(name);
        return found === undefined ? undefined : DeclaredFunction.all.find(f => f.name === found.name);
    }

    /**
     * The values argument `index` (from 0) takes when it takes one of a fixed list, in Excel's order and with
     * Excel's texts, such as `XLOOKUP`'s `match_mode`: what completion lists there (ADR-0058). Empty for an
     * argument that takes any value.
     */
    valuesOf(index: number): readonly ArgumentValue[] {
        const values = findFunction(this.name)?.values[index];
        // oxlint-disable-next-line ex-grid/no-stand-in-default -- C#'s ValuesOf: an argument without a fixed list lists none
        return values ?? [];
    }
}
