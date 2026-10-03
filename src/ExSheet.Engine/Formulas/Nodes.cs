namespace ExSheet.Engine.Formulas;

/// <summary>A node of a parsed Formula. Its text is the Formula's, kept by the Entry (ADR-0047).</summary>
internal abstract class Node
{
    /// <summary>Every Reference this node reads, statically: the dependency graph's edges.</summary>
    public virtual IEnumerable<Reference> References => [];

    /// <summary>Every structured reference this node reads, statically: a Linked Table's readers (ADR-0049).</summary>
    public virtual IEnumerable<StructuredReferenceNode> StructuredReferences => [];
}

internal sealed class NumberNode(double number) : Node
{
    public double Number { get; } = number;
}

internal sealed class TextNode(string text) : Node
{
    public string Text { get; } = text;
}

internal sealed class BooleanNode(bool value) : Node
{
    public bool Value { get; } = value;
}

internal sealed class ErrorNode(ErrorValue error) : Node
{
    public ErrorValue Error { get; } = error;
}

/// <summary>An argument left empty, as in <c>IF(A1,,1)</c>.</summary>
internal sealed class MissingNode : Node
{
    public static MissingNode Instance { get; } = new();
}

internal sealed class ReferenceNode(Reference reference) : Node
{
    public Reference Reference { get; } = reference;

    public override IEnumerable<Reference> References => [Reference];
}

/// <summary>A structured reference into a Linked Table, <c>Table[Column]</c> (ADR-0049).</summary>
internal sealed class StructuredReferenceNode(string table, string column) : Node
{
    public string Table { get; } = table;

    public string Column { get; } = column;

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => [this];
}

/// <summary>A name that is not a function call, a Reference or a boolean: ExSheet defines no names, so it is <c>#NAME?</c>.</summary>
internal sealed class NameNode(string name) : Node
{
    public string Name { get; } = name;
}

/// <summary>
/// The intersection of two operands written with whitespace between them, one of them a name
/// (<c>item one</c>): ExSheet defines no names, so it is <c>#NAME?</c> (ADR-0047). The parser
/// refuses an intersection of two References.
/// </summary>
internal sealed class IntersectionNode(Node left, Node right) : Node
{
    public Node Left { get; } = left;

    public Node Right { get; } = right;

    public override IEnumerable<Reference> References => Left.References.Concat(Right.References);
}

internal sealed class FunctionNode(string name, IReadOnlyList<Node> arguments, FunctionDefinition? function) : Node
{
    /// <summary>The name: upper case for a declared function, as typed otherwise.</summary>
    public string Name { get; } = name;

    public IReadOnlyList<Node> Arguments { get; } = arguments;

    /// <summary>The declared function, or <see langword="null"/> for a name outside the set (<c>#NAME?</c>).</summary>
    public FunctionDefinition? Function { get; } = function;

    public override IEnumerable<Reference> References => Arguments.SelectMany(a => a.References);

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Arguments.SelectMany(a => a.StructuredReferences);
}

internal sealed class ParenthesesNode(Node inner) : Node
{
    public Node Inner { get; } = inner;

    public override IEnumerable<Reference> References => Inner.References;

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Inner.StructuredReferences;
}

internal sealed class UnaryNode(char op, Node operand) : Node
{
    public char Operator { get; } = op;

    public Node Operand { get; } = operand;

    public override IEnumerable<Reference> References => Operand.References;

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Operand.StructuredReferences;
}

internal sealed class PercentNode(Node operand) : Node
{
    public Node Operand { get; } = operand;

    public override IEnumerable<Reference> References => Operand.References;

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Operand.StructuredReferences;
}

internal sealed class BinaryNode(string op, Node left, Node right) : Node
{
    public string Operator { get; } = op;

    public Node Left { get; } = left;

    public Node Right { get; } = right;

    public override IEnumerable<Reference> References => Left.References.Concat(Right.References);

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Left.StructuredReferences.Concat(Right.StructuredReferences);
}
