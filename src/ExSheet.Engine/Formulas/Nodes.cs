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

internal sealed class ReferenceNode(Reference reference) : Node
{
    public Reference Reference { get; } = reference;

    public override void WriteTo(StringBuilder text) => Reference.WriteTo(text);

    public override IEnumerable<Reference> References => [Reference];
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
