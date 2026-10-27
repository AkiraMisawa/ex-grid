Product: ExSheet
Kind: Fixed
ADR: 0121, 0124

ExSheet in TypeScript answers `TODAY()` and `NOW()`, which showed `#GETTING_DATA` for good: the day and the time are the browser's zone's, read again at midnight and as each minute turns, as the Blazor Sheet reads them. `today` fixes the day and `timeZone` names another zone; one the browser does not know is refused by name.
