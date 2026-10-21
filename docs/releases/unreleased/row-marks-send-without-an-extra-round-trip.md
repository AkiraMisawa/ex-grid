Product: ExGrid
Kind: Fixed
ADR: 0240, 0043
Docs: exgrid/columns#mark--template-and-action-columns

Successive row Marks with no earlier browser-held input are sent in order without waiting a round
trip between them; Marks behind a held key or Action continue to wait in the existing Input Order.
A row click made while the Server connection is down no longer holds later input after
reconnection: the next input exposes the lost click, which is refused without replaying it.
