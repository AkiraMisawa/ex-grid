namespace ExSheet.Engine.Formulas;

/// <summary>A node of a parsed Formula. Its text is the Formula's, kept by the Entry (ADR-0047).</summary>
internal abstract class Node
{
    /// <summary>Every Reference this node reads, statically: the dependency graph's edges.</summary>
    public virtual IEnumerable<Reference> References => [];

    /// <summary>Every structured reference this node reads, statically: a Linked Table's readers (ADR-0049).</summary>
    public virtual IEnumerable<StructuredReferenceNode> StructuredReferences => [];

    /// <summary>Every function call this node makes, statically, its arguments' calls included: <c>TODAY</c>'s readers (ADR-0121).</summary>
    public virtual IEnumerable<FunctionNode> Calls => [];

    /// <summary>
    /// How deep a walk of this node goes, counted in nodes, where a walk that reads an operator's
    /// chain (<see cref="BinaryNode.Operands"/>) loops along it rather than descending: a leaf is 1, a
    /// node one more than its deepest child, except a <see cref="BinaryNode"/>'s left operand, which
    /// stands at the operator's own depth; and a name a <c>LET</c> binds one more than its value, which
    /// is computed where the name is read. The parser refuses a Formula deeper than
    /// <see cref="Parser.MostNesting"/>, so that no walk of a Formula the engine accepts can exhaust
    /// the stack.
    /// </summary>
    public virtual int Depth => 1;
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

    /// <summary>
    /// Whether an argument is left empty: written empty, or a <c>LET</c> name bound to a value left
    /// empty, as in <c>LET(x,,IF(A1,x,1))</c>. Such a name reads as the empty argument it is bound
    /// to would read in its place (ADR-0181; Excel's keyboard took <c>LET(x,,x)</c>, LET-107, run 17).
    /// </summary>
    public static bool IsEmpty(Node argument) => argument switch
    {
        MissingNode => true,
        NameNode { Binding: { } binding } => IsEmpty(binding.Value),
        _ => false,
    };
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

/// <summary>
/// A name that is not a function call, a Reference or a boolean. ExSheet defines no names of its
/// own, so it is <c>#NAME?</c> — unless a <c>LET</c> around it binds it (ADR-0181).
/// </summary>
internal sealed class NameNode(string name, int position, LetBinding? binding) : Node
{
    public string Name { get; } = name;

    /// <summary>Where the name stands in the Formula's text.</summary>
    public int Position { get; } = position;

    /// <summary>The <c>LET</c> binding the name reads, as the text's scopes resolve it; <see langword="null"/> for none (<c>#NAME?</c>).</summary>
    public LetBinding? Binding { get; } = binding;

    /// <summary>Reading a bound name computes its value there, so it is one deeper than the value; the value is bound before any name can read it.</summary>
    public override int Depth { get; } = binding is null ? 1 : binding.Value.Depth + 1;
}

/// <summary>One name a <c>LET</c> binds, and the value it is bound to (ADR-0181).</summary>
internal sealed class LetBinding(string name)
{
    /// <summary>The name as typed; names compare without regard to case.</summary>
    public string Name { get; } = name;

    /// <summary>The value's syntax, computed when the name is first read, and at most once (ADR-0181).</summary>
    public Node Value { get; set; } = MissingNode.Instance;
}

/// <summary>
/// <c>LET(name1, value1, …, calculation)</c> (ADR-0181): names bound inside one Formula, each seen by
/// the values after it and by the calculation. Its References, Linked Tables and calls are every one
/// its text names, the bindings' whether they are read or not, so the dependency graph, a cycle and a
/// waiting Linked Table follow the text.
/// </summary>
internal sealed class LetNode(IReadOnlyList<LetBinding> bindings, Node calculation) : Node
{
    public IReadOnlyList<LetBinding> Bindings { get; } = bindings;

    public Node Calculation { get; } = calculation;

    private IEnumerable<Node> Parts => Bindings.Select(b => b.Value).Append(Calculation);

    public override IEnumerable<Reference> References => Parts.SelectMany(p => p.References);

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Parts.SelectMany(p => p.StructuredReferences);

    public override IEnumerable<FunctionNode> Calls => Parts.SelectMany(p => p.Calls);

    public override int Depth { get; } = Math.Max(bindings.Select(b => b.Value.Depth).DefaultIfEmpty(0).Max(), calculation.Depth) + 1;
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

    public override IEnumerable<FunctionNode> Calls => Left.Calls.Concat(Right.Calls);

