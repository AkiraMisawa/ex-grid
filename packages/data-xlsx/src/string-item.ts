// A string item's text — a shared string's `<si>`, an inline string's `<is>` — as ExGrid.Data.Xlsx reads it.

import { decodeXString } from './x-string';
import type { XmlReader } from './xml-reader';

/** The text of a string item (`<si>` or `<is>`), the reader on its start: its `<t>`, or its runs' `<t>`s in order, a
 * phonetic guide left out. Leaves the reader on its end. */
export function readText(xml: XmlReader): [string, boolean] {
    if (xml.isEmptyElement()) return ['', false];
    const depth = xml.depth();
    let text = '';
    let rich = false;
    xml.read();
    while (!xml.eof() && !(xml.nodeType() === 'endElement' && xml.depth() === depth)) {
        if (xml.nodeType() !== 'element') {
            xml.read();
            continue;
        }
        switch (xml.localName()) {
            case 't':
                text += xml.readElementContentAsString();
                break;
            case 'r':
                rich = true;
                if (xml.isEmptyElement()) xml.skip();
                else xml.read();
                break;
            default:
                // A run's properties, a phonetic guide and its properties: not text.
                xml.skip();
                break;
        }
    }
    return [decodeXString(text), rich];
}
