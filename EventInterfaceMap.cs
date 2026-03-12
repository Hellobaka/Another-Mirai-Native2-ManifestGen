using System.Collections.Generic;

namespace AMN.ManifestGen
{
    public static class EventInterfaceMap
    {
        // key: 接口 full name（namespace + name）
        // val: (typeId, displayName)
        public static readonly Dictionary<string, (int Type, string Name)> Map = new()
        {
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupMessageHandler"] = ((int)PluginEventType.GroupMsg, "群消息"),
            ["Another_Mirai_Native.Abstractions.Handlers.IPrivateMessageHandler"] = ((int)PluginEventType.PrivateMsg, "私聊消息"),
            ["Another_Mirai_Native.Abstractions.Handlers.IDiscussMessageHandler"] = ((int)PluginEventType.DiscussMsg, "讨论组消息"),

            ["Another_Mirai_Native.Abstractions.Handlers.IUploadHandler"] = ((int)PluginEventType.Upload, "文件上传"),

            ["Another_Mirai_Native.Abstractions.Handlers.IAdminChangeHandler"] = ((int)PluginEventType.AdminChange, "管理员变更"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupMemberDecreaseHandler"] = ((int)PluginEventType.GroupMemberDecrease, "群成员减少"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupMemberIncreaseHandler"] = ((int)PluginEventType.GroupMemberIncrease, "群成员增加"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupBanHandler"] = ((int)PluginEventType.GroupBan, "群禁言"),

            ["Another_Mirai_Native.Abstractions.Handlers.IFriendAddedHandler"] = ((int)PluginEventType.FriendAdded, "好友添加完成"),
            ["Another_Mirai_Native.Abstractions.Handlers.IFriendRequestHandler"] = ((int)PluginEventType.FriendRequest, "好友添加请求"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupAddRequestHandler"] = ((int)PluginEventType.GroupAddRequest, "加群申请"),

            ["Another_Mirai_Native.Abstractions.Handlers.IStartUpHandler"] = ((int)PluginEventType.StartUp, "酷Q启动"),
            ["Another_Mirai_Native.Abstractions.Handlers.IExitHandler"] = ((int)PluginEventType.Exit, "酷Q退出"),
            ["Another_Mirai_Native.Abstractions.Handlers.IEnableHandler"] = ((int)PluginEventType.Enable, "插件启用"),
            ["Another_Mirai_Native.Abstractions.Handlers.IDisableHandler"] = ((int)PluginEventType.Disable, "插件禁用"),
        };
    }
}