    public override int Depth { get; } = Math.Max(left.Depth, right.Depth) + 1;
}

internal sealed class FunctionNode(string name, IReadOnlyList<Node> arguments, FunctionDefinition? function) : Node
{
    /// <summary>The name: upper case for a declared function, as typed otherwise.</summary>
    public string Name { get; } = name;

    public IReadOnlyList<Node> Arguments { get; } = arguments;

    /// <summary>The declared function, or <see langword="null"/> for a name outside the set (<c>#NAME?</c>).</summary>
    public FunctionDefinition? Function { get; } = function;

    /// <summary>
    /// For <c>RAND</c> and <c>RANDBETWEEN</c>: which of the Formula's draws this call is, counted in
    /// the Formula's text from 0 (ADR-0180). Two calls are two draws; one call read twice, as a
    /// <c>LET</c> binding is, is one.
    /// </summary>
    public int Place { get; init; }

    public override IEnumerable<Reference> References => Arguments.SelectMany(a => a.References)
        .Concat(FunctionLibrary.CriteriaResultReferences(this));

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Arguments.SelectMany(a => a.StructuredReferences);

    public override IEnumerable<FunctionNode> Calls => Arguments.SelectMany(a => a.Calls).Prepend(this);

    public override int Depth { get; } = arguments.Select(a => a.Depth).DefaultIfEmpty(0).Max() + 1;
}

internal sealed class ParenthesesNode(Node inner) : Node
{
    public Node Inner { get; } = inner;

    public override IEnumerable<Reference> References => Inner.References;

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Inner.StructuredReferences;

    public override IEnumerable<FunctionNode> Calls => Inner.Calls;

    public override int Depth { get; } = inner.Depth + 1;
}

internal sealed class UnaryNode(char op, Node operand) : Node
{
    public char Operator { get; } = op;

    public Node Operand { get; } = operand;

    public override IEnumerable<Reference> References => Operand.References;

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Operand.StructuredReferences;

    public override IEnumerable<FunctionNode> Calls => Operand.Calls;

    public override int Depth { get; } = operand.Depth + 1;
}

internal sealed class PercentNode(Node operand) : Node
{
    public Node Operand { get; } = operand;

    public override IEnumerable<Reference> References => Operand.References;

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Operand.StructuredReferences;

    public override IEnumerable<FunctionNode> Calls => Operand.Calls;

    public override int Depth { get; } = operand.Depth + 1;
}

/// <summary>
/// A binary operator. Excel's operators associate to the left, so <c>=1+1+…+1</c> is a chain of
/// these down their left operands, as long as the Formula: a walk over one loops along that chain
/// (<see cref="Spine"/>, <see cref="Operands"/>) and descends only into the right operands, so a
/// chain of thousands of operators, which Excel accepts, never deepens the stack.
/// </summary>
internal sealed class BinaryNode(string op, Node left, Node right) : Node
{
    public string Operator { get; } = op;

    public Node Left { get; } = left;

    public Node Right { get; } = right;

    /// <summary>The left operand stands at this operator's depth: a walk loops along it.</summary>
    public override int Depth { get; } = Math.Max(left.Depth, right.Depth + 1);

    public override IEnumerable<Reference> References => Operands.SelectMany(o => o.References);

    public override IEnumerable<StructuredReferenceNode> StructuredReferences => Operands.SelectMany(o => o.StructuredReferences);

    public override IEnumerable<FunctionNode> Calls => Operands.SelectMany(o => o.Calls);

    /// <summary>
    /// This operator and those down its left operands, innermost (the first applied) first: the
    /// chain <c>1+2-3</c> is the <c>+</c>, then the <c>-</c>. The innermost's left operand is
    /// <see cref="Leftmost"/>.
    /// </summary>
    public IReadOnlyList<BinaryNode> Spine
    {
        get
        {
            var spine = new List<BinaryNode>();
            for (Node node = this; node is BinaryNode binary; node = binary.Left) spine.Add(binary);
            spine.Reverse();
            return spine;
        }
    }

    /// <summary>The first operand of the chain: the left operand of its innermost operator, which is no operator.</summary>
    public Node Leftmost
    {
        get
        {
            Node node = this;
            while (node is BinaryNode binary) node = binary.Left;
            return node;
        }
    }

    /// <summary>The chain's operands in the text's order: <see cref="Leftmost"/>, then each operator's right operand.</summary>
    public IEnumerable<Node> Operands
    {
        get
        {
            var spine = Spine;
            yield return spine[0].Left;
            foreach (var binary in spine) yield return binary.Right;
        }
    }
}
