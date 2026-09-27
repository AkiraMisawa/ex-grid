using System.Globalization;
using System.Text;

namespace ExSheet.Engine.Formulas;

/// <summary>A node of a parsed Formula. Writing a node back gives the invariant syntax (ADR-0047).</summary>
internal abstract class Node
{
    public abstract void WriteTo(StringBuilder text);

    /// <summary>Every Reference this node reads, statically: the dependency graph's edges.</summary>
    public virtual IEnumerable<Reference> References => [];
}

internal sealed class NumberNode(double number) : Node
{
    public double Number { get; } = number;

    public override void WriteTo(StringBuilder text) => text.Append(Number.ToString("R", CultureInfo.InvariantCulture));
}

internal sealed class TextNode(string text) : Node
{
    public string Text { get; } = text;

    public override void WriteTo(StringBuilder text) => text.Append('"').Append(Text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
}

internal sealed class BooleanNode(bool value) : Node
{
    public bool Value { get; } = value;

    public override void WriteTo(StringBuilder text) => text.Append(Value ? "TRUE" : "FALSE");
}

internal sealed class ErrorNode(ErrorValue error) : Node
{
    public ErrorValue Error { get; } = error;

    public override void WriteTo(StringBuilder text) => text.Append(Error.ToText());
}

/// <summary>An argument left empty, as in <c>IF(A1,,1)</c>.</summary>
internal sealed class MissingNode : Node
{
    public static MissingNode Instance { get; } = new();

    public override void WriteTo(StringBuilder text)
    {
    }
}

internal sealed class ReferenceNode(Reference reference) : Node
{
    public Reference Reference { get; } = reference;

    public override void WriteTo(StringBuilder text) => Reference.WriteTo(text);

    public override IEnumerable<Reference> References => [Reference];
}

/// <summary>
/// A structured reference into a Linked Table, <c>Table[Column]</c> (ADR-0049). Written with
/// single brackets when the column name is plain, and with double brackets when it holds a
/// character Excel's structured references treat as special.
/// </summary>
internal sealed class StructuredReferenceNode(string table, string column) : Node
{
    // The characters Microsoft's documentation lists as needing the column in double brackets.
    private const string Special = " \t\n\r,:.[]#'\"{}$^&*+=-></";

    public string Table { get; } = table;

    public string Column { get; } = column;

    public override void WriteTo(StringBuilder text)
    {
        text.Append(Table).Append('[');
        var doubled = Column.AsSpan().IndexOfAny(Special) >= 0;
        if (doubled) text.Append('[');
        foreach (var c in Column)
        {
            if (c is '[' or ']' or '#' or '\'') text.Append('\'');
            text.Append(c);
        }
        if (doubled) text.Append(']');
        text.Append(']');
    }
}

/// <summary>A name that is not a function call, a Reference or a boolean: ExSheet defines no names, so it is <c>#NAME?</c>.</summary>
internal sealed class NameNode(string name) : Node
{
    public string Name { get; } = name;

    public override void WriteTo(StringBuilder text) => text.Append(Name);
}

internal sealed class FunctionNode(string name, IReadOnlyList<Node> arguments, FunctionDefinition? function) : Node
{
    /// <summary>The name as written back: upper case for a declared function, as typed otherwise.</summary>
    public string Name { get; } = name;

    public IReadOnlyList<Node> Arguments { get; } = arguments;

    /// <summary>The declared function, or <see langword="null"/> for a name outside the set (<c>#NAME?</c>).</summary>
    public FunctionDefinition? Function { get; } = function;

    public override void WriteTo(StringBuilder text)
    {
        text.Append(Name).Append('(');
        for (var i = 0; i < Arguments.Count; i++)
        {
            if (i > 0) text.Append(',');
            Arguments[i].WriteTo(text);
        }
        text.Append(')');
    }

    public override IEnumerable<Reference> References => Arguments.SelectMany(a => a.References);
}

internal sealed class ParenthesesNode(Node inner) : Node
{
    public Node Inner { get; } = inner;

    public override void WriteTo(StringBuilder text)
    {
        text.Append('(');
        Inner.WriteTo(text);
        text.Append(')');
    }

    public override IEnumerable<Reference> References => Inner.References;
}

internal sealed class UnaryNode(char op, Node operand) : Node
{
    public char Operator { get; } = op;

    public Node Operand { get; } = operand;

    public override void WriteTo(StringBuilder text)
    {
        text.Append(Operator);
        Operand.WriteTo(text);
    }

    public override IEnumerable<Reference> References => Operand.References;
}

internal sealed class PercentNode(Node operand) : Node
{
    public Node Operand { get; } = operand;

    public override void WriteTo(StringBuilder text)
    {
        Operand.WriteTo(text);
        text.Append('%');
    }

    public override IEnumerable<Reference> References => Operand.References;
}

internal sealed class BinaryNode(string op, Node left, Node right) : Node
{
    public string Operator { get; } = op;

    public Node Left { get; } = left;

    public Node Right { get; } = right;

    public override void WriteTo(StringBuilder text)
    {
        Left.WriteTo(text);
        text.Append(Operator);
        Right.WriteTo(text);
    }

    public override IEnumerable<Reference> References => Left.References.Concat(Right.References);
}
