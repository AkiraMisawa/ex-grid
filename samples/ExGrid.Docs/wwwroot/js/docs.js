// The Docs Site's one JavaScript use (ADR-0110): the copy button writes the code to the
// clipboard, which C# cannot reach.
export function copyText(text) {
    return navigator.clipboard.writeText(text);
}
