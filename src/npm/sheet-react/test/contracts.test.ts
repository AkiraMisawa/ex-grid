// @vitest-environment happy-dom
// The contract every Wrapper keeps with ExSheet's own slots (ADR-0390 section 7), run here against a Chrome of plain
// controls, as each Wrapper runs it against its design system's.
import { formatCellsSlotContract, sheetTabSlotContract, toolbarItemSlotContract } from './sheet-slot-contract';
import { plainSheetChrome } from './plain-sheet-chrome';

toolbarItemSlotContract('a plain Chrome', plainSheetChrome);
formatCellsSlotContract('a plain Chrome', plainSheetChrome);
sheetTabSlotContract('a plain Chrome', plainSheetChrome);
