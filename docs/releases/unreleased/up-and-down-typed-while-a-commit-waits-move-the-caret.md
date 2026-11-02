Product: ExGrid
Kind: Fixed
ADR: 0380

Up and Down typed in the Cell Editor's Caret mode while a commit waited for its verdict were dropped once the verdict refused it, so the keys after them typed or deleted at the wrong place in the text. They now put the caret at the field's start and end, as they do when typed after the answer. In a text area of a Consumer's editor, where the line above or below is the browser's layout, or a field with a list of suggestions, a held Up or Down is said as an input that could not be taken again rather than dropped quietly.
