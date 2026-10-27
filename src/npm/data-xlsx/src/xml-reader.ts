// A pull reader of a part's XML, read as .NET's XmlReader reads it under the settings ExGrid.Data.Xlsx gives it
// (ADR-0330 §9): DTDs prohibited, comments and processing instructions ignored, whitespace kept, characters
// checked. It is not a general XML reader: it reads what a well-formed part holds, refuses what is not
// well-formed, and offers the members the package's readers use, with XmlReader's meaning — the node's depth,
// whether an element is empty, an element's text read whole, an element skipped.
//
// Text arrives in pieces, as a worksheet inflates. The reader holds what has arrived and has not been read,
// and asks for more by throwing `NeedMore` when a node runs past it; a caller that reads a worksheet a batch at
// a time marks where its batch began, gives the reader more and reads the batch again from the mark
// (`snapshot`, `restore`). A part read whole is given all of its text first, and never asks.

import { unreachable } from './must';

/** A part that is not well-formed XML: what XmlReader throws an XmlException for. */
export class XmlError extends Error {
    constructor(message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'XmlError';
    }
}

/** The reader ran past the text it holds: give it more and read again from the last mark. Never escapes the package. */
export class NeedMore extends Error {
    constructor() {
        super('Internal: the XML reader needs more text than it holds.');
        this.name = 'NeedMore';
    }
}

/** What the reader is on, as XmlReader's NodeType names it, of the kinds a part's reader meets. */
export type XmlNodeType = 'none' | 'element' | 'endElement' | 'text' | 'cdata' | 'whitespace' | 'significantWhitespace';

/** An attribute of the element the reader is on. */
export interface XmlAttribute {
    /** Its qualified name, as written: `r:id`. */
    readonly name: string;
    readonly prefix: string;
    readonly localName: string;
    /** Its namespace: none for an attribute written without a prefix; `http://www.w3.org/2000/xmlns/` for a declaration. */
    readonly namespaceUri: string;
    readonly value: string;
}

/** The text of a part, piece by piece; `undefined` once it has all come. */
export type TextSource = () => Promise<string | undefined>;

const xmlnsNamespace = 'http://www.w3.org/2000/xmlns/';
const xmlNamespace = 'http://www.w3.org/XML/1998/namespace';

interface Frame {
    readonly name: string;
    readonly localName: string;
    readonly prefix: string;
    readonly namespaceUri: string;
    /** The namespaces this element declares, by prefix ('' for the default one); null for none. */
    readonly declared: ReadonlyMap<string, string> | null;
    /** Whether `xml:space="preserve"` is in force inside it. */
    readonly preserve: boolean;
}

/** Where the reader stands, kept by `snapshot` and put back by `restore`. */
export interface XmlMark {
    readonly pos: number;
    readonly state: ReaderState;
}

interface ReaderState {
    readonly nodeType: XmlNodeType;
    readonly localName: string;
    readonly prefix: string;
    readonly namespaceUri: string;
    readonly value: string;
    readonly isEmptyElement: boolean;
    readonly depth: number;
    readonly attributes: readonly XmlAttribute[];
    readonly stack: readonly Frame[];
    readonly popOnNext: boolean;
    readonly started: boolean;
    readonly rootSeen: boolean;
    readonly rootClosed: boolean;
    readonly eof: boolean;
}

const invalidCharacter = /[\u0000-\u0008\u000B\u000C\u000E-\u001F\uFFFE\uFFFF]/; // oxlint-disable-line no-control-regex -- XML 1.0 refuses exactly these characters
// oxlint-disable-next-line no-misleading-character-class -- XML 1.0's NameStartChar ranges, read a UTF-16 unit at a time
const nameStart = /[A-Z_a-z:\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD\uD800-\uDBFF]/;
// oxlint-disable-next-line no-misleading-character-class -- XML 1.0's NameChar ranges, read a UTF-16 unit at a time
const nameRest = /^[A-Z_a-z:\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u02FF\u0370-\u037D\u037F-\u1FFF\u200C\u200D\u2070-\u218F\u2C00-\u2FEF\u3001-\uD7FF\uF900-\uFDCF\uFDF0-\uFFFD\uD800-\uDFFF.\-0-9\u00B7\u0300-\u036F\u203F-\u2040]*$/;

