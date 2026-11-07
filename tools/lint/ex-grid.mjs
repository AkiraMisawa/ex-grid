// The workspace's own lint rules, loaded by .oxlintrc.json as a JS plugin. Each one stops a way code
// carries on with a value it made up when the real one is missing or an operation failed: the shape of
// a quiet fallback, which looks plausible on screen and is the defect the spine's first rule forbids
// (AGENTS.md, "The spine of the design"; CONVENTIONS.md, "No quiet fallback").

/** Whether a node is a value a fallback typically makes up: a literal, `undefined`, `[]` or `{}`. */
function isStandIn(node) {
    switch (node.type) {
        case 'Literal':
            return true;
        case 'TemplateLiteral':
            return node.expressions.length === 0;
        case 'Identifier':
            return node.name === 'undefined' || node.name === 'NaN' || node.name === 'Infinity';
        case 'UnaryExpression':
            return (node.operator === '-' || node.operator === '+') && isStandIn(node.argument);
        case 'ArrayExpression':
            return node.elements.length === 0;
        case 'ObjectExpression':
            return node.properties.length === 0;
        default:
            return false;
    }
}

/** Whether the block's last statement throws, so the catch adds context and lets the error go on. */
function rethrows(block) {
    const last = block.body[block.body.length - 1];
    return last !== undefined && last.type === 'ThrowStatement';
}

const functions = new Set(['FunctionDeclaration', 'FunctionExpression', 'ArrowFunctionExpression']);

/** Whether a `return` leaves the catch before its throw: a fallback answered on one path. A function
 * declared inside the block returns from itself, not from the catch, so it is not searched. */
function returnsFrom(node) {
    if (node === null || typeof node !== 'object') {
        return false;
    }
    if (Array.isArray(node)) {
        return node.some(returnsFrom);
    }
    if (node.type === 'ReturnStatement') {
        return true;
    }
    if (functions.has(node.type)) {
        return false;
    }
    return Object.entries(node).some(([key, value]) => key !== 'parent' && returnsFrom(value));
}

const noCatch = {
    meta: {
        type: 'problem',
        docs: { description: 'A catch that does not rethrow, on every path, makes an error disappear.' },
        messages: {
            clause: 'A catch that does not rethrow makes the error disappear. Let it propagate, or rethrow with context; where the failure is truly expected, disable this rule on the line with `-- <why>`.',
            method: '`.catch` makes a rejection disappear unless its handler rethrows. Let it propagate; where the rejection is truly expected, disable this rule on the line with `-- <why>`.',
            rejectedInThen: '`.then`\'s second argument is a `.catch`: it makes a rejection disappear unless it rethrows. Let it propagate; where the rejection is truly expected, disable this rule on the line with `-- <why>`.',
        },
        schema: [],
    },
    create(context) {
        return {
            CatchClause(node) {
                if (!rethrows(node.body) || returnsFrom(node.body)) {
                    context.report({ node, messageId: 'clause' });
                }
            },
            CallExpression(node) {
                const callee = node.callee;
                if (callee.type !== 'MemberExpression' || callee.computed || callee.property.type !== 'Identifier') {
                    return;
                }
                if (callee.property.name === 'catch') {
                    context.report({ node: callee.property, messageId: 'method' });
                } else if (callee.property.name === 'then' && node.arguments.length >= 2) {
                    context.report({ node: callee.property, messageId: 'rejectedInThen' });
                }
            },
        };
    },
};

const noStandInDefault = {
    meta: {
        type: 'problem',
        docs: { description: 'A missing value is not replaced by a made-up one.' },
        messages: {
            standIn: '`{{operator}}` replaces a missing value with a made-up one, and the result looks plausible. If the value may be absent by design, make that a parameter or property default where the signature shows it; if it may not, throw. Where this default is the specified answer, disable this rule on the line with `-- <ADR or why>`.',
        },
        schema: [],
    },
    create(context) {
        const check = (node, operator, right) => {
            if ((operator === '??' || operator === '||' || operator === '??=' || operator === '||=') && isStandIn(right)) {
                context.report({ node, messageId: 'standIn', data: { operator } });
            }
        };
        return {
            LogicalExpression(node) {
                check(node, node.operator, node.right);
            },
            AssignmentExpression(node) {
                check(node, node.operator, node.right);
            },
        };
    },
};

