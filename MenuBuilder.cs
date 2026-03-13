using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace AMN.ManifestGen
{
    public static class MenuBuilder
    {
        private const string MenuAttributeFullName = "Another_Mirai_Native.Abstractions.Attributes.MenuAttribute";

        public static AppInfo.Menu[] BuildMenusFromEntryType(MetadataReader mr, TypeDefinition entryTypeDef)
        {
            var menus = new List<AppInfo.Menu>();

            foreach (var caHandle in entryTypeDef.GetCustomAttributes())
            {
                var ca = mr.GetCustomAttribute(caHandle);

                if (!MetadataHelpers.IsTargetAttribute(mr, ca, MenuAttributeFullName))
                {
                    continue;
                }

                var decoded = ca.DecodeValue(new MetadataHelpers.SimpleTypeProvider());

                // 约定：MenuAttribute(string name) 或 MenuAttribute(string name, string function)
                var name = decoded.FixedArguments.Length >= 1 ? decoded.FixedArguments[0].Value as string : null;
                string? function = decoded.FixedArguments.Length >= 2 ? decoded.FixedArguments[1].Value as string : null;

                // 命名参数也支持：Name / Function / Address
                int address = 0;
                foreach (var na in decoded.NamedArguments)
                {
                    if (na.Name == "Name")
                    {
                        name = na.Value as string;
                    }
                    else if (na.Name == "Function")
                    {
                        function = na.Value as string;
                    }
                    else if (na.Name == "Address" && na.Value is int i)
                    {
                        address = i;
                    }
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new InvalidOperationException("MenuAttribute 缺少 Name。");
                }

                // fallback：如果没提供 function，就用一个固定值（或用 entry type name）
                function ??= name;

                menus.Add(new AppInfo.Menu
                {
                    name = name!,
                    function = function,
                    address = address
                });
            }

            // 输出稳定：按 name 排序
            return menus.OrderBy(m => m.name).ToArray();
        }
    }
}