/** A qualified name's prefix and local name. */
function split(qualified: string): readonly [string, string] {
    const colon = qualified.indexOf(':');
    return colon < 0 ? ['', qualified] : [qualified.slice(0, colon), qualified.slice(colon + 1)];
}

function isSpace(c: number): boolean {
    return c === 0x20 || c === 0x09 || c === 0x0a || c === 0x0d;
}

/** XML's line ends made line feeds: `\r\n` and a lone `\r` are each `\n` (XML 1.0, section 2.11). */
function lineFeeds(text: string): string {
    return text.includes('\r') ? text.replace(/\r\n?/g, '\n') : text;
}

function checkCharacters(text: string): void {
    const bad = invalidCharacter.exec(text);
    if (bad !== null) {
        const code = bad[0].charCodeAt(0);
        throw new XmlError(`'${bad[0]}', hexadecimal value 0x${code.toString(16).toUpperCase().padStart(2, '0')}, is an invalid character.`);
    }
}

function checkName(name: string): void {
    if (name === '' || !nameStart.test(name.charAt(0)) || !nameRest.test(name.slice(1))) {
        throw new XmlError(`'${name}' is not a name XML allows.`);
    }
}

/** The text with its character and entity references replaced, as XML 1.0 section 4.6 predefines them. */
function unescape(text: string): string {
    if (!text.includes('&')) return text;
    let out = '';
    let at = 0;
    for (;;) {
        const amp = text.indexOf('&', at);
        if (amp < 0) return out + text.slice(at);
        const semi = text.indexOf(';', amp + 1);
        if (semi < 0) throw new XmlError('An entity reference is not ended by a semicolon.');
        out += text.slice(at, amp);
        const name = text.slice(amp + 1, semi);
        switch (name) {
            case 'lt': out += '<'; break;
            case 'gt': out += '>'; break;
            case 'amp': out += '&'; break;
            case 'quot': out += '"'; break;
            case 'apos': out += "'"; break;
            default: {
                const decimal = /^#([0-9]+)$/.exec(name);
                const hexadecimal = /^#x([0-9a-fA-F]+)$/.exec(name);
                const digits = decimal?.[1] ?? hexadecimal?.[1];
                if (digits === undefined) {
                    if (name.startsWith('#')) throw new XmlError(`'&${name};' is not a character reference.`);
                    throw new XmlError(`Reference to undeclared entity '${name}'.`);
                }
                const code = Number.parseInt(digits, decimal === null ? 16 : 10);
                const valid = code === 0x9 || code === 0xa || code === 0xd || (code >= 0x20 && code <= 0xd7ff)
                    || (code >= 0xe000 && code <= 0xfffd) || (code >= 0x10000 && code <= 0x10ffff);
                if (!valid) throw new XmlError(`The character reference '&${name};' is to a character XML does not allow.`);
                out += String.fromCodePoint(code);
            }
        }
        at = semi + 1;
    }
}

/** A pull reader of one part's XML, XmlReader's members by its meaning. */
export class XmlReader {
    readonly #source: TextSource;
    #text = '';
    #pos = 0;
    #ended = false;

    #nodeType: XmlNodeType = 'none';
    #localName = '';
    #prefix = '';
    #namespaceUri = '';
    #value = '';
    #isEmptyElement = false;
    #depth = 0;
    #attributes: readonly XmlAttribute[] = [];
    #stack: Frame[] = [];
    #popOnNext = false;
    #started = false;
    #rootSeen = false;
    #rootClosed = false;
    #eof = false;

    constructor(source: TextSource) {
        this.#source = source;
    }

    /** A reader of `text`, whole. */
    static of(text: string): XmlReader {
        let given = false;
        return new XmlReader(() => {
            if (given) return Promise.resolve(undefined);
            given = true;
            return Promise.resolve(text);
        });
    }

