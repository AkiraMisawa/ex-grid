namespace ExGrid.Data.Xlsx;

/// <summary>
/// An element of a worksheet that the reader does not read (<see cref="XlsxSheetReader.OtherElements"/>), kept as far as
/// a reader of the file needs to judge what it holds: its name, its attributes, its text, and its children to
/// <see cref="MaxDepth"/> levels, the first <see cref="MaxChildren"/> of each. It is never the whole element, and is not
/// written back.
/// </summary>
public sealed class XlsxElement
{
    /// <summary>The levels of children kept below a worksheet's element.</summary>
    public const int MaxDepth = 3;

    /// <summary>The children kept of each element, in the file's order.</summary>
    public const int MaxChildren = 16;

    /// <summary>The characters of an element's own text that are kept.</summary>
    public const int MaxText = 1024;

    internal XlsxElement(string name, IReadOnlyDictionary<string, string> attributes, IReadOnlyList<XlsxElement> children, string? text, bool childrenCut)
    {
        Name = name;
        Attributes = attributes;
        Children = children;
        Text = text;
        ChildrenCut = childrenCut;
    }

    /// <summary>The element's local name, as <c>pageMargins</c>; an extension's without its prefix, as <c>conditionalFormattings</c>.</summary>
    public string Name { get; }

    /// <summary>Its attributes by local name (<c>r:id</c> is <c>id</c>), namespace declarations left out.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; }

    /// <summary>Its child elements, the first <see cref="MaxChildren"/>, to <see cref="MaxDepth"/> levels below the worksheet's element.</summary>
    public IReadOnlyList<XlsxElement> Children { get; }

    /// <summary>Its own text, its first <see cref="MaxText"/> characters; null when it holds none.</summary>
    public string? Text { get; }

    /// <summary>Whether it held children that <see cref="Children"/> leaves out, past the limits.</summary>
    public bool ChildrenCut { get; }

    /// <summary>The attribute <paramref name="name"/>, or null when the element does not write it.</summary>
    public string? Attribute(string name) => Attributes.GetValueOrDefault(name);

    /// <inheritdoc />
    public override string ToString() => Name;
}
