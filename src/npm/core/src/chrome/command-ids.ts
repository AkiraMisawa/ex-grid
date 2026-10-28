/**
 * The ids of the commands the core defines (ADR-0010, ADR-0036, ADR-0044), in one place: the core builds
 * its menus with them, and a Chrome (the built-in one's labels, a Wrapper's words and icons) resolves its
 * rendering from them. A Consumer's own command carries an id of its own choosing, which none of these is.
 */
export const gridCommandIds = Object.freeze({
    /** Sorts the column ascending. Excel's S. */
    sortAscending: 'sort-ascending',
    /** Sorts the column descending. Excel's O. */
    sortDescending: 'sort-descending',
    /** Removes the column's filter; enabled only while it has one. Excel's C. */
    clearFilter: 'clear-filter',
    /** Hides the column. */
    hide: 'hide',
    /** Pins the columns up to this one. */
    pin: 'pin',
    /** Unpins all columns. */
    unpin: 'unpin',
    /** Sizes the column to fit what the Window holds (ADR-0016). */
    sizeToFit: 'size-to-fit',
    /** The Context Menu's copy of the selection (ADR-0036). */
    copy: 'copy',
    /** The Context Menu's copy of the selection with its headers (ADR-0005, ADR-0036). */
    copyWithHeaders: 'copy-with-headers',
} as const);

/**
 * The ids of the words of the notices a page holding another version's files calls for (ADR-0123), as the C# core's
 * `ReloadNoticeLabelIds` names them: each product resolves its stylesheet's notice by the same id.
 */
export const reloadNoticeLabelIds = Object.freeze({
    /** The stylesheet the page holds is not the one a product was built with: shown at the top of that product's
     * root, by the stylesheet's own rules (ERR-11). */
    stylesheet: 'reload-notice-stylesheet',
} as const);

/**
 * The ids of the names the core gives its own fields (ADR-0051, ADR-0203), for assistive technology: a Consumer's
 * `commandLabel` answers them in its own words, as it answers a command's.
 */
export const gridLabelIds = Object.freeze({
    /** The Formula Bar's Name Box. */
    nameBox: 'name-box',
    /** The Formula Bar's text field. */
    formulaBar: 'formula-bar',
    /** A header's ▾, which opens the column menu (ADR-0044). */
    columnMenu: 'column-menu',
    /** A collapsed Header Group's toggle, its group's label at `{label}` (ADR-0400 section 8). */
    headerGroupExpand: 'header-group-expand',
    /** An expanded Header Group's toggle, its group's label at `{label}` (ADR-0400 section 8). */
    headerGroupCollapse: 'header-group-collapse',
} as const);
