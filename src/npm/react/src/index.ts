/**
 * ExGrid for React: the Script Core's Framework Adapter (ADR-0350). `ExGrid` renders the element the core
 * draws into and hands the core its props; everything the grid shows and decides is the core's.
 *
 * @packageDocumentation
 */
export { ExGrid } from './ex-grid';
export type { ExGridApi, ExGridProps } from './ex-grid';
export { templateColumn } from './template-column';
export type { TemplateColumnOptions } from './template-column';
export { chromeMounts, useCellEditor, useEditorCompletion, useFilterPanel, useFindPanel, useFormulaBarText, useMenuItems, useNameBox } from './chrome';
export type {
    CellEditorHandles, ChromeMountsMade, CompletionOptionProps, EditorCompletionHandles, ExGridChrome, FilterPanelHandles, FindPanelHandles, FormulaBarTextHandles,
    MenuItemProps, NameBoxHandles,
} from './chrome';
export { GridPortals, useGridPortals } from './portals';