    nodeType(): XmlNodeType { return this.#nodeType; }

    localName(): string { return this.#localName; }

    prefix(): string { return this.#prefix; }

    namespaceUri(): string { return this.#namespaceUri; }

    /** The text of a text, CDATA or whitespace node; '' on an element. */
    value(): string { return this.#value; }

    isEmptyElement(): boolean { return this.#isEmptyElement; }

    /** How deep the node lies: the root element is 0, its children 1, and an element's text one below it. */
    depth(): number { return this.#depth; }

    attributes(): readonly XmlAttribute[] { return this.#attributes; }

    /** Whether the reader has read past the part's end. */
    eof(): boolean { return this.#eof; }

    /** Whether every piece of the text has arrived. */
    ended(): boolean { return this.#ended; }

    /** The characters that have arrived and are not read yet. */
    unread(): number { return this.#text.length - this.#pos; }

    /** Reads pieces until at least `ahead` characters lie unread, or the text has all come. Done only where no mark
     * is held: what has been read is let go. */
    async fill(ahead: number): Promise<void> {
        if (this.#pos > 0) {
            this.#text = this.#text.slice(this.#pos);
            this.#pos = 0;
        }
        while (!this.#ended && this.#text.length < ahead) {
            const piece = await this.#source();
            if (piece === undefined) this.#ended = true;
            else this.#text += piece;
        }
    }

    snapshot(): XmlMark {
        return {
            pos: this.#pos,
            state: {
                nodeType: this.#nodeType, localName: this.#localName, prefix: this.#prefix, namespaceUri: this.#namespaceUri,
                value: this.#value, isEmptyElement: this.#isEmptyElement, depth: this.#depth, attributes: this.#attributes,
                stack: [...this.#stack], popOnNext: this.#popOnNext, started: this.#started, rootSeen: this.#rootSeen,
                rootClosed: this.#rootClosed, eof: this.#eof,
            },
        };
    }

    restore(mark: XmlMark): void {
        const s = mark.state;
        this.#pos = mark.pos;
        this.#nodeType = s.nodeType;
        this.#localName = s.localName;
        this.#prefix = s.prefix;
        this.#namespaceUri = s.namespaceUri;
        this.#value = s.value;
        this.#isEmptyElement = s.isEmptyElement;
        this.#depth = s.depth;
        this.#attributes = s.attributes;
        this.#stack = [...s.stack];
        this.#popOnNext = s.popOnNext;
        this.#started = s.started;
        this.#rootSeen = s.rootSeen;
        this.#rootClosed = s.rootClosed;
        this.#eof = s.eof;
    }

    /** The attribute by its qualified name, as written (`GetAttribute(name)`), or `undefined`. */
    getAttribute(name: string): string | undefined {
        for (const attribute of this.#attributes) {
            if (attribute.name === name) return attribute.value;
        }
        return undefined;
    }

    /** The attribute by its local name and namespace (`GetAttribute(localName, namespaceURI)`), or `undefined`. */
    getAttributeNs(localName: string, namespaceUri: string): string | undefined {
        for (const attribute of this.#attributes) {
            if (attribute.localName === localName && attribute.namespaceUri === namespaceUri) return attribute.value;
        }
        return undefined;
    }

    /** Moves to the next content node — an element, an element's end, or text that is not only white space — unless the
     * reader is on one (`MoveToContent`). */
    moveToContent(): XmlNodeType {
        for (;;) {
            switch (this.#nodeType) {
                case 'element':
                case 'endElement':
                case 'text':
                case 'cdata':
                    return this.#nodeType;
                case 'none':
                case 'whitespace':
                case 'significantWhitespace':
                    if (!this.read()) return this.#nodeType;
                    break;
                default:
                    return unreachable(this.#nodeType);
            }
        }
    }

    /** The text of the element the reader is on, its text and CDATA joined, leaving the reader past its end
     * (`ReadElementContentAsString`). An element that holds an element is not well-formed for it. */
    readElementContentAsString(): string {
        if (this.#nodeType !== 'element') throw new Error(`Internal: the reader is on ${this.#nodeType}, not an element.`);
        if (this.#isEmptyElement) {
            this.read();
            return '';
        }
        let text = '';
        this.read();
        for (;;) {
            const node = this.nodeType();
            switch (node) {
                case 'text':
                case 'cdata':
                case 'whitespace':
                case 'significantWhitespace':
                    text += this.#value;
                    this.read();
                    break;
                case 'element':
                    throw new XmlError('ReadElementContentAs() methods cannot be called on an element that has child elements.');
                case 'endElement':
                    this.read();
                    return text;
                case 'none':
                    throw new XmlError('Unexpected end of file.');
                default:
                    return unreachable(node);
            }
        }
    }

    /** Reads on to the next element of `localName` in `namespaceUri`; false, at the part's end, when there is none
     * (`ReadToFollowing`). */
    readToFollowing(localName: string, namespaceUri: string): boolean {
        while (this.read()) {
            if (this.#nodeType === 'element' && this.#localName === localName && this.#namespaceUri === namespaceUri) return true;
        }
        return false;
    }

    /** Moves past the element the reader is on and all it holds, or past any other node (`Skip`). */
    skip(): void {
        if (this.#nodeType !== 'element' || this.#isEmptyElement) {
            this.read();
            return;
        }
        const depth = this.#depth;
        while (this.read()) {
            if (this.nodeType() === 'endElement' && this.depth() === depth) break;
        }
        this.read();
    }

    /** Reads the next node; false at the part's end. */
    read(): boolean {
        if (this.#eof) return false;
        if (this.#popOnNext) {
            this.#stack.pop();
            this.#popOnNext = false;
        }
        if (!this.#started) this.#prolog();
        const text = this.#text;
        for (;;) {
            if (this.#pos >= text.length) {
                if (!this.#ended) throw new NeedMore();
                if (!this.#rootSeen) throw new XmlError('Root element is missing.');
                if (this.#stack.length > 0) {
                    throw new XmlError(`Unexpected end of file has occurred. The following elements are not closed: ${this.#stack.map(f => f.name).toReversed().join(', ')}.`);
                }
                this.#eof = true;
                this.#node('none', '', '', '', '', this.#stack.length);
                return false;
            }
            if (text.charCodeAt(this.#pos) !== 0x3c) {
                this.#readText(text);
                return true;
            }
            const next = this.#charAt(text, this.#pos + 1);
            if (next === '/') {
                this.#readEnd(text);
                return true;
            }
            if (next === '?') {
                this.#skipInstruction(text);
                continue;
            }
            if (next === '!') {
                if (this.#readBang(text)) return true;
                continue;
            }
            this.#readStart(text);
            return true;
        }
    }

    // ---- the nodes -------------------------------------------------------------------------------

    #node(type: XmlNodeType, localName: string, prefix: string, namespaceUri: string, value: string, depth: number): void {
        this.#nodeType = type;
        this.#localName = localName;
        this.#prefix = prefix;
        this.#namespaceUri = namespaceUri;
        this.#value = value;
        this.#depth = depth;
        this.#isEmptyElement = false;
        this.#attributes = [];
    }

    /** The character at `at`, asking for more text where it has not arrived; '' past the part's end. */
    #charAt(text: string, at: number): string {
        if (at < text.length) return text.charAt(at);
        if (!this.#ended) throw new NeedMore();
        return '';
    }

    /** Where `token` next begins at or after `from`, asking for more text where it has not arrived; -1 past the part's end. */
    #find(text: string, token: string, from: number): number {
        const at = text.indexOf(token, from);
        if (at < 0 && !this.#ended) throw new NeedMore();
        return at;
    }

    #prolog(): void {
        const text = this.#text;
        // The declaration, when there is one, is the part's first characters (XML 1.0, section 2.8).
        if (text.length < 6 && !this.#ended) throw new NeedMore();
        if (text.startsWith('<?xml') && isSpace(text.charCodeAt(5))) {
            const end = this.#find(text, '?>', 5);
            if (end < 0) throw new XmlError('The XML declaration is not ended.');
            const declaration = text.slice(5, end);
            const encoding = /\bencoding\s*=\s*["']([^"']*)["']/.exec(declaration)?.[1];
            if (!/\bversion\s*=\s*["']1\.[0-9]+["']/.test(declaration)) throw new XmlError('The XML declaration names no version 1.x.');
            if (encoding !== undefined && !/^(?:utf-?8|utf-?16|unicode)$/i.test(encoding)) {
                throw new XmlError(`The part declares the encoding '${encoding}', which is not read.`);
            }
            this.#pos = end + 2;
        }
        this.#started = true;
    }

    #readText(text: string): void {
        const lt = this.#find(text, '<', this.#pos);
        const end = lt < 0 ? text.length : lt;
        const raw = text.slice(this.#pos, end);
        checkCharacters(raw);
        if (raw.includes(']]>')) throw new XmlError("']]>' is not allowed in text.");
        // Line ends are normalised before references are expanded (XML 1.0 §2.11), so `&#13;` stays a carriage return.
        const value = unescape(lineFeeds(raw));
        let space = true;
        for (let i = 0; i < raw.length; i++) {
            if (!isSpace(raw.charCodeAt(i))) {
                space = false;
                break;
            }
        }
        const depth = this.#stack.length;
        if (depth === 0 && !space) throw new XmlError('Data at the root level is invalid.');
        if (depth > 0 && lt < 0) throw new XmlError(`Unexpected end of file has occurred. The following elements are not closed: ${this.#stack.map(f => f.name).toReversed().join(', ')}.`);
        const preserve = this.#stack.at(-1)?.preserve === true;
        this.#node(space ? (preserve ? 'significantWhitespace' : 'whitespace') : 'text', '', '', '', value, depth);
        this.#pos = end;
    }

    #skipInstruction(text: string): void {
        const end = this.#find(text, '?>', this.#pos + 2);
        if (end < 0) throw new XmlError('A processing instruction is not ended.');
        const body = text.slice(this.#pos + 2, end);
        const space = body.search(/\s/);
        const target = space < 0 ? body : body.slice(0, space);
        if (target.toLowerCase() === 'xml') throw new XmlError('Unexpected XML declaration. The XML declaration must be the first node in the document.');
        checkName(target);
        checkCharacters(body);
        this.#pos = end + 2;
    }

    /** A comment (skipped), CDATA (a node: true) or a document type (refused). */
    #readBang(text: string): boolean {
        const at = this.#pos;
        if (text.length < at + 9 && !this.#ended) throw new NeedMore();
        if (text.startsWith('<!--', at)) {
            const end = this.#find(text, '-->', at + 4);
            if (end < 0) throw new XmlError('A comment is not ended.');
            const body = text.slice(at + 4, end);
            if (body.includes('--') || body.endsWith('-')) throw new XmlError("An XML comment cannot contain '--', and '-' cannot be the last character.");
            checkCharacters(body);
            this.#pos = end + 3;
            return false;
        }
        if (text.startsWith('<![CDATA[', at)) {
            if (this.#stack.length === 0) throw new XmlError('Data at the root level is invalid.');
            const end = this.#find(text, ']]>', at + 9);
            if (end < 0) throw new XmlError('A CDATA section is not ended.');
            const body = text.slice(at + 9, end);
            checkCharacters(body);
            this.#node('cdata', '', '', '', lineFeeds(body), this.#stack.length);
            this.#pos = end + 3;
            return true;
        }
        if (text.startsWith('<!DOCTYPE', at)) {
            throw new XmlError("For security reasons DTD is prohibited in this XML document. To enable DTD processing set the DtdProcessing property on XmlReaderSettings to Parse and pass the settings into XmlReader.Create method.");
        }
        throw new XmlError('An unexpected markup declaration was met.');
    }

    #readEnd(text: string): void {
        const close = this.#find(text, '>', this.#pos + 2);
        if (close < 0) throw new XmlError('An end tag is not ended.');
        const name = text.slice(this.#pos + 2, close).replace(/[\t\n\r ]+$/, '');
        const frame = this.#stack.at(-1);
        if (frame === undefined) throw new XmlError(`The end tag '${name}' closes no element.`);
        if (frame.name !== name) throw new XmlError(`The '${frame.name}' start tag does not match the end tag of '${name}'.`);
        this.#node('endElement', frame.localName, frame.prefix, frame.namespaceUri, '', this.#stack.length - 1);
        this.#stack.pop();
        if (this.#stack.length === 0) this.#rootClosed = true;
        this.#pos = close + 1;
    }

    #readStart(text: string): void {
        if (this.#rootClosed) throw new XmlError('There are multiple root elements.');
        let at = this.#pos + 1;
        const nameEnd = (from: number): number => {
            let i = from;
            for (;;) {
                const c = this.#charAt(text, i);
                if (c === '' || c === '>' || c === '/' || c === '=' || isSpace(c.charCodeAt(0))) return i;
                i++;
            }
        };
        const end = nameEnd(at);
        const name = text.slice(at, end);
        checkName(name);
        at = end;
        const raw: { name: string; value: string }[] = [];
        let empty = false;
        for (;;) {
            let spaced = false;
            while (isSpace(this.#charAt(text, at).charCodeAt(0))) {
                at++;
                spaced = true;
            }
            const c = this.#charAt(text, at);
            if (c === '>') {
                at++;
                break;
            }
            if (c === '/') {
                if (this.#charAt(text, at + 1) !== '>') throw new XmlError(`The start tag '${name}' is not ended.`);
                at += 2;
                empty = true;
                break;
            }
            if (c === '') throw new XmlError(`The start tag '${name}' is not ended.`);
            if (!spaced) throw new XmlError(`The start tag '${name}' needs white space before an attribute.`);
            const attributeEnd = nameEnd(at);
            const attributeName = text.slice(at, attributeEnd);
            checkName(attributeName);
            at = attributeEnd;
            while (isSpace(this.#charAt(text, at).charCodeAt(0))) at++;
            if (this.#charAt(text, at) !== '=') throw new XmlError(`The attribute '${attributeName}' has no value.`);
            at++;
            while (isSpace(this.#charAt(text, at).charCodeAt(0))) at++;
            const quote = this.#charAt(text, at);
            if (quote !== '"' && quote !== "'") throw new XmlError(`The value of the attribute '${attributeName}' is not quoted.`);
            const close = this.#find(text, quote, at + 1);
            if (close < 0) throw new XmlError(`The value of the attribute '${attributeName}' is not ended.`);
            const written = text.slice(at + 1, close);
            if (written.includes('<')) throw new XmlError(`'<', hexadecimal value 0x3C, is an invalid attribute character.`);
            checkCharacters(written);
            // Attribute-value normalization (XML 1.0, section 3.3.3): each literal white space character is a space,
            // a line end one space; a character reference keeps its character.
            const value = unescape(written.replace(/\r\n|[\t\n\r]/g, ' '));
            if (raw.some(a => a.name === attributeName)) throw new XmlError(`'${attributeName}' is a duplicate attribute name.`);
            raw.push({ name: attributeName, value });
            at = close + 1;
        }
        // The namespaces it declares, then its own name and its attributes' resolved through them.
        let declared: Map<string, string> | null = null;
        let preserve = this.#stack.at(-1)?.preserve === true;
        for (const attribute of raw) {
            if (attribute.name === 'xmlns') (declared ??= new Map()).set('', attribute.value);
            else if (attribute.name.startsWith('xmlns:')) (declared ??= new Map()).set(attribute.name.slice(6), attribute.value);
            else if (attribute.name === 'xml:space') preserve = attribute.value === 'preserve' ? true : attribute.value === 'default' ? false : preserve;
        }
        const lookup = (prefix: string): string | undefined => {
            if (prefix === 'xml') return xmlNamespace;
            if (prefix === 'xmlns') return xmlnsNamespace;
            const own = declared?.get(prefix);
            if (own !== undefined) return own;
            for (let i = this.#stack.length - 1; i >= 0; i--) {
                const uri = this.#stack[i]?.declared?.get(prefix);
                if (uri !== undefined) return uri;
            }
            return prefix === '' ? '' : undefined;
        };
        const [prefix, localName] = split(name);
        const namespaceUri = lookup(prefix);
        if (namespaceUri === undefined) throw new XmlError(`'${prefix}' is an undeclared prefix.`);
        const attributes = raw.map((attribute): XmlAttribute => {
            if (attribute.name === 'xmlns') return { name: 'xmlns', prefix: '', localName: 'xmlns', namespaceUri: xmlnsNamespace, value: attribute.value };
            const [p, local] = split(attribute.name);
            if (p === '') return { name: attribute.name, prefix: '', localName: local, namespaceUri: '', value: attribute.value };
            const uri = lookup(p);
            if (uri === undefined) throw new XmlError(`'${p}' is an undeclared prefix.`);
            return { name: attribute.name, prefix: p, localName: local, namespaceUri: uri, value: attribute.value };
        });
        for (let i = 0; i < attributes.length; i++) {
            const a = attributes[i];
            if (a === undefined || a.namespaceUri === '') continue;
            for (let j = i + 1; j < attributes.length; j++) {
                const b = attributes[j];
                if (b !== undefined && b.localName === a.localName && b.namespaceUri === a.namespaceUri) {
                    throw new XmlError(`The attribute '${b.localName}' of the namespace '${b.namespaceUri}' is written twice.`);
                }
            }
        }
        const depth = this.#stack.length;
        this.#node('element', localName, prefix, namespaceUri, '', depth);
        this.#isEmptyElement = empty;
        this.#attributes = attributes;
        this.#stack.push({ name, localName, prefix, namespaceUri, declared, preserve });
        this.#popOnNext = empty;
        this.#rootSeen = true;
        if (empty && depth === 0) this.#rootClosed = true;
        this.#pos = at;
    }
}
