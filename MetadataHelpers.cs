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

        public static (string Id, string Name, string Version, string? Description, string? Author) DecodePluginAttribute(MetadataReader mr, CustomAttribute ca)
        {
            var decoded = ca.DecodeValue(new SimpleTypeProvider());

            if (decoded.FixedArguments.Length < 3)
            {
                throw new InvalidOperationException("PluginAttribute 构造参数不足：期望 (string id, string name, string version)。");
            }

            var id = decoded.FixedArguments[0].Value as string ?? "";
            var name = decoded.FixedArguments[1].Value as string ?? "";
            var version = decoded.FixedArguments[2].Value as string ?? "";

            string? description = decoded.FixedArguments.Length > 3 ? (decoded.FixedArguments[3].Value as string ?? "") : null;
            string? author = decoded.FixedArguments.Length > 3 ? (decoded.FixedArguments[4].Value as string ?? "") : null;

            return (id, name, version, description, author);
        }

        /// <summary>
        /// 获取该类型及其整条继承链上所有实现的接口全名。
        /// 对于外部程序集基类 CommandHandlerBase，直接补充其已知接口。
        /// </summary>
        public static IEnumerable<string> GetImplementedInterfaceFullNames(MetadataReader mr, TypeDefinition typeDef)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var results = new List<string>();
            var current = typeDef;

            while (true)
            {
                // 收集当前层的直接接口
                foreach (var ih in current.GetInterfaceImplementations())
                {
                    var impl = mr.GetInterfaceImplementation(ih);
                    var fullName = GetTypeFullName(mr, impl.Interface);
                    if (!string.IsNullOrEmpty(fullName) && visited.Add(fullName))
                        results.Add(fullName);
                }

                // 向上走一层基类
                var baseHandle = current.BaseType;
                if (baseHandle.IsNil)
                    break;

                if (baseHandle.Kind == HandleKind.TypeDefinition)
                {
                    // 基类在同一程序集内，继续向上遍历
                    current = mr.GetTypeDefinition((TypeDefinitionHandle)baseHandle);
                }
                else if (baseHandle.Kind == HandleKind.TypeReference)
                {
                    // 外部程序集基类：特判 CommandHandlerBase
                    var tr = mr.GetTypeReference((TypeReferenceHandle)baseHandle);
                    if (mr.GetString(tr.Name) == "CommandHandlerBase")
                    {
                        foreach (var iface in CommandHandlerBaseInterfaces)
                        {
                            if (visited.Add(iface))
                                results.Add(iface);
                        }
                    }
                    break;
                }
                else
                {
                    break;
                }
            }

            return results;
        }

        private static readonly string[] CommandHandlerBaseInterfaces =
        [
            "Another_Mirai_Native.Abstractions.Handlers.IGroupMessageHandler",
            "Another_Mirai_Native.Abstractions.Handlers.IPrivateMessageHandler",
        ];

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
