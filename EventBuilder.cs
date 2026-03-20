using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace AMN.ManifestGen
{
    public static class EventBuilder
    {
        public static AppInfo.Event[] BuildEventsFromEntryType(MetadataReader mr, TypeDefinition entryTypeDef)
        {
            var priorities = PriorityReader.ReadEventPriorities(mr, entryTypeDef);
            var ifaceFullNames = MetadataHelpers.GetImplementedInterfaceFullNames(mr, entryTypeDef);
            var events = new List<AppInfo.Event>();

            foreach (var ifaceFull in ifaceFullNames)
            {
                if (!EventInterfaceMap.Map.TryGetValue(ifaceFull, out var info))
                {
                    continue;
                }

                var ifaceShortName = ifaceFull.Split('.').Last();
                var priority = priorities.TryGetValue(info.Type, out var p) ? p : 1;

                events.Add(new AppInfo.Event
                {
                    id = events.Count + 2 + 1,
                    type = info.Type,
                    name = info.Name,
                    function = ifaceShortName, // 或 ifaceFull
                    priority = priority,
                });
            }

            // 按 type 排序，输出更稳定
            return events.OrderBy(e => e.type).ToArray();
        }
    }
}
