// The names the package and its parts are read and written by (ECMA-376 Part 1 and Part 2, transitional), as
// ExGrid.Data.Xlsx's Names holds them.

export const mainNamespace = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main';
export const relationshipsNamespace = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';
export const packageRelationshipsNamespace = 'http://schemas.openxmlformats.org/package/2006/relationships';
export const contentTypesNamespace = 'http://schemas.openxmlformats.org/package/2006/content-types';
export const drawingNamespace = 'http://schemas.openxmlformats.org/drawingml/2006/main';
export const strictPrefix = 'http://purl.oclc.org/ooxml/';

export const officeDocumentType = `${relationshipsNamespace}/officeDocument`;
export const worksheetType = `${relationshipsNamespace}/worksheet`;
export const sharedStringsType = `${relationshipsNamespace}/sharedStrings`;
export const stylesType = `${relationshipsNamespace}/styles`;
export const themeType = `${relationshipsNamespace}/theme`;
export const sheetMetadataType = `${relationshipsNamespace}/sheetMetadata`;
export const tableType = `${relationshipsNamespace}/table`;

export const workbookContent = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml';
export const templateContent = 'application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml';
export const binaryContent = 'application/vnd.ms-excel.sheet.binary.macroEnabled.main';
export const worksheetContent = 'application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml';
export const sharedStringsContent = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml';
export const stylesContent = 'application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml';
export const sheetMetadataContent = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheetMetadata+xml';
export const tableContent = 'application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml';
export const relationshipsContent = 'application/vnd.openxmlformats-package.relationships+xml';
export const vbaProjectContent = 'application/vnd.ms-office.vbaProject';

/** The future-metadata extension that marks a dynamic array (Excel 365). */
export const dynamicArrayExtension = '{bdbb8cdc-fa1e-496e-a857-3c3f30c029c3}';
