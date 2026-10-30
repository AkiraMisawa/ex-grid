// A part's XML written as .NET's XmlWriter writes it under the settings ExGrid.Data.Xlsx gives it (UTF-8, no byte
// order mark, no indentation), so a part written by either package is the same text (ADR-0370, section 2): the
// declaration with `standalone="yes"`, a default namespace declared after the element's other attributes, an empty
// element closed as ` />`, text with `&`, `<` and `>` escaped and its line ends written as line feeds, and an
// attribute with `"`, a tab and its line ends escaped as well. It writes what the writer's callers give it, which
// have checked that XML can carry it.

const declaration = '<?xml version="1.0" encoding="utf-8" standalone="yes"?>';

function escapeText(text: string): string {
    return text.replace(/[&<>]|\r\n?/g, c => {
        switch (c) {
            case '&': return '&amp;';
            case '<': return '&lt;';
            case '>': return '&gt;';
            default: return '\n';
        }
    });
}

function escapeAttribute(text: string): string {
    return text.replace(/[&<>"\t\n\r]/g, c => {
        switch (c) {
            case '&': return '&amp;';
            case '<': return '&lt;';
            case '>': return '&gt;';
            case '"': return '&quot;';
            case '\t': return '&#x9;';
            case '\n': return '&#xA;';
            default: return '&#xD;';
        }
    });
}

/** Writes a part's XML into text taken a piece at a time. */
export class XmlTextWriter {
    #text = '';
    readonly #names: string[] = [];
    /** Whether the last element's start tag is not closed yet: its attributes may still come. */
    #open = false;
    /** The default namespace the open start tag declares after its attributes, or `undefined`. */
    #namespace: string | undefined;

    /** The text written and not taken. */
    get length(): number {
        return this.#text.length;
    }

    /** Takes the text written so far. */
    take(): string {
        const text = this.#text;
        this.#text = '';
        return text;
    }

    /** `WriteStartDocument(standalone: true)`. */
    startDocument(): void {
        this.#text += declaration;
    }

    /** `WriteStartElement`: the element `name`, as written, declaring `namespace` as the default one when given. */
    start(name: string, namespace?: string): void {
        this.#close();
        this.#text += `<${name}`;
        this.#names.push(name);
        this.#open = true;
        this.#namespace = namespace;
    }

    /** `WriteAttributeString`: an attribute of the open start tag, its name as written (`r:id`, `xmlns:r`). */
    attribute(name: string, value: string): void {
        if (!this.#open) throw new Error(`Internal: the attribute ${name} is written outside a start tag.`);
        this.#text += ` ${name}="${escapeAttribute(value)}"`;
    }

    /** `WriteString`: text inside the element. Even empty, it closes the start tag, so the element ends with its own tag. */
    text(value: string): void {
        this.#close();
        this.#text += escapeText(value);
    }

    /** `WriteEndElement`. */
    end(): void {
        const name = this.#names.pop();
        if (name === undefined) throw new Error('Internal: an element is ended where none is open.');
        if (this.#open) {
            this.#declare();
            this.#text += ' />';
            this.#open = false;
        } else {
            this.#text += `</${name}>`;
        }
    }

    /** `WriteElementString`: an element holding `value`, or empty for none or ''. */
    element(name: string, value: string | null): void {
        this.start(name);
        if (value !== null && value !== '') this.text(value);
        this.end();
    }

    #declare(): void {
        if (this.#namespace !== undefined) this.#text += ` xmlns="${escapeAttribute(this.#namespace)}"`;
        this.#namespace = undefined;
    }

    #close(): void {
        if (!this.#open) return;
        this.#declare();
        this.#text += '>';
        this.#open = false;
    }
}