// A text field written from script with setRangeText keeps none of what the browser keeps of an edit typed: the edit
// joins no undo history, so a Ctrl+Z after it walks a history the typing never made, and the field sends no `change`
// when it is left. Held input taken again wrote its text so, and a Ctrl+Z after held typing undid nothing where the same
// keys typed one at a time were undone (the Hunt of 2026-11-05, differential seed 1165771808). Every edit the grid makes
// for a user's input goes through pending-commit.ts's typeText, which edits through the browser's own command where the
// field holds the keyboard.
const noScriptEdit = {
    meta: {
        type: 'problem',
        docs: { description: 'A user\'s edit is made through the browser\'s editing, not written from script.' },
        messages: {
            setRangeText: '`setRangeText` writes a field outside its undo history: a Ctrl+Z after it undoes something else than the same input typed would. Edit through `typeText` (pending-commit.ts), which uses the browser\'s own editing where the field holds the keyboard; where the text is not a user\'s edit, disable this rule on the line with `-- <why>`.',
        },
        schema: [],
    },
    create(context) {
        return {
            CallExpression(node) {
                const callee = node.callee;
                if (callee.type === 'MemberExpression' && !callee.computed && callee.property.type === 'Identifier'
                    && callee.property.name === 'setRangeText') {
                    context.report({ node: callee.property, messageId: 'setRangeText' });
                }
            },
        };
    },
};

// The command a call's argument names outright, if it does.
const namedCommand = (expression) => (expression.type === 'Literal' && typeof expression.value === 'string' ? [expression.value] : []);

const noFieldHistory = {
    meta: {
        type: 'problem',
        docs: { description: 'An open edit\'s undo and redo walk its own history, never the field\'s.' },
        messages: {
            history: '`execCommand(\'{{command}}\')` walks a field\'s own undo history, whose runs the browser ends whenever anything else on the page repaints, so where it stops depends on timing (ADR-0380 section 3\'s note of 2026-11-07). An open edit walks its `EditHistory` (edit-history.ts); where the field holds no edit of the grid\'s, disable this rule on the line with `-- <why>`.',
        },
        schema: [],
    },
    create(context) {
        return {
            CallExpression(node) {
                const callee = node.callee;
                const [command] = node.arguments;
                if (callee.type === 'MemberExpression' && !callee.computed && callee.property.type === 'Identifier'
                    && callee.property.name === 'execCommand' && command !== undefined) {
                    // A command named outright, or by a choice between two named outright; one passed through a typed
                    // parameter is that parameter's type to hold.
                    const names = command.type === 'ConditionalExpression' ? [...namedCommand(command.consequent), ...namedCommand(command.alternate)] : namedCommand(command);
                    const history = names.find(name => name === 'undo' || name === 'redo');
                    if (history !== undefined) context.report({ node: callee.property, messageId: 'history', data: { command: history } });
                }
            },
        };
    },
};

const directive = /^\s*(?:oxlint|eslint)-disable(?:-next-line|-line)?\b/;

const disableStatesWhy = {
    meta: {
        type: 'problem',
        docs: { description: 'A rule switched off says why, after `--`.' },
        messages: {
            why: 'A rule switched off says why, after ` -- `, so a reviewer can judge the exception rather than find it.',
            whole: 'Switch a rule off on a line, not for a whole file or a block: the exception stays as narrow as its reason.',
        },
        schema: [],
    },
    create(context) {
        return {
            Program() {
                for (const comment of context.sourceCode.getAllComments()) {
                    const text = comment.value;
                    if (!directive.test(text)) {
                        continue;
                    }
                    if (!/-disable-(?:next-)?line\b/.test(text)) {
                        context.report({ loc: comment.loc, messageId: 'whole' });
                    } else if (!/\s--\s+\S/.test(text)) {
                        context.report({ loc: comment.loc, messageId: 'why' });
                    }
                }
            },
        };
    },
};

export default {
    meta: { name: 'ex-grid' },
    rules: {
        'no-catch': noCatch,
        'no-stand-in-default': noStandInDefault,
        'disable-states-why': disableStatesWhy,
        'no-script-edit': noScriptEdit,
        'no-field-history': noFieldHistory,
    },
};
