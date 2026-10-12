Product: ExGrid
Kind: Behaviour
ADR: 0050, 0100

Where you declare the Alt key alone (`Alt+Alt`), the keys typed after its release wait until your `keyup` handler has answered it, then go to the frame you handed the keyboard to (`HandKeyboardToFrameAsync`) or, told nothing, to the grid as before; the grid waits only for an empty question it sends behind the release, so your handler must call `HandKeyboardToFrameAsync`, and `HandKeyboardToAsync` for the frame's control, before its first `await`. An Alt pressed alone while the grid still holds keys can still reach you before or after its release depending on the round trip, and an outcome there can depend on timing (a known limitation, not yet decided).
