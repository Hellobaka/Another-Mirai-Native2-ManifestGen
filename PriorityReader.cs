using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace AMN.ManifestGen
{
    public static class PriorityReader
    {
        // TODO：改成你真实 Attribute 完整名
        private const string EventPriorityAttributeFullName = "Another_Mirai_Native.Abstractions.EventPriorityAttribute";

        // 返回：type(int) -> priority(int)
        public static Dictionary<int, int> ReadEventPriorities(MetadataReader mr, TypeDefinition entryTypeDef)
        {
            var dict = new Dictionary<int, int>();

            foreach (var caHandle in entryTypeDef.GetCustomAttributes())
            {
                var ca = mr.GetCustomAttribute(caHandle);
                if (!MetadataHelpers.IsTargetAttribute(mr, ca, EventPriorityAttributeFullName))
                    continue;

                var decoded = ca.DecodeValue(new MetadataHelpers.SimpleTypeProvider());

                // 约定构造器：EventPriorityAttribute(PluginEventType type, int priority)
                // enum 在 metadata 中会以其 underlying（通常 int32）出现
                if (decoded.FixedArguments.Length < 2)
                    throw new InvalidOperationException("EventPriorityAttribute 参数不足，期望 (PluginEventType type, int priority)。");

                var typeVal = decoded.FixedArguments[0].Value;
                var priVal = decoded.FixedArguments[1].Value;

                if (typeVal is not int eventType)
                    throw new InvalidOperationException("EventPriorityAttribute 第一个参数无法解析为 int（PluginEventType）。");

                if (priVal is not int priority)
                    throw new InvalidOperationException("EventPriorityAttribute 第二个参数无法解析为 int（Priority）。");

                // 如果重复定义同一个事件的优先级：你可以选择覆盖/报错
                dict[eventType] = priority;
            }

            return dict;
        }
    }
}
