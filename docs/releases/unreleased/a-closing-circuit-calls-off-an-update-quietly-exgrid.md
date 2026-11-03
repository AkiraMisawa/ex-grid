Product: ExGrid
Kind: Fixed
ADR: 0451

On Blazor Server, a page left while its ExGrid's parameters were being sent to the browser no longer logs an unhandled
exception in the circuit. The call was only called off by the closing circuit; any other failure is still raised.
