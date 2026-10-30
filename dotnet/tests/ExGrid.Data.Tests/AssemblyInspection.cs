using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ExGrid.Data.Tests;

/// <summary>
/// Reads an assembly's metadata to tell which of its members refer to another assembly: through the
/// IL of a method body (a call, a field, a type, a token), its locals and its catch clauses, through a
/// signature (a field's type, a method's parameters and return), through a type's base, interfaces
/// and generic constraints, and through an attribute. The runtime loads an assembly only when a
/// member that refers to it is compiled or run, so the members named are the only ways it loads.
/// </summary>
internal static class AssemblyInspection
{
    private static readonly Dictionary<short, OperandType> Operands = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value, o => o.OperandType);

    /// <summary>The members of the assembly at <paramref name="path"/> that refer to the assembly named
    /// <paramref name="referenced"/>, as <c>Namespace.Type.Member</c>, in order.</summary>
    public static IReadOnlyList<string> Referrers(string path, string referenced)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var refers = new Refers(md, referenced);
        if (!refers.Any)
            throw new InvalidOperationException($"{Path.GetFileName(path)} does not refer to {referenced} at all, so nothing is shown by finding who does.");

        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var typeHandle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(typeHandle);
            var typeName = Name(md, typeHandle);
            if (refers.Entity(type.BaseType)
                || type.GetInterfaceImplementations().Any(i => refers.Entity(md.GetInterfaceImplementation(i).Interface))
                || Constrained(md, type.GetGenericParameters(), refers))
            {
                found.Add(typeName);
            }
            foreach (var fieldHandle in type.GetFields())
            {
                var field = md.GetFieldDefinition(fieldHandle);
                if (field.DecodeSignature(refers, null))
                    found.Add($"{typeName}.{md.GetString(field.Name)}");
            }
            foreach (var methodHandle in type.GetMethods())
            {
                var method = md.GetMethodDefinition(methodHandle);
                var signature = method.DecodeSignature(refers, null);
                if (signature.ReturnType || signature.ParameterTypes.Any(p => p)
                    || Constrained(md, method.GetGenericParameters(), refers)
                    || (method.RelativeVirtualAddress != 0 && Body(pe.GetMethodBody(method.RelativeVirtualAddress), md, refers)))
                {
                    found.Add($"{typeName}.{md.GetString(method.Name)}");
                }
            }
        }
        foreach (var attributeHandle in md.CustomAttributes)
        {
            var attribute = md.GetCustomAttribute(attributeHandle);
            if (refers.Entity(attribute.Constructor))
                found.Add($"an attribute on {attribute.Parent.Kind} 0x{MetadataTokens.GetToken(attribute.Parent):X8}");
        }
        return [.. found];
    }

    private static bool Constrained(MetadataReader md, GenericParameterHandleCollection parameters, Refers refers)
        => parameters.Any(p => md.GetGenericParameter(p).GetConstraints().Any(c => refers.Entity(md.GetGenericParameterConstraint(c).Type)));

    /// <summary>Whether a method body refers to the assembly: by a token in its IL, its locals or a catch clause.</summary>
    private static bool Body(MethodBodyBlock body, MetadataReader md, Refers refers)
    {
        if (!body.LocalSignature.IsNil && md.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(refers, null).Any(l => l))
            return true;
        if (body.ExceptionRegions.Any(r => r.Kind == ExceptionRegionKind.Catch && refers.Entity(r.CatchType)))
            return true;
        var il = body.GetILReader();
        while (il.RemainingBytes > 0)
        {
            short code = il.ReadByte();
            if (code == 0xFE)
                code = unchecked((short)(0xFE00 | il.ReadByte()));
            switch (Operands[code])
            {
                case OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok or OperandType.InlineSig:
                    if (refers.Entity(MetadataTokens.EntityHandle(il.ReadInt32())))
                        return true;
                    break;
                case OperandType.InlineSwitch:
                    var targets = il.ReadInt32();
                    il.Offset += 4 * targets;
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                    il.Offset += 1;
                    break;
                case OperandType.InlineVar:
                    il.Offset += 2;
                    break;
                case OperandType.InlineI8 or OperandType.InlineR:
                    il.Offset += 8;
                    break;
                default: // InlineBrTarget, InlineI, InlineString, ShortInlineR
                    il.Offset += 4;
                    break;
            }
        }
        return false;
    }

    private static string Name(MetadataReader md, TypeDefinitionHandle handle)
    {
        var type = md.GetTypeDefinition(handle);
        var declaring = type.GetDeclaringType();
        var name = md.GetString(type.Name);
        if (!declaring.IsNil)
            return $"{Name(md, declaring)}+{name}";
        var space = md.GetString(type.Namespace);
        return space.Length == 0 ? name : $"{space}.{name}";
    }

    /// <summary>Tells whether a handle or a signature refers to the assembly, decoding signatures to
    /// whether any type in them does.</summary>
    private sealed class Refers(MetadataReader md, string referenced) : ISignatureTypeProvider<bool, object?>
    {
        private readonly HashSet<AssemblyReferenceHandle> scope = [.. md.AssemblyReferences.Where(h => md.GetString(md.GetAssemblyReference(h).Name) == referenced)];

        public bool Any => scope.Count > 0;

        public bool Entity(EntityHandle handle)
        {
            if (handle.IsNil)
                return false;
            switch (handle.Kind)
            {
                case HandleKind.TypeReference:
                    return Reference((TypeReferenceHandle)handle);
                case HandleKind.TypeSpecification:
                    return md.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, null);
                case HandleKind.MemberReference:
                {
                    var member = md.GetMemberReference((MemberReferenceHandle)handle);
                    if (Entity(member.Parent))
                        return true;
                    if (member.GetKind() == MemberReferenceKind.Field)
                        return member.DecodeFieldSignature(this, null);
                    var signature = member.DecodeMethodSignature(this, null);
                    return signature.ReturnType || signature.ParameterTypes.Any(p => p);
                }
                case HandleKind.MethodSpecification:
                {
                    var specification = md.GetMethodSpecification((MethodSpecificationHandle)handle);
                    return Entity(specification.Method) || specification.DecodeSignature(this, null).Any(t => t);
                }
                case HandleKind.StandaloneSignature:
                {
                    var signature = md.GetStandaloneSignature((StandaloneSignatureHandle)handle);
                    if (signature.GetKind() == StandaloneSignatureKind.LocalVariables)
                        return signature.DecodeLocalSignature(this, null).Any(l => l);
                    var method = signature.DecodeMethodSignature(this, null);
                    return method.ReturnType || method.ParameterTypes.Any(p => p);
                }
                default:
                    // A definition of this assembly's own refers to nothing by being named.
                    return false;
            }
        }

        private bool Reference(TypeReferenceHandle handle)
        {
            var scopeHandle = md.GetTypeReference(handle).ResolutionScope;
            return scopeHandle.Kind switch
            {
                HandleKind.AssemblyReference => scope.Contains((AssemblyReferenceHandle)scopeHandle),
                HandleKind.TypeReference => Reference((TypeReferenceHandle)scopeHandle),
                _ => false,
            };
        }

        public bool GetArrayType(bool elementType, ArrayShape shape) => elementType;

        public bool GetByReferenceType(bool elementType) => elementType;

        public bool GetFunctionPointerType(MethodSignature<bool> signature) => signature.ReturnType || signature.ParameterTypes.Any(p => p);

        public bool GetGenericInstantiation(bool genericType, ImmutableArray<bool> typeArguments) => genericType || typeArguments.Any(t => t);

        public bool GetGenericMethodParameter(object? genericContext, int index) => false;

        public bool GetGenericTypeParameter(object? genericContext, int index) => false;

        public bool GetModifiedType(bool modifier, bool unmodifiedType, bool isRequired) => modifier || unmodifiedType;

        public bool GetPinnedType(bool elementType) => elementType;

        public bool GetPointerType(bool elementType) => elementType;

        public bool GetPrimitiveType(PrimitiveTypeCode typeCode) => false;

        public bool GetSZArrayType(bool elementType) => elementType;

        public bool GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => false;

        public bool GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => Reference(handle);

        public bool GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}
