Product: ExPivot
Kind: Fixed
ADR: 0410, 0240

A key typed as Show Details opens its records, while a Chrome's tab or dialog Close is still on its way (on Blazor Server, a round trip later), is now taken by the new tab or by Close, as the keyboard was meant to move there, and no longer by the report the records cover or by the page. The Show Details dialog holds the keyboard itself until its Close is drawn.
