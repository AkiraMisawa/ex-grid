using System.Reflection;
using System.Text;

namespace ExGrid.Docs;

/// <summary>
/// The shipped assemblies' documentation, as the build read it (ExGrid.Docs.Generator): each
/// member's summary as HTML, keyed by its documentation id (ADR-0110).
/// </summary>
internal static partial class ApiDocs
{
    /// <summary>The summary of <paramref name="member"/>, or null when it has none.</summary>
    public static string? Summary(MemberInfo member) =>
        Summaries.TryGetValue(Id(member), out var html) ? html : null;

    /// <summary>The documentation id the compiler wrote for <paramref name="member"/>.</summary>
    public static string Id(MemberInfo member) => member switch
    {
        Type type => "T:" + Name(type),
        PropertyInfo property => "P:" + Name(property.DeclaringType!) + "." + property.Name,
        FieldInfo field => "F:" + Name(field.DeclaringType!) + "." + field.Name,
        EventInfo e => "E:" + Name(e.DeclaringType!) + "." + e.Name,
        ConstructorInfo ctor => "M:" + Name(ctor.DeclaringType!) + ".#ctor" + Parameters(ctor),
        MethodInfo method => "M:" + Name(method.DeclaringType!) + "." + method.Name
            + (method.IsGenericMethodDefinition ? "``" + method.GetGenericArguments().Length : "")
            + Parameters(method),
        _ => "",
    };

    private static string Name(Type type)
    {
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
            type = type.GetGenericTypeDefinition();
        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    private static string Parameters(MethodBase method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
            return "";
        return "(" + string.Join(",", parameters.Select(p => Encode(p.ParameterType))) + ")";
    }

    private static string Encode(Type type)
    {
        if (type.IsGenericParameter)
            return (type.DeclaringMethod is null ? "`" : "``") + type.GenericParameterPosition;
        if (type.IsByRef)
            return Encode(type.GetElementType()!) + "@";
        if (type.IsArray)
            return Encode(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition().FullName!;
            var name = definition[..definition.IndexOf('`')].Replace('+', '.');
            return name + "{" + string.Join(",", type.GetGenericArguments().Select(Encode)) + "}";
        }
        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    /// <summary>How a reader writes <paramref name="type"/>: <c>IReadOnlyList&lt;string&gt;</c>, <c>int?</c>.</summary>
    public static string Display(Type type, bool nullable = false)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return Display(underlying) + "?";
        var text = new StringBuilder();
        if (type.IsArray)
            text.Append(Display(type.GetElementType()!)).Append("[]");
        else if (type.IsGenericType)
        {
            var name = type.Name;
            text.Append(name[..name.IndexOf('`')]).Append('<')
                .Append(string.Join(", ", type.GetGenericArguments().Select(a => Display(a))))
                .Append('>');
        }
        else
            text.Append(Alias(type) ?? type.Name);
        if (nullable)
            text.Append('?');
        return text.ToString();
    }

    private static string? Alias(Type type) => type.IsEnum ? null : Type.GetTypeCode(type) switch
    {
        TypeCode.Boolean => "bool",
        TypeCode.Int32 => "int",
        TypeCode.Int64 => "long",
        TypeCode.Double => "double",
        TypeCode.Decimal => "decimal",
        TypeCode.String => "string",
        TypeCode.Single => "float",
        TypeCode.Byte => "byte",
        TypeCode.Char => "char",
        _ => type == typeof(object) ? "object" : type == typeof(void) ? "void" : null,
    };
}
