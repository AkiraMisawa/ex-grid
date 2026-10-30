import type { XlsxElement } from '@ex-grid/data-xlsx';
import { parseDouble } from './invariant';
import type { NotCarriedKind } from './open-report';
import { trimWhiteSpace } from './text-casing';

// What a worksheet's element the file layer does not read holds that the Open Report says (ADR-0330 §4), as ExSheet.Xlsx's
// SheetElements says it: what changes how the Sheet looks or prints, or what else it held. Every file Excel writes carries
// elements in a form that holds nothing of the kind — its margins at their defaults, its phonetic settings, the error marks
// a user dismissed, its dimension — and those are not listed, so a plain file's report is empty and a listed entry means
// something was left behind.

/** Excel's default margins, in inches: left, right, top, bottom, header, footer. */
const defaultMargins: readonly (readonly [string, number])[] = [['left', 0.7], ['right', 0.7], ['top', 0.75], ['bottom', 0.75], ['header', 0.3], ['footer', 0.3]];

/** One kind of thing not carried, with what names it more closely, where something does. */
export interface ElementNotCarried {
    readonly kind: NotCarriedKind;
    readonly detail: string | undefined;
}

/** What `element` holds that is not carried, each kind with a detail; nothing when it holds none. */
export function elementNotCarried(element: XlsxElement): ElementNotCarried[] {
    const kinds: ElementNotCarried[] = [];
    const add = (kind: NotCarriedKind, detail?: string): void => {
        kinds.push({ kind, detail });
    };
    const attribute = (name: string): string | undefined => element.attributes.get(name);
    switch (element.name) {
        // Nothing a Sheet shows or prints: the used range, the sheet's default row (judged below), phonetic settings,
        // dismissed error marks, calculation flags.
        case 'dimension':
        case 'phoneticPr':
        case 'ignoredErrors':
        case 'sheetCalcPr':
            break;
        case 'sheetFormatPr': {
            if (isTrue(attribute('zeroHeight'))) add('hiddenRows');
            if (isTrue(attribute('customHeight'))) add('rowHeights');
            // Excel's own base width is 8 characters, which the default 8.43 is; any other default is the Sheet's own.
            const base = attribute('baseColWidth');
            if (attribute('defaultColWidth') !== undefined || (base !== undefined && base !== '8')) add('defaultColumnWidth');
            break;
        }
        case 'sheetViews':
            if (element.children.some(viewSetsAnything)) add('view');
            break;
        case 'sheetPr':
            if (element.children.some(c => c.name === 'tabColor')) add('sheetProperties');
            if (element.children.some(c => c.name === 'pageSetUpPr' && isTrue(c.attributes.get('fitToPage')))) add('printSettings');
            break;
        case 'pageMargins':
            if (defaultMargins.some(([name, inches]) => {
                const value = attribute(name);
                return value !== undefined && marginInches(value) !== inches;
            })) {
                add('printSettings');
            }
            break;
        case 'printOptions':
            if (['horizontalCentered', 'verticalCentered', 'headings', 'gridLines'].some(a => isTrue(attribute(a)))) add('printSettings');
            break;
        case 'pageSetup':
            if (pageSetupSetsAnything(element)) add('printSettings');
            break;
        case 'headerFooter':
            if (element.children.some(c => c.text !== null && trimWhiteSpace(c.text) !== '')) add('printSettings');
            break;
        case 'rowBreaks':
        case 'colBreaks':
            if (element.children.some(c => c.name === 'brk') || element.childrenCut) add('printSettings');
            break;
        case 'extLst':
            // Each extension by what it holds: Excel 2010's conditional formatting, validation, sparklines, slicers.
            for (const extension of element.children.filter(e => e.name === 'ext')) {
                for (const held of extension.children) kinds.push(...elementNotCarried(held));
            }
            break;
        case 'mergeCells': add('mergedCells'); break;
        case 'conditionalFormatting':
        case 'conditionalFormattings': add('conditionalFormatting'); break;
        case 'dataValidations': add('dataValidation'); break;
        case 'legacyDrawing': add('notesAndComments'); break;
        case 'hyperlinks': add('hyperlinks'); break;
        case 'drawing':
        case 'picture':
        case 'oleObjects':
        case 'controls':
        case 'legacyDrawingHF':
        case 'sparklineGroups': add('picturesAndCharts'); break;
        case 'sheetProtection':
        case 'protectedRanges': add('protection'); break;
        case 'autoFilter':
        case 'sortState': add('autoFilter'); break;
        case 'tableParts': add('excelTable'); break;
        case 'slicerList':
        case 'timelineRefs': add('slicers'); break;
        default: add('other', element.name); break;
    }
    return kinds;
}

/**
 * Whether a `<sheetView>` changes how the Sheet looks beyond what is carried: gridlines, headings or zeros hidden, Formulas
 * shown, right to left, a zoom, a page layout. The selection and the scroll position change nothing; frozen panes are said on
 * their own (`frozenPanes`).
 */
function viewSetsAnything(view: XlsxElement): boolean {
    if (view.name !== 'sheetView') return false;
    const a = (name: string): string | undefined => view.attributes.get(name);
    const zoom = a('zoomScale');
    const layout = a('view');
    return isFalse(a('showGridLines')) || isFalse(a('showRowColHeaders')) || isFalse(a('showZeros'))
        || isTrue(a('showFormulas')) || isTrue(a('rightToLeft'))
        || (zoom !== undefined && zoom !== '100')
        || (layout !== undefined && layout !== 'normal');
}

/**
 * Whether a `<pageSetup>` sets anything beyond Excel's defaults (ECMA-376 Part 1, §18.3.1.63). Letter (1) and A4 (9), the
 * paper Excel writes by the machine's locale, are not counted; any other paper, or a size of its own, is. The printer's
 * resolution and its settings part are the printer's, and are not counted.
 */
function pageSetupSetsAnything(element: XlsxElement): boolean {
    for (const [name, value] of element.attributes) {
        let sets: boolean;
        switch (name) {
            case 'horizontalDpi':
            case 'verticalDpi':
            case 'id':
            case 'usePrinterDefaults':
                sets = false;
                break;
            case 'paperSize': sets = value !== '1' && value !== '9'; break;
            case 'orientation': sets = value !== 'default' && value !== 'portrait'; break;
            case 'scale': sets = value !== '100'; break;
            case 'fitToWidth':
            case 'fitToHeight':
            case 'firstPageNumber':
            case 'copies': sets = value !== '1'; break;
            case 'pageOrder': sets = value !== 'downThenOver'; break;
            case 'cellComments': sets = value !== 'none'; break;
            case 'errors': sets = value !== 'displayed'; break;
            case 'blackAndWhite':
            case 'draft':
            case 'useFirstPageNumber': sets = isTrue(value); break;
            default: sets = true; break;
        }
        if (sets) return true;
    }
    return false;
}

function isTrue(value: string | undefined): boolean {
    return value === '1' || value === 'true';
}

function isFalse(value: string | undefined): boolean {
    return value === '0' || value === 'false';
}

/** A margin in inches; one that does not read as a number differs from every default. */
function marginInches(value: string): number {
    const inches = parseDouble(value);
    return inches === undefined ? Number.NaN : inches;
}
