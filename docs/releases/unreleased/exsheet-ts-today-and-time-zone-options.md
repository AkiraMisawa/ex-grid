Product: ExSheet
Kind: Added
ADR: 0121, 0124

ExSheet in TypeScript takes `today`, a fixed Sheet Day for a report that must show the same day whenever it is opened, and `timeZone`, an IANA zone whose day and time `TODAY()` and `NOW()` answer, as the Blazor Sheet's `Today` and `TimeZone` parameters do. A zone the browser does not know, or a day that is not one, is refused by name.
