using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace AMN.ManifestGen
{
    public static class MetadataHelpers
    {
        public static string GetFullTypeName(MetadataReader mr, TypeDefinition td)
        {
            var ns = mr.GetString(td.Namespace);
            var name = mr.GetString(td.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public static (string Id, string Name, string Version, string? Author, string? Description) DecodePluginAttribute(MetadataReader mr, CustomAttribute ca)
        {
            var decoded = ca.DecodeValue(new SimpleTypeProvider());

            if (decoded.FixedArguments.Length < 3)
            {
                throw new InvalidOperationException("PluginAttribute 构造参数不足：期望 (string id, string name, string version)。");
            }

            var id = decoded.FixedArguments[0].Value as string ?? "";
            var name = decoded.FixedArguments[1].Value as string ?? "";
            var version = decoded.FixedArguments[2].Value as string ?? "";

            string? author = null;
            string? description = null;

            foreach (var na in decoded.NamedArguments)
            {
                var val = na.Value as string;

                if (na.Name == "Author")
                {
                    author = val;
                }
                else if (na.Name == "Description")
                {
                    description = val;
                }
            }

            return (id, name, version, author, description);
        }
        public static IEnumerable<string> GetImplementedInterfaceFullNames(MetadataReader mr, TypeDefinition typeDef)
        {
            foreach (var ih in typeDef.GetInterfaceImplementations())
            {
                var impl = mr.GetInterfaceImplementation(ih);
                yield return GetTypeFullName(mr, impl.Interface);
            }
        }

        public static bool IsTargetAttribute(MetadataReader mr, CustomAttribute ca, string attributeFullName)
        {
            var (ns, name) = GetAttributeTypeName(mr, ca);
            var full = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            return string.Equals(full, attributeFullName, StringComparison.Ordinal);
        }

        public static string GetTypeFullName(MetadataReader mr, EntityHandle typeHandle)
        {
            return typeHandle.Kind switch
            {
                HandleKind.TypeReference => GetTypeRefFullName(mr, (TypeReferenceHandle)typeHandle),
                HandleKind.TypeDefinition => GetTypeDefFullName(mr, (TypeDefinitionHandle)typeHandle),
                _ => ""
            };
        }

        private static (string Namespace, string Name) GetAttributeTypeName(MetadataReader mr, CustomAttribute ca)
        {
            var ctor = ca.Constructor;
            return ctor.Kind switch
            {
                HandleKind.MemberReference => GetFromMemberRef(mr, (MemberReferenceHandle)ctor),
                HandleKind.MethodDefinition => GetFromMethodDef(mr, (MethodDefinitionHandle)ctor),
                _ => ("", "")
            };
        }

        private static (string Namespace, string Name) GetFromMemberRef(MetadataReader mr, MemberReferenceHandle h)
        {
            var memberRef = mr.GetMemberReference(h);
            return GetTypeNameFromEntityHandle(mr, memberRef.Parent);
        }

        private static (string Namespace, string Name) GetFromMethodDef(MetadataReader mr, MethodDefinitionHandle h)
        {
            var md = mr.GetMethodDefinition(h);
            var td = mr.GetTypeDefinition(md.GetDeclaringType());
            return (mr.GetString(td.Namespace), mr.GetString(td.Name));
        }

        private static (string Namespace, string Name) GetTypeNameFromEntityHandle(MetadataReader mr, EntityHandle h)
        {
            return h.Kind switch
            {
                HandleKind.TypeReference => GetFromTypeRef(mr, (TypeReferenceHandle)h),
                HandleKind.TypeDefinition => GetFromTypeDef(mr, (TypeDefinitionHandle)h),
                _ => ("", "")
            };
        }

        private static (string Namespace, string Name) GetFromTypeRef(MetadataReader mr, TypeReferenceHandle h)
        {
            var tr = mr.GetTypeReference(h);
            return (mr.GetString(tr.Namespace), mr.GetString(tr.Name));
        }

        private static (string Namespace, string Name) GetFromTypeDef(MetadataReader mr, TypeDefinitionHandle h)
        {
            var td = mr.GetTypeDefinition(h);
            return (mr.GetString(td.Namespace), mr.GetString(td.Name));
        }

        private static string GetTypeRefFullName(MetadataReader mr, TypeReferenceHandle h)
        {
            var tr = mr.GetTypeReference(h);
            var ns = mr.GetString(tr.Namespace);
            var name = mr.GetString(tr.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        private static string GetTypeDefFullName(MetadataReader mr, TypeDefinitionHandle h)
        {
            var td = mr.GetTypeDefinition(h);
            var ns = mr.GetString(td.Namespace);
            var name = mr.GetString(td.Name);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        // 给 DecodeValue 用（string/int 等）
        public sealed class SimpleTypeProvider : ICustomAttributeTypeProvider<object>
        {
            public object GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode;
            public object GetSystemType() => typeof(Type);
            public object GetSZArrayType(object elementType) => elementType;

            public object GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => new object();
            public object GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => new object();
            public object GetTypeFromSerializedName(string name) => name;

            public PrimitiveTypeCode GetUnderlyingEnumType(object type) => PrimitiveTypeCode.Int32;
            public bool IsSystemType(object type) => false;
        }
    }
}
