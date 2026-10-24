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

const noCatch = {
    meta: {
        type: 'problem',
        docs: { description: 'A catch that does not rethrow makes an error disappear.' },
        messages: {
            clause: 'A catch that does not rethrow makes the error disappear. Let it propagate, or rethrow with context; where the failure is truly expected, disable this rule on the line with `-- <why>`.',
            method: '`.catch` makes a rejection disappear unless its handler rethrows. Let it propagate; where the rejection is truly expected, disable this rule on the line with `-- <why>`.',
        },
        schema: [],
    },
    create(context) {
        return {
            CatchClause(node) {
                if (!rethrows(node.body)) {
                    context.report({ node, messageId: 'clause' });
                }
            },
            CallExpression(node) {
                const callee = node.callee;
                if (callee.type === 'MemberExpression' && !callee.computed && callee.property.type === 'Identifier' && callee.property.name === 'catch') {
                    context.report({ node: callee.property, messageId: 'method' });
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
    },
};
