Product: ExSheet
Kind: Added
ADR: 0100

ExSheet in TypeScript and React takes the Sheet Toolbar's rows as data, `toolbar={['default', { label, keyTip, items }]}`, where a row of your own holds ExSheet's items by their commands, separators and buttons of your own; and Ctrl+F1 hides and shows the toolbar where `onShowToolbarChanged` hears it, as the Blazor Sheet's two-way `ShowToolbar` does.
