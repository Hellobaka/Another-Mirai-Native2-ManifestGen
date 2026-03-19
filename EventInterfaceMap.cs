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

            ["Another_Mirai_Native.Abstractions.Handlers.IGroupFileUploadHandler"] = ((int)PluginEventType.Upload, "文件上传"),

            ["Another_Mirai_Native.Abstractions.Handlers.IAdminChangeHandler"] = ((int)PluginEventType.AdminChange, "管理员变更"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupMemberDecreaseHandler"] = ((int)PluginEventType.GroupMemberDecrease, "群成员减少"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupMemberIncreaseHandler"] = ((int)PluginEventType.GroupMemberIncrease, "群成员增加"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupMemberBannedHandler"] = ((int)PluginEventType.GroupBan, "群成员被禁言"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupMemberUnbannedHandler"] = ((int)PluginEventType.GroupBan, "群成员被解除禁言"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupWholeBannedHandler"] = ((int)PluginEventType.GroupBan, "群全员禁言"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupWholeUnbannedHandler"] = ((int)PluginEventType.GroupBan, "群解除全员禁言"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupInviteRequestHandler"] = ((int)PluginEventType.GroupAddRequest, "受群邀请"),

            ["Another_Mirai_Native.Abstractions.Handlers.IFriendAddedHandler"] = ((int)PluginEventType.FriendAdded, "好友添加完成"),
            ["Another_Mirai_Native.Abstractions.Handlers.IFriendAddRequestHandler"] = ((int)PluginEventType.FriendRequest, "好友添加请求"),
            ["Another_Mirai_Native.Abstractions.Handlers.IGroupAddRequestHandler"] = ((int)PluginEventType.GroupAddRequest, "加群申请"),

            ["Another_Mirai_Native.Abstractions.Handlers.IStartUpHandler"] = ((int)PluginEventType.StartUp, "酷Q启动"),
            ["Another_Mirai_Native.Abstractions.Handlers.IExitHandler"] = ((int)PluginEventType.Exit, "酷Q退出"),
            ["Another_Mirai_Native.Abstractions.Handlers.IEnableHandler"] = ((int)PluginEventType.Enable, "插件启用"),
            ["Another_Mirai_Native.Abstractions.Handlers.IDisableHandler"] = ((int)PluginEventType.Disable, "插件禁用"),
        };
    }
}
