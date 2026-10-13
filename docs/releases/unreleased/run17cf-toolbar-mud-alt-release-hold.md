Product: ExGrid
Kind: Behaviour
ADR: 0050, 0100, 0021

Where you declare the Alt key alone (`Alt+Alt`), the grid hears the Alt key's release on its root, with the `keyup` listener it already has, and holds it in its place while it holds keys, handing it to your `keyup` handler after the press it releases: an Alt pressed alone and released while keys are held reaches you in the order it was typed, whatever the round trip. A chord's release (Alt with another key, such as Alt+↓), or one whose press the grid did not hear, no longer reaches your handler at all. The keys typed after a release wait until your handler has answered it, then go to the frame you handed the keyboard to (`HandKeyboardToFrameAsync`) or, told nothing, to the grid; your handler calls `HandKeyboardToFrameAsync`, and `HandKeyboardToAsync` for the frame's control, before its first `await`.
