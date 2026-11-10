Product: ExGrid
Kind: Behaviour
Docs: exgrid/columns#a-compact-header
ADR: 0500

Changing `HideColumnMenu` (`hideColumnMenu` in ExGrid for React) on a grid in use no longer measures its Auto columns
afresh. An Auto width now keeps what its values taught it and gives up, or takes back, only the room its header kept for
the ▾: a column its header decided still narrows and widens by that room, and a column an earlier, wider value had grown
stays at that width, where it used to come back at the width of the rows painted at that moment. Nothing changes for a
grid that sets `HideColumnMenu` once.
