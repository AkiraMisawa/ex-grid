// The Docs Site's uses of JavaScript (ADR-0110).

// The copy button writes the code to the clipboard, which C# cannot reach.
export function copyText(text) {
    return navigator.clipboard.writeText(text);
}

// The framework switch is kept between visits (ADR-0350 §12). A browser that keeps nothing for the
// site refuses storage with a SecurityError, or a QuotaExceededError on a write; that refusal is the
// answer "not kept". Any other error is a defect and is thrown.
function refused(error) {
    return error instanceof DOMException && (error.name === "SecurityError" || error.name === "QuotaExceededError");
}

export function readFramework(key) {
    try {
        return { kept: true, value: window.localStorage.getItem(key) };
    } catch (error) {
        if (refused(error)) return { kept: false, value: null };
        throw error;
    }
}

export function writeFramework(key, value) {
    try {
        window.localStorage.setItem(key, value);
        return true;
    } catch (error) {
        if (refused(error)) return false;
        throw error;
    }
}
