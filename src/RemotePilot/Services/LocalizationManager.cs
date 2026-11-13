using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace RemotePilot.Services;

public enum AppLanguage
{
    Zh,
    En
}

public class LocalizationManager : INotifyPropertyChanged
{
    private static LocalizationManager? _instance;
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    private AppLanguage _currentLanguage = AppLanguage.Zh;
    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                ApplyLanguage(_currentLanguage);
                OnPropertyChanged();
                OnPropertyChanged("Item[]");
                LanguageChanged?.Invoke(this, _currentLanguage);
            }
        }
    }

    public string this[string key]
    {
        get
        {
            var dict = _currentLanguage == AppLanguage.En ? _enStrings : _zhStrings;
            return dict.TryGetValue(key, out var val) ? val : key;
        }
    }

    public event EventHandler<AppLanguage>? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationManager()
    {
    }

    public void Initialize(AppLanguage defaultLanguage = AppLanguage.Zh)
    {
        ApplyLanguage(defaultLanguage);
    }

    public void ToggleLanguage()
    {
        CurrentLanguage = CurrentLanguage == AppLanguage.Zh ? AppLanguage.En : AppLanguage.Zh;
    }

    public void SetLanguage(AppLanguage language)
    {
        CurrentLanguage = language;
    }

    public string GetString(string key) => this[key];

    public string Format(string key, params object[] args)
    {
        try
        {
            return string.Format(this[key], args);
        }
        catch (FormatException)
        {
            return this[key];
        }
    }

    private void ApplyLanguage(AppLanguage language)
    {
        var app = Application.Current;
        if (app == null) return;

        var other = language == AppLanguage.En ? _zhStrings : _enStrings;
        var strings = language == AppLanguage.En ? _enStrings : _zhStrings;

        // 先清掉另一语言的遗留键，避免键缺失时残留旧语种文案
        foreach (var k in other.Keys)
        {
            if (!strings.ContainsKey(k))
            {
                app.Resources.Remove(k);
            }
        }

        foreach (var (k, v) in strings)
        {
            app.Resources[k] = v;
        }
    }

    private static readonly Dictionary<string, string> _zhStrings = new()
    {
        // 导航栏
        ["Nav_Assets"] = "资产中心",
        ["Nav_Sessions"] = "会话工作台",
        ["Nav_Favorites"] = "收藏列表",
        ["Nav_Recent"] = "最近使用",
        ["Nav_Groups"] = "服务器分组",
        ["Nav_Help"] = "帮助手册",
        ["Nav_Templates"] = "连接方案模板",
        ["Nav_Settings"] = "设置",
        ["Nav_Feedback"] = "✉ 留下您的宝贵意见",
        ["Btn_ToggleLeftNav"] = "◀",
        ["Btn_ToggleLeftNavExpand"] = "▶",
        ["Tip_CollapseLeftNav"] = "收起导航栏",
        ["Tip_ExpandLeftNav"] = "展开导航栏",
        ["Msg_FeedbackTitle"] = "RemotePilot 意见反馈",
        ["Msg_FeedbackFallback"] = "感谢您愿意为 RemotePilot 提出宝贵建议，我们会认真阅读每一条反馈。\n\n当前电脑没有检测到可用的邮件客户端，因此未能自动打开发信窗口。\n请手动发送邮件至：\n\nremotemate@protonmail.com\n\n开源地址：https://github.com/OpenNodeSaas/RemotePilot\n欢迎在 GitHub 提交 Issue 或参与讨论。\n\n小提示：在此窗口按 Ctrl+C 可复制全文，粘贴到任意邮箱网页版即可发送。",

        // 顶部与操作
        ["Search_Placeholder"] = "全局搜索：名称 / IP / 分组 / 负责人 / 备注",
        ["Btn_Search"] = "搜索",
        ["Btn_AddServer"] = "新增资产",
        ["Btn_Import"] = "导入",
        ["Btn_ExportSelected"] = "导出所选",
        ["Btn_Delete"] = "批量删除",
        ["Btn_Open"] = "连接",
        ["Btn_Connect"] = "连接",
        ["Btn_ShowAll"] = "显示全部",
        ["Btn_SelectAll"] = "全选/反选",
        ["Btn_Probe"] = "检测延迟",
        ["Btn_SyncCloud"] = "接口导入",
        ["Btn_SaveSettings"] = "保存配置",
        ["Btn_ResetResolution"] = "恢复默认",
        ["Btn_Edit"] = "编辑",
        ["Btn_Remove"] = "删除",
        ["Btn_QuickConnect"] = "快速连接",
        ["Btn_ViewTable"] = "表格",
        ["Btn_ViewGrid"] = "宫格",
        ["Btn_Save"] = "确定",
        ["Btn_Cancel"] = "取消",
        ["Btn_Browse"] = "浏览...",
        ["Btn_Connect"] = "连接",

        // 主题与语言
        ["Theme_Light"] = "亮色模式",
        ["Theme_Dark"] = "暗色模式",
        ["Lang_Zh"] = "简体中文",
        ["Lang_En"] = "English",

        // 工作区与面板
        ["Panel_Details"] = "资产详情与快速配置",
        ["Panel_Collapse"] = "收起详情",
        ["Panel_Expand"] = "展开详情",

        // 表格列
        ["Col_Action"] = "操作",
        ["Col_Favorite"] = "收藏",
        ["Col_Select"] = "选择",
        ["Col_Group"] = "分组",
        ["Col_Template"] = "模板",
        ["Col_Owner"] = "负责人",
        ["Col_ServerName"] = "服务器名与IP/端口",
        ["Col_IpPort"] = "IP和端口",
        ["Col_Name"] = "名称",
        ["Col_Protocol"] = "协议",
        ["Col_Username"] = "账号",
        ["Col_Status"] = "状态",
        ["Col_ExpiresOn"] = "到期时间",
        ["Col_Note1"] = "备注1",
        ["Col_Note2"] = "备注2",
        ["Col_Note3"] = "备注3",
        ["Col_Note4"] = "备注4",

        // 列设置
        ["Btn_ColumnSettings"] = "列设置",
        ["ColSettings_Title"] = "列设置",
        ["ColSettings_Hint"] = "「名称」和「操作」为必要列，不可隐藏。",
        ["ColSettings_FontSize"] = "字号：",
        ["ColSettings_MoveUp"] = "上移",
        ["ColSettings_MoveDown"] = "下移",
        ["FontSize_Small"] = "小",
        ["FontSize_Normal"] = "标准",
        ["FontSize_Large"] = "大",
        ["FontSize_XLarge"] = "特大",

        // 工具栏配置项
        ["Setting_WindowMode"] = "窗口模式:",
        ["Setting_ModeNormal"] = "正常",
        ["Setting_ModeFullscreen"] = "全屏",
        ["Setting_ModeSmall"] = "小屏",
        ["Setting_ModeTab"] = "标签窗口",
        ["Setting_DeviceControl"] = "设备控制:",
        ["Setting_DriveMap"] = "硬盘映射",
        ["Setting_Audio"] = "声音",
        ["Setting_Clipboard"] = "使用粘贴板",
        ["Tip_DeviceRedirect"] = "仅影响本次连接；三项各自独立，未改动的那一项仍按资产的连接方案取值（剪贴板默认启用）",
        ["Setting_ColorDepth"] = "颜色:",
        ["Setting_Resolution"] = "分辨率设置",
        ["Setting_ProbeInterval"] = "定时探测间隔(分)",
        ["Setting_RememberPassword"] = "本机记住登录密码",
        ["Setting_RememberPasswordHint"] = "开启后，SSH 与 RDP 的密码使用 Windows DPAPI 加密后仅保存在本机；下次连接自动登录，无需再次输入。关闭时会立即清除所有已保存的密码。",
        ["Setting_Bit8"] = "8位",
        ["Setting_Bit15"] = "15位",
        ["Setting_Bit16"] = "16位",
        ["Setting_Bit24"] = "24位",
        ["Setting_Bit32"] = "32位(最高)",
        ["Btn_Screen14"] = "1/4屏",
        ["Btn_Screen16"] = "1/6屏",
        ["Btn_Screen19"] = "1/9屏",
        ["Btn_SplitH"] = "左右一分二",
        ["Btn_SplitV"] = "上下一分二",

        // 详情面板字段
        ["Insp_Name"] = "服务器名称",
        ["Insp_Port"] = "端口",
        ["Insp_ExpiryWarn"] = "到期预警状态: ",
        ["Insp_LastProbe"] = "最后检测耗时: ",
        ["Insp_Placeholder"] = "请选择一台服务器查看或修改详情",
        ["Insp_Expired"] = "已过期 {0} 天",
        ["Insp_DaysLeft"] = "剩余 {0} 天 (即将到期)",
        ["Insp_Normal"] = "正常 (剩余 {0} 天)",
        ["Insp_NotSet"] = "未设置到期日 (正常)",
        ["Insp_NotProbed"] = "尚未进行网络探测",
        ["Insp_Online"] = "在线可达",
        ["Insp_Offline"] = "离线超时",
        ["Insp_ProbeAt"] = "{0} (检测时间: {1})",

        // 诊断卡片
        ["Diag_Title"] = "⚠️ 连接/探测异常诊断",
        ["Diag_Reason"] = "无法建立连接",
        ["Diag_Advice"] = "检查建议:",
        ["Diag_AdviceDetail"] = "• 请确认服务器网络与端口（22/3389）是否开放\n• 请检查防火墙或云端安全组策略\n• 请核对账号密码或 SSH 私钥是否正确",
        ["Diag_Retry"] = "重试连接",

        // 卡片
        ["Card_Owner"] = "负责人: ",
        ["Card_Expires"] = "到期日: ",

        // 会话工作台
        ["Layout_Label"] = "分屏布局:",
        ["Layout_Single"] = "单会话",
        ["Layout_1x2"] = "左右分屏 (1x2)",
        ["Layout_2x1"] = "上下分屏 (2x1)",
        ["Layout_2x2"] = "四宫格 (2x2)",
        ["Layout_2x3"] = "六宫格 (2x3)",
        ["Layout_3x3"] = "九宫格 (3x3)",
        ["Layout_Fit"] = "适应窗口",
        ["Layout_Fullscreen"] = "全屏模式",
        ["Layout_ExitFullscreen"] = "退出全屏",
        ["Layout_Config"] = "连接设置",
        ["Layout_Disconnect"] = "断开当前",
        ["Layout_CloseAll"] = "关闭全部",
        ["Layout_EmptyCell"] = "+ 点击添加会话或双击资产快速置入",
        ["Layout_RdpTitle"] = "🖥 独立远程桌面会话",
        ["Layout_RdpTarget"] = "目标: {0}:{1} (用户: {2})",
        ["Layout_LaunchMstsc"] = "再次启动远程桌面 (mstsc)",
        ["Layout_RdpLaunched"] = "已自动调用系统远程桌面 (mstsc)，可在任务栏切换。",
        ["Layout_AddSessionHint"] = "请在资产列表中双击资产或点击「连接」将其置入工作台。",
        ["Layout_NoActiveSession"] = "当前没有选中的活动会话。",

        // 设置页
        ["Settings_Title"] = "设置",
        ["Settings_Theme"] = "外观主题",
        ["Settings_ThemeHint"] = "亮色 / 暗色即时切换，所有窗口同步生效。",
        ["Settings_Language"] = "界面语言",
        ["Settings_LanguageHint"] = "简体中文 / English 即时切换。",
        ["Settings_Probe"] = "定时可达性探测",
        ["Settings_ProbeHint"] = "设置大于 0 的分钟数后，程序会在后台定时探测所有资产可达性。",
        ["Settings_Data"] = "本地数据",
        ["Settings_DataHint"] = "资产清单仅保存在本机，不上传任何服务器。",
        ["Settings_OpenDataDir"] = "打开数据目录",
        ["Settings_AboutTitle"] = "关于 RemotePilot",
        ["Settings_AboutText"] = "完全免费、零账号、离线可用的设备远程运维工具。支持 RDP 一键直连、内置 SSH 终端、分组/负责人/备注/到期提醒。",
        ["Settings_Portal"] = "访问 RemotePilot 官网",

        // SSH 凭据
        ["Cred_Title"] = "SSH 登录凭据",
        ["Cred_ReenterTitle"] = "认证失败，请重新输入凭据",
        ["Status_CredReenter"] = "凭据已更新，正在重新连接…",
        ["Cred_Subtitle"] = "密码只保存在本次运行的内存中，绝不落盘。",
        ["Cred_Username"] = "用户名",
        ["Cred_Password"] = "密码",
        ["Cred_KeyPath"] = "私钥文件 (可选)",
        ["Cred_Passphrase"] = "私钥口令 (可选)",
        ["Cred_Connect"] = "连接",
        ["Cred_DialogHint"] = "该服务器尚未保存可用凭据，请输入本次连接使用的凭据。",
        ["Cred_UsernameLabel"] = "用户名:",
        ["Cred_PasswordLabel"] = "密码:",
        ["Cred_KeyPathLabel"] = "私钥路径:",
        ["Cred_RememberLocal"] = "本机记住密码（Windows DPAPI 加密）",
        ["Msg_SshUserRequired"] = "请输入 SSH 用户名。",
        ["Msg_SshCredRequired"] = "请输入密码，或选择一个私钥文件。",

        // 状态栏与提示
        ["Status_Ready"] = "就绪 - RemotePilot",
        ["Status_RememberPasswordOn"] = "已开启本机记住密码（DPAPI 加密保存）",
        ["Status_RememberPasswordOff"] = "已关闭本机记住密码，已清除本地保存的密码",
        ["Status_Total"] = "共 {0} 台服务器，当前显示 {1} 台",
        ["Msg_Tip"] = "提示",
        ["Msg_SelectAsset"] = "请先选择一台服务器资产。",
        ["Msg_SelectToDelete"] = "请先勾选需要删除的服务器。",
        ["Msg_ConfirmDelete"] = "确认删除服务器 \"{0}\"（{1}）吗？",
        ["Msg_DeleteTitle"] = "确认删除",
        ["Msg_ConfirmBatchDelete"] = "确认删除选中的 {0} 台服务器吗？\n\n{1}",
        ["Msg_BatchDeleteTitle"] = "批量删除确认",
        ["Msg_Saved"] = "资产属性已成功保存。",
        ["Msg_ExportedEmpty"] = "没有可导出的服务器资产。",

        // 资产编辑对话框
        ["Dlg_Title"] = "服务器配置",
        ["Dlg_Header"] = "服务器配置信息",
        ["Fld_Name"] = "服务器名称:",
        ["Fld_Group"] = "分组:",
        ["Fld_Host"] = "IP与端口:",
        ["Fld_Port"] = "端口:",
        ["Fld_Protocol"] = "协议:",
        ["Fld_Template"] = "连接模板:",
        ["Fld_TemplateNone"] = "（无模板）",
        ["Fld_Username"] = "账号:",
        ["Fld_Password"] = "密码:",
        ["Fld_RememberPassword"] = "本机记住密码（DPAPI 加密，仅存本机）",
        ["Fld_SshPort"] = "SSH端口:",
        ["Fld_KeyPath"] = "SSH私钥路径:",
        ["Fld_Passphrase"] = "私钥口令:",
        ["Fld_JumpHost"] = "跳板机（SSH中转）:",
        ["Fld_JumpHostNone"] = "（不使用跳板机）",
        ["Msg_JumpCredMissing"] = "跳板机缺少可用凭据，请先为跳板机配置密码（并开启本机记住密码）或私钥。",
        ["Term_JumpConnecting"] = "正在经跳板机 {0} 连接 {1}:{2} …",
        ["Fld_Owner"] = "负责人:",
        ["Fld_ExpiresOn"] = "到期时间:",
        ["Fld_Note1"] = "备注1:",
        ["Fld_Note2"] = "备注2:",
        ["Fld_Note3"] = "备注3:",
        ["Fld_Note4"] = "备注4:",
        ["Dlg_RdpRedirect"] = "RDP 设备重定向与参数",
        ["Dlg_DriveMap"] = "硬盘映射(本地硬盘)",
        ["Dlg_Audio"] = "加载远程声音",
        ["Dlg_Clipboard"] = "使用粘贴板",
        ["Msg_HostRequired"] = "服务器 IP 地址不能为空。",
        ["Msg_PortInvalid"] = "请输入有效的端口号 (1-65535)。",
        ["Dlg_BrowseKey"] = "选择 SSH 私钥文件",
        ["Dlg_AllFilesFilter"] = "所有文件 (*.*)|*.*",

        // 连接方案抽屉
        ["Drawer_Title"] = "连接方案配置抽屉",
        ["Drawer_Sec1"] = "1. 概览与继承状态",
        ["Drawer_AssetName"] = "资产名称: ",
        ["Drawer_Target"] = "目标地址: ",
        ["Drawer_ProtocolType"] = "协议类型: ",
        ["Drawer_ApplyTemplate"] = "应用模板 (下拉切换):",
        ["Drawer_ParamSource"] = "参数来源: ",
        ["Drawer_SourceCustom"] = "自定义覆盖",
        ["Drawer_SourceInherit"] = "继承自模板: {0}",
        ["Drawer_SourceFullCustom"] = "完全自定义",
        ["Drawer_SourceTemplateOnly"] = "模板独立配置",
        ["Drawer_TemplatePrefix"] = "[模板] {0}",
        ["Drawer_PortLabel"] = "端口: {0}",
        ["Drawer_Sec2"] = "2. 显示设置",
        ["Drawer_ResolutionMode"] = "分辨率模式",
        ["Drawer_ResFit"] = "适应容器 (默认)",
        ["Drawer_Res1080p"] = "标准 1080p (1920x1080)",
        ["Drawer_Res2k"] = "标准 2k (2560x1440)",
        ["Drawer_Res4k"] = "标准 4k (3840x2160)",
        ["Drawer_ResCustom"] = "自定义宽高",
        ["Drawer_Width"] = "宽 (Width)",
        ["Drawer_Height"] = "高 (Height)",
        ["Drawer_ColorDepth"] = "颜色深度",
        ["Drawer_Depth8"] = "8位 (256色)",
        ["Drawer_Depth15"] = "15位 (高彩)",
        ["Drawer_Depth16"] = "16位 (高彩)",
        ["Drawer_Depth24"] = "24位 (真彩)",
        ["Drawer_Depth32"] = "32位 (最高)",
        ["Drawer_NegotiationWarn"] = "⚠️ 部分协商参数（如颜色深度与分辨率）需重新连接生效。",
        ["Drawer_Sec3"] = "3. 本地资源与重定向",
        ["Drawer_ChkDrive"] = "本地硬盘映射 (加载本地磁盘至远程)",
        ["Drawer_ChkAudio"] = "声音重定向 (本地播放 / 远程静音)",
        ["Drawer_ChkClipboard"] = "使用粘贴板（允许双向同步）",
        ["Drawer_ComplianceHint"] = "💡 注意：开启剪贴板或硬盘映射可能涉及企业安全策略，请遵守合规规范。",
        ["Drawer_Sec4"] = "4. 协议特有配置",
        ["Drawer_Gateway"] = "RDP 网关服务器 (Gateway)",
        ["Drawer_GatewayHint"] = "若有域控或堡垒机网关，请在此输入网关地址",
        ["Drawer_SshPort"] = "SSH 端口",
        ["Drawer_SshUser"] = "登录用户名",
        ["Drawer_KeyPath"] = "私钥文件路径 (Key File)",
        ["Drawer_SaveAsTemplate"] = "另存为新模板",
        ["Drawer_SaveApply"] = "保存并应用",
        ["Drawer_TemplateNameSuffix"] = "{0} 模板",
        ["Drawer_NewTemplate"] = "新建连接模板",
        ["Drawer_TemplateSaved"] = "成功另存为新连接模板！",
        ["Drawer_KeyFilter"] = "密钥文件 (*.pem;*.key;*.id_rsa;*.*)|*.pem;*.key;*.id_rsa;*.*|所有文件 (*.*)|*.*",

        // 主窗口补充键
        ["App_Title"] = "RemotePilot 远程运维管理中心",
        ["Theme_Toggle"] = "🌓 主题",
        ["Lang_Toggle"] = "中 / EN",
        ["Btn_ExpiryBanner"] = "⚠️ 临期资产提示",
        ["Tip_Favorite"] = "收藏 / 取消收藏",
        ["Tip_MaximizeRestore"] = "最大化/还原 (双击标题栏也可切换)",
        ["Tip_CloseSession"] = "关闭会话",
        ["Panel_Overview"] = "资产详细概览",
        ["Btn_Collapse"] = "◀ 收起",
        ["Tag_Total"] = "总数({0})",
        ["Group_Default"] = "默认分组",
        ["Status_Online"] = "在线",
        ["Status_Offline"] = "离线",
        ["Status_Probing"] = "检测中...",
        ["Status_NotProbed"] = "未检测",
        ["Status_Connecting"] = "连接中...",
        ["Status_Disconnected"] = "已断开",
        ["Status_Connected"] = "已连接",
        ["Status_Failed"] = "失败",
        ["Term_Disconnected"] = "连接已断开",
        ["Term_Connecting"] = "正在连接 {0}:{1} …",
        ["Term_ConnectFailed"] = "连接失败: {0}",
        ["Term_StatusFailed"] = "连接失败",
        ["Term_SchemeDark"] = "配色: 深色",
        ["Term_SchemeGreen"] = "配色: 绿色",
        ["Term_SchemeLight"] = "配色: 浅色",
        ["Term_Copy"] = "复制",
        ["Term_Paste"] = "粘贴",
        ["Term_SelectAll"] = "全选",
        ["Term_Clear"] = "清屏",
        ["Msg_FitWindowDone"] = "当前会话视图已自适应网格容器窗口。",
        ["Msg_RdpLaunchFailed"] = "启动 RDP 失败: {0}",
        ["Status_ExpiryBanner"] = "⚠️ 有 {0} 台服务器即将到期或已过期，点击快速筛选",
        ["Status_DrawerApplied"] = "连接方案已保存并应用到当前资产。",
        ["Status_TemplateSaved"] = "新连接模板 [{0}] 保存成功。",
        ["Status_ExitFullscreen"] = "退出全屏模式",
        ["Status_EnterFullscreen"] = "进入全屏工作台模式",
        ["Status_FavoritesView"] = "收藏夹：{0} 台服务器",
        ["Status_RecentView"] = "最近使用：{0} 台服务器",
        ["Status_GroupsView"] = "分组视图：{0} 个分组，共 {1} 台",
        ["Status_ExpiryFilter"] = "已一键筛选临期/过期资产，共 {0} 台",
        ["Status_Retrying"] = "正在重试连接服务器: {0} ({1})...",
        ["Status_Favorited"] = "已收藏 {0}",
        ["Status_Unfavorited"] = "已取消收藏 {0}",
        ["Status_ProbingAll"] = "正在批量探测服务器网络可达性与延迟...",
        ["Status_ProbeDone"] = "网络探测完成。",
        ["Status_ProbeScheduled"] = "定时探测完成于 {0}",
        ["Status_SshOpened"] = "已在会话工作台打开 SSH 终端: {0}",
        ["Status_RdpOpened"] = "已在会话工作台打开远程桌面: {0}",
        ["Status_ConnectBackground"] = "已发起连接：{0}，可在「会话工作台」查看",
        ["Status_ClipboardSecurityHint"] = "Windows 还会再弹一次安全对话框，请在弹窗中勾选“剪贴板”",
        ["Status_ConnectFailed"] = "连接失败: {0}",
        ["Msg_SelectAssetForDrawer"] = "请先选择一台服务器资产，以便为其配置连接方案。",
        ["Msg_SelectAssetForRetry"] = "请先选择一台需要重试连接的服务器资产。",
        ["Msg_SelectToConnect"] = "请选择要连接的服务器。",
        ["Msg_ErrorTitle"] = "错误",
        ["Msg_ImportDoneTitle"] = "导入完成",
        ["Msg_ImportDone"] = "成功导入 {0} 台服务器。",
        ["Msg_ImportSkipped"] = "有 {0} 条数据解析失败并跳过。",
        ["Msg_ImportFailed"] = "导入失败: {0}",
        ["Msg_ExportDoneTitle"] = "导出成功",
        ["Msg_ExportDone"] = "成功导出 {0} 条记录至:\n{1}",
        ["Msg_ExportFailed"] = "导出失败: {0}",
        ["Msg_SettingsSaved"] = "设置与资产列表已保存。",
        ["Msg_SyncDoneTitle"] = "导入完成",
        ["Msg_SyncDone"] = "已从接口获取并导入 {0} 台服务器！",
        ["Msg_SshConnectFailed"] = "SSH 连接失败: {0}",
        ["Msg_RdpConnectFailed"] = "远程桌面连接失败: {0}",
        ["Msg_BrowserFailed"] = "打开浏览器失败: {0}",
        ["Dlg_ImportTitle"] = "导入服务器资产（ISS7 导出 / 标准格式）",
        ["Dlg_ImportFilter"] = "ISS7 导出 (*.xls;*.xlsx)|*.xls;*.xlsx|标准格式 CSV (*.csv)|*.csv|所有文件 (*.*)|*.*",
        ["Msg_ImportFormatHint"] = "未识别到可导入的数据。标准格式表头：分组,服务器名称,服务器IP和端口,账号,密码,到期时间,负责人,其他备注1,其他备注2,其他备注3。",
        ["Dlg_ExportTitle"] = "导出服务器资产 CSV",
        ["Dlg_ExportFilter"] = "CSV 文件 (*.csv)|*.csv",
        ["Csv_ErrorIpPort"] = "服务器IP和端口缺失或无效",

        // 导出字段选择
        ["Dlg_ExportFieldsTitle"] = "选择导出字段",
        ["ExpFld_Hint"] = "请选择需要导出的字段。密码默认不导出；勾选密码后将以明文写入文件，存在泄露风险。",
        ["ExpFld_PasswordWarn"] = "⚠️ 勾选“密码”或“密钥路径”会把服务器凭据 / 私钥路径以明文写入 CSV，请务必妥善保管导出文件。",
        ["ExpFld_RiskTitle"] = "明文密码泄露风险",
        ["ExpFld_RiskBody"] = "您勾选了导出密码列。密码会以明文形式写入 CSV 文件，任何拿到该文件的人都能看到服务器凭据，可能造成安全风险。\n\n确定要明文导出密码吗？",
        ["Col_SshKeyPath"] = "密钥路径",
        ["ExpFld_KeyPathRiskTitle"] = "密钥路径泄露风险",
        ["ExpFld_KeyPathRiskBody"] = "您勾选了导出密钥路径列。密钥文件路径会以明文写入 CSV 文件，可能暴露服务器登录方式，存在安全风险。\n\n确定要导出密钥路径吗？",
        ["Msg_ExportDoneNoPwd"] = "凭据不在本次导出范围内，请另行备份。",

        // 接口导入对话框（用户自定义接口，无需登录任何平台）
        ["Sync_Title"] = "从接口导入资产 - RemotePilot",
        ["Sync_Header"] = "从自定义接口导入资产",
        ["Sync_Desc"] = "填写你自己的资产接口地址，直接 GET 拉取即可。返回 JSON 数组或 CSV 文本均可，无需登录任何平台。",
        ["Sync_BaseUrl"] = "接口地址:",
        ["Sync_Token"] = "令牌(可选):",
        ["Sync_Preview"] = "资产预览:",
        ["Sync_ColImport"] = "导入",
        ["Sync_ColName"] = "名称",
        ["Sync_ColHost"] = "主机",
        ["Sync_BtnSync"] = "获取资产",
        ["Sync_BtnConfirm"] = "确认导入勾选项",
        ["Sync_MsgUrlRequired"] = "请输入接口地址。",
        ["Sync_Connecting"] = "正在请求接口...",
        ["Sync_Fetching"] = "正在解析接口返回数据...",
        ["Sync_Fetched"] = "已获取到 {0} 个资产（去重后 {1} 个），请勾选并确认导入。",
        ["Sync_Imported"] = "成功导入 {0} 个资产！",
        ["Sync_Error"] = "导入出错: {0}",

        // 全选/反选与选中计数
        ["Btn_InvertSelect"] = "反选",
        ["Status_SelectedCount"] = "已选 {0} 台",
        ["Btn_BatchProbe"] = "批量检测",
        ["Tip_StatusNotProbed"] = "尚未进行连通性检测。点击『批量检测』可对所有资产探测连通性。",
        ["Tip_ExpiryBannerItem"] = "{0} ({1}) 到期日 {2}",
        ["Msg_DeleteListMore"] = "… 其余 {0} 台未列出",

        // 模板管理
        ["TmplMgmt_BtnDelete"] = "删除",
        ["TmplMgmt_WinTitle"] = "连接方案模板",
        ["TmplMgmt_Header"] = "连接方案模板管理",
        ["TmplMgmt_TemplateList"] = "模板列表",
        ["TmplMgmt_SortByName"] = "按名称排序",
        ["TmplMgmt_SortByUsage"] = "按使用数量排序",
        ["TmplMgmt_DetailTitle"] = "模板详情",
        ["TmplMgmt_RdpSettings"] = "RDP 设置",
        ["TmplMgmt_Resolution"] = "分辨率",
        ["TmplMgmt_ColorDepth"] = "颜色深度",
        ["TmplMgmt_Gateway"] = "网关",
        ["TmplMgmt_RedirectSettings"] = "设备重定向",
        ["TmplMgmt_NoValue"] = "-",
        ["TmplMgmt_Bit"] = "位",
        ["TmplMgmt_NoGateway"] = "无网关",
        ["TmplMgmt_Copy"] = "副本",
        ["TmplMgmt_DeleteWarning"] = "该模板被 {0} 个资产使用，删除后这些资产将使用顶栏默认值。\n\n确定要删除吗？",
        ["TmplMgmt_DeleteTitle"] = "确认删除",
        ["TmplMgmt_DeleteConfirm"] = "确定要删除该模板吗？",
        ["Btn_New"] = "新建",
        ["Btn_Copy"] = "复制",
        ["Btn_Close"] = "关闭",

        // VNC（外装客户端）
        ["Status_VncOpened"] = "已在会话工作台挂载 VNC 客户端: {0}",
        ["Msg_VncConnectFailed"] = "VNC 连接失败: {0}",
        ["Msg_VncLaunchFailed"] = "启动 VNC 客户端失败: {0}",
        ["Msg_VncViewerMissing"] = "未找到 VNC 客户端程序。本应用不内置 VNC 客户端，请先安装 TightVNC / UltraVNC / RealVNC / TigerVNC 之一，再到左侧「帮助手册」中指定客户端程序路径。",
        ["Layout_VncTitle"] = "🖥 外装 VNC 会话",
        ["Layout_VncTarget"] = "目标 {0}:{1} · 客户端 {2}",
        ["Layout_VncViewerMissing"] = "未找到 VNC 客户端，请到「帮助手册」配置客户端路径",
        ["Layout_LaunchVnc"] = "启动 VNC 客户端",

        // 帮助手册
        ["Help_Title"] = "帮助手册",
        ["Help_Subtitle"] = "这里汇总各协议的疑点与注意事项，并提供外装 VNC 客户端（程序路径 + 启动参数）的配置入口。",
        ["Help_VncConfigTitle"] = "VNC 客户端配置",
        ["Help_ViewerLabel"] = "VNC 客户端程序",
        ["Help_ArgsLabel"] = "启动参数模板",
        ["Help_ArgsHint"] = "可用占位符：{host} 主机、{port} 端口、{username} 用户名、{password} 密码。未保存密码时，含 {password} 的整段参数会被自动省略；像 -password 空格 密码 这种分离写法，会连同 -password 开关一起去掉。",
        ["Help_BtnBrowse"] = "浏览...",
        ["Help_BtnDetect"] = "自动探测",
        ["Help_BtnResetArgs"] = "恢复默认参数",
        ["Help_BtnSave"] = "保存设置",
        ["Help_ViewerFilter"] = "VNC 客户端 (*.exe)|*.exe|所有文件 (*.*)|*.*",
        ["Help_ViewerDetected"] = "已定位到：{0}（{1}）",
        ["Help_ViewerNotDetected"] = "未探测到本机已安装的 VNC 客户端。请手动指定 vncviewer.exe / tvnviewer.exe 的路径。",
        ["Help_ViewerNotExists"] = "指定的文件不存在，请重新选择。",
        ["Help_Saved"] = "VNC 客户端设置已保存。",
        ["Help_VncMoved"] = "VNC 客户端的路径与启动参数已迁移到左侧导航的「设置」页。",

        // 设置
        ["Settings_Subtitle"] = "配置外装 VNC 客户端与 SSH 终端外观。",
        ["Settings_VncSection"] = "VNC 客户端",
        ["Settings_SshSection"] = "SSH 终端外观",
        ["Settings_SshFontSize"] = "字体大小",
        ["Settings_SshScheme"] = "配色方案",
        ["Settings_SshStandalone"] = "SSH 以独立窗口打开",
        ["Settings_BtnSave"] = "保存设置",
        ["Settings_Saved"] = "设置已保存。",

        ["Help_SecVncUsage_Title"] = "VNC 使用说明",
        ["Help_SecVncUsage_Body"] = "RemotePilot 不内置 VNC 客户端：连接时只是把本机已安装的 VNC viewer 拉起来，远程画面由它自己的窗口承载，所以工作台里显示的是占位页而不是远程画面。\n\n· 默认端口 5900，对应服务端的 display :0；服务端开在 display :1 时端口是 5901，以此类推。\n· 参数模板中的 {host} {port} {username} {password} 会被替换成资产里的实际值。各客户端写法不同：TightVNC 用 主机::端口，UltraVNC 用 -connect 主机::端口。\n· 密码：勾选「记住密码」后，密码会作为启动参数传给客户端；这会以明文出现在进程命令行中，同机其他进程和安全软件都可能看到，请自行评估风险。不勾选则客户端会自己弹窗要求输入。\n· 新建资产时把协议选为 vnc，端口填 5900（或服务端实际端口）。",

        ["Help_SecVncFaq_Title"] = "VNC 常见疑点",
        ["Help_SecVncFaq_Body"] = "问：点连接后提示「未找到 VNC 客户端程序」？\n答：本机没装 VNC viewer，或装在非常规位置。装一个 TightVNC / UltraVNC / RealVNC / TigerVNC 后到左侧「设置」页点「自动探测」；仍不行就手动指定路径：RealVNC、TigerVNC、UltraVNC 用 vncviewer.exe，TightVNC 用 tvnviewer.exe。\n\n问：客户端弹出来了，但还是要我输密码？\n答：两种情况。一是资产没有勾选「记住密码」，应用拿不到明文密码，自然没法带参数；二是该客户端不支持命令行密码（RealVNC、TigerVNC 属于这类），只能在客户端窗口里输入。\n\n问：改了参数模板后连不上了？\n答：模板写法必须匹配你用的客户端。点「恢复默认参数」回到该客户端家族的默认写法，再按需微调。\n\n问：点了连接却什么都没弹？\n答：先看左侧「设置」页的客户端路径校验结果是否正常，再检查模板里的地址分隔符是不是客户端不认的写法（:: 与 : 有区别）。",

        ["Help_SecRdp_Title"] = "RDP 常见疑点",
        ["Help_SecRdp_Body"] = "问：明明勾了磁盘映射、剪贴板、声音，mstsc 弹出的窗口里还是没勾上？\n答：这是 Windows 的行为，不是应用没生效。在较新的 Windows 安全更新之后，未签名的 .rdp 文件会弹出「注意：未知远程连接」对话框，其中所有重定向默认关闭，必须每次手动勾选；.rdp 文件本身无法预先把它勾好。可选应对：每次手动勾选（最省事）、用 rdpsign 给 .rdp 签名（需要代码签名证书）、或按组策略把 RedirectionWarningDialogVersion 设为 1（会削弱这层防护，需管理员权限）。\n\n问：RDP 窗口标题为什么显示成「名称 (IP)」？\n答：mstsc 的标题取自 .rdp 文件名。应用会把临时文件命名为「资产名称 (主机地址).rdp」，所以标题就是「名称 (IP)」；资产名称为空时只显示 IP。\n\n问：连接失败了，应用里看不到失败原因？\n答：mstsc 是独立进程，应用无法探测它内部的认证失败，具体原因以 mstsc 自己的弹窗为准。",

        ["Help_SecRedirection_Title"] = "重定向为什么没生效",
        ["Help_SecRedirection_Body"] = "问：明明在顶栏或连接方案里勾了剪贴板 / 驱动器，mstsc 窗口里还是没生效？\n答：这是 Windows 的安全限制，不是应用没写入。自 Windows 2026 年 4 月安全更新（CVE-2026-26151）起，远程桌面在打开任何 .rdp 文件前都会弹出安全对话框；对话框内所有重定向（剪贴板、驱动器、打印机、麦克风、摄像头等）默认全部关闭，必须每次连接手动勾选。未签名的 .rdp 没有「不再询问」选项。\n\n问：那应用做了什么？\n答：本应用会按你在顶栏 / 连接方案里的勾选，把 redirectclipboard:i:1 等键写入临时 .rdp；但 Windows 仍会要求你在弹出的安全对话框中再确认一次，这一步应用无法代勾。\n\n问：具体怎么操作？\n答：连接时在弹出的安全对话框中勾选「剪贴板」（以及其它需要的项），然后继续连接即可。\n\n问：和「记住密码」有关系吗？\n答：无关。「记住密码」只决定是否把凭据写入 .rdp（password 51:b），不影响重定向开关是否出现在对话框中。\n\n问：有没有办法一次配好、以后不用每次勾？\n答：有，但属于组织级操作：用 rdpsign 给 .rdp 做代码签名，或由 IT 通过组策略信任指定发布者证书的指纹（SHA-2）。签名 / 信任之后 Windows 不再拦截，重定向勾选才会直接生效。",

        ["Help_SecSsh_Title"] = "SSH 常见疑点",
        ["Help_SecSsh_Body"] = "问：SSH 密码错了会怎样？\n答：应用会提示重新输入凭据并重连，同一资产只提示一次，避免凭据仍然错误时反复弹窗。\n\n问：跳板机需要什么条件？\n答：跳板机资产自己要有可用的密码或私钥；两者都缺失时，连接在开始前就会直接报错。\n\n问：密钥和密码能同时保留吗？\n答：可以。连接时优先使用存在的私钥文件，私钥不可用时才回退到密码。",

        ["Help_SecGeneral_Title"] = "通用说明",
        ["Help_SecGeneral_Body"] = "· 资产与配置保存在 %LOCALAPPDATA%\\FastRdp\\RemoteOps\\assets.json。\n· 密码使用 Windows DPAPI 按「当前用户」加密后落盘；换 Windows 用户或换电脑后密文无法解密，需要重新输入。\n· RDP 每次连接会生成一个临时 .rdp 文件，位于 %TEMP%\\RemotePilot\\{随机目录}\\ 下，其中包含 DPAPI 加密后的密码字段，可以安全删除。\n· 未勾选「记住密码」时，应用不会替任何协议保存凭据。"
    };

    private static readonly Dictionary<string, string> _enStrings = new()
    {
        // Navigation
        ["Nav_Assets"] = "Asset Hub",
        ["Nav_Sessions"] = "Session Hub",
        ["Nav_Favorites"] = "Favorites",
        ["Nav_Recent"] = "Recent",
        ["Nav_Groups"] = "Groups",
        ["Nav_Help"] = "Help Manual",
        ["Nav_Templates"] = "Connection Plan Templates",
        ["Nav_Settings"] = "Settings",
        ["Nav_Feedback"] = "✉ Send Feedback",
        ["Btn_ToggleLeftNav"] = "◀",
        ["Btn_ToggleLeftNavExpand"] = "▶",
        ["Tip_CollapseLeftNav"] = "Collapse navigation",
        ["Tip_ExpandLeftNav"] = "Expand navigation",
        ["Msg_FeedbackTitle"] = "RemotePilot Feedback",
        ["Msg_FeedbackFallback"] = "Thank you for helping make RemotePilot better — every piece of feedback is read carefully.\n\nNo mail client was detected on this PC, so the compose window could not be opened automatically.\nPlease email us at:\n\nremotemate@protonmail.com\n\nOpen source: https://github.com/OpenNodeSaas/RemotePilot\nFeel free to open an Issue or join the discussion on GitHub.\n\nTip: press Ctrl+C in this window to copy the text, then paste it into any webmail.",

        // Top command bar & actions
        ["Search_Placeholder"] = "Search name / IP / group / owner / note",
        ["Btn_Search"] = "Search",
        ["Btn_AddServer"] = "Add Asset",
        ["Btn_Import"] = "Import",
        ["Btn_ExportSelected"] = "Export",
        ["Btn_Delete"] = "Delete",
        ["Btn_Open"] = "Connect",
        ["Btn_Connect"] = "Connect",
        ["Btn_ShowAll"] = "Show All",
        ["Btn_SelectAll"] = "Toggle All",
        ["Btn_Probe"] = "Probe Ping",
        ["Btn_SyncCloud"] = "Import via API",
        ["Btn_SaveSettings"] = "Save Config",
        ["Btn_ResetResolution"] = "Default Res",
        ["Btn_Edit"] = "Edit",
        ["Btn_Remove"] = "Delete",
        ["Btn_QuickConnect"] = "Quick Connect",
        ["Btn_ViewTable"] = "Table",
        ["Btn_ViewGrid"] = "Cards",
        ["Btn_Save"] = "OK",
        ["Btn_Cancel"] = "Cancel",
        ["Btn_Browse"] = "Browse...",

        // Themes & Languages
        ["Theme_Light"] = "Light Theme",
        ["Theme_Dark"] = "Dark Theme",
        ["Lang_Zh"] = "简体中文",
        ["Lang_En"] = "English",

        // Workspaces & Panels
        ["Panel_Details"] = "Asset Details & Quick Config",
        ["Panel_Collapse"] = "Collapse",
        ["Panel_Expand"] = "Details",

        // Table headers
        ["Col_Action"] = "Action",
        ["Col_Favorite"] = "Favorite",
        ["Col_Select"] = "Select",
        ["Col_Group"] = "Group",
        ["Col_Template"] = "Template",
        ["Col_Owner"] = "Owner",
        ["Col_ServerName"] = "Server / IP:Port",
        ["Col_IpPort"] = "IP & Port",
        ["Col_Name"] = "Name",
        ["Col_Protocol"] = "Protocol",
        ["Col_Username"] = "User",
        ["Col_Status"] = "Status",
        ["Col_ExpiresOn"] = "Expires On",
        ["Col_Note1"] = "Note 1",
        ["Col_Note2"] = "Note 2",
        ["Col_Note3"] = "Note 3",
        ["Col_Note4"] = "Note 4",

        // Column settings
        ["Btn_ColumnSettings"] = "Columns",
        ["ColSettings_Title"] = "Column Settings",
        ["ColSettings_Hint"] = "\"Name\" and \"Action\" are required and cannot be hidden.",
        ["ColSettings_FontSize"] = "Font size:",
        ["ColSettings_MoveUp"] = "Move up",
        ["ColSettings_MoveDown"] = "Move down",
        ["FontSize_Small"] = "Small",
        ["FontSize_Normal"] = "Normal",
        ["FontSize_Large"] = "Large",
        ["FontSize_XLarge"] = "Extra large",

        // Config options
        ["Setting_WindowMode"] = "Window Mode:",
        ["Setting_ModeNormal"] = "Normal",
        ["Setting_ModeFullscreen"] = "Fullscreen",
        ["Setting_ModeSmall"] = "Small",
        ["Setting_ModeTab"] = "Tab Window",
        ["Setting_DeviceControl"] = "Device Redirection:",
        ["Setting_DriveMap"] = "Drive Map",
        ["Setting_Audio"] = "Remote Audio",
        ["Setting_Clipboard"] = "Use Clipboard",
        ["Tip_DeviceRedirect"] = "Applies to this connection only; the three items are independent, and any item left unchanged still follows the asset's connection profile (clipboard is on by default)",
        ["Setting_ColorDepth"] = "Color:",
        ["Setting_Resolution"] = "Resolution",
        ["Setting_ProbeInterval"] = "Auto Ping Interval (min)",
        ["Setting_RememberPassword"] = "Remember passwords on this PC",
        ["Setting_RememberPasswordHint"] = "When enabled, SSH and RDP passwords are encrypted with Windows DPAPI and stored on this PC only; the next connection signs in automatically. Disabling clears all saved passwords immediately.",
        ["Setting_Bit8"] = "8-bit",
        ["Setting_Bit15"] = "15-bit",
        ["Setting_Bit16"] = "16-bit",
        ["Setting_Bit24"] = "24-bit",
        ["Setting_Bit32"] = "32-bit (Best)",
        ["Btn_Screen14"] = "1/4 Screen",
        ["Btn_Screen16"] = "1/6 Screen",
        ["Btn_Screen19"] = "1/9 Screen",
        ["Btn_SplitH"] = "Split Left/Right",
        ["Btn_SplitV"] = "Split Top/Bottom",

        // Inspector fields
        ["Insp_Name"] = "Server Name",
        ["Insp_Port"] = "Port",
        ["Insp_ExpiryWarn"] = "Expiry Status: ",
        ["Insp_LastProbe"] = "Last Probe: ",
        ["Insp_Placeholder"] = "Select a server to view or edit its details",
        ["Insp_Expired"] = "Expired {0} day(s) ago",
        ["Insp_DaysLeft"] = "{0} day(s) left (expiring soon)",
        ["Insp_Normal"] = "Normal ({0} day(s) left)",
        ["Insp_NotSet"] = "No expiry date (normal)",
        ["Insp_NotProbed"] = "Not probed yet",
        ["Insp_Online"] = "Reachable",
        ["Insp_Offline"] = "Offline / timeout",
        ["Insp_ProbeAt"] = "{0} (probed at {1})",

        // Diagnostics
        ["Diag_Title"] = "⚠️ Connection / Probe Diagnostics",
        ["Diag_Reason"] = "Unable to establish connection",
        ["Diag_Advice"] = "Suggestions:",
        ["Diag_AdviceDetail"] = "• Verify server network and ports (22/3389)\n• Check firewall or cloud security group rules\n• Verify credentials or SSH private key",
        ["Diag_Retry"] = "Retry",

        // Cards
        ["Card_Owner"] = "Owner: ",
        ["Card_Expires"] = "Expires: ",

        // Session workspace
        ["Layout_Label"] = "Layout:",
        ["Layout_Single"] = "Single",
        ["Layout_1x2"] = "Split 1x2",
        ["Layout_2x1"] = "Split 2x1",
        ["Layout_2x2"] = "Grid 2x2",
        ["Layout_2x3"] = "Grid 2x3",
        ["Layout_3x3"] = "Grid 3x3",
        ["Layout_Fit"] = "Fit Window",
        ["Layout_Fullscreen"] = "Fullscreen",
        ["Layout_ExitFullscreen"] = "Exit Fullscreen",
        ["Layout_Config"] = "Connection Plan",
        ["Layout_Disconnect"] = "Disconnect",
        ["Layout_CloseAll"] = "Close All",
        ["Layout_EmptyCell"] = "+ Drop a session here or double-click an asset",
        ["Layout_RdpTitle"] = "🖥 Native RDP Session",
        ["Layout_RdpTarget"] = "Target: {0}:{1} (user: {2})",
        ["Layout_LaunchMstsc"] = "Launch Remote Desktop again (mstsc)",
        ["Layout_RdpLaunched"] = "System Remote Desktop (mstsc) launched. Switch from the taskbar.",
        ["Layout_AddSessionHint"] = "Double-click an asset in the list or press Connect to add it to the workspace.",
        ["Layout_NoActiveSession"] = "No active session selected.",

        // Settings page
        ["Settings_Title"] = "Settings",
        ["Settings_Theme"] = "Appearance",
        ["Settings_ThemeHint"] = "Switch light / dark instantly across all windows.",
        ["Settings_Language"] = "Language",
        ["Settings_LanguageHint"] = "Switch between Simplified Chinese and English instantly.",
        ["Settings_Probe"] = "Scheduled Reachability Probe",
        ["Settings_ProbeHint"] = "Set minutes greater than 0 to probe all assets periodically in background.",
        ["Settings_Data"] = "Local Data",
        ["Settings_DataHint"] = "All assets are stored locally only. Nothing is uploaded.",
        ["Settings_OpenDataDir"] = "Open Data Folder",
        ["Settings_AboutTitle"] = "About RemotePilot",
        ["Settings_AboutText"] = "A free, zero-account, offline-capable remote operations tool. One-click RDP, built-in SSH terminal, groups/owners/notes/expiry reminders.",
        ["Settings_Portal"] = "Visit RemotePilot Website",

        // SSH credentials
        ["Cred_Title"] = "SSH Credentials",
        ["Cred_ReenterTitle"] = "Authentication failed, please re-enter credentials",
        ["Status_CredReenter"] = "Credentials updated, reconnecting…",
        ["Cred_Subtitle"] = "Password is kept in memory for this run only and never written to disk.",
        ["Cred_Username"] = "Username",
        ["Cred_Password"] = "Password",
        ["Cred_KeyPath"] = "Private Key File (optional)",
        ["Cred_Passphrase"] = "Key Passphrase (optional)",
        ["Cred_Connect"] = "Connect",
        ["Cred_DialogHint"] = "No usable credentials are saved for this server. Enter credentials for this connection.",
        ["Cred_UsernameLabel"] = "Username:",
        ["Cred_PasswordLabel"] = "Password:",
        ["Cred_KeyPathLabel"] = "Private Key Path:",
        ["Cred_RememberLocal"] = "Remember password on this PC (Windows DPAPI encrypted)",
        ["Msg_SshUserRequired"] = "Please enter the SSH username.",
        ["Msg_SshCredRequired"] = "Enter a password or select a private key file.",

        // Status & Info
        ["Status_Ready"] = "Ready - RemotePilot",
        ["Status_RememberPasswordOn"] = "Remember passwords enabled (DPAPI encrypted)",
        ["Status_RememberPasswordOff"] = "Remember passwords disabled; saved passwords cleared",
        ["Status_Total"] = "{0} server(s) total, {1} shown",
        ["Msg_Tip"] = "Notice",
        ["Msg_SelectAsset"] = "Please select a server asset first.",
        ["Msg_SelectToDelete"] = "Please check the servers to delete.",
        ["Msg_ConfirmDelete"] = "Delete server \"{0}\" ({1})?",
        ["Msg_DeleteTitle"] = "Confirm Delete",
        ["Msg_ConfirmBatchDelete"] = "Delete {0} selected server(s)?\n\n{1}",
        ["Msg_BatchDeleteTitle"] = "Batch Delete",
        ["Msg_Saved"] = "Asset changes saved.",
        ["Msg_ExportedEmpty"] = "Nothing to export.",

        // Asset edit dialog
        ["Dlg_Title"] = "Server Configuration",
        ["Dlg_Header"] = "Server Configuration",
        ["Fld_Name"] = "Name:",
        ["Fld_Group"] = "Group:",
        ["Fld_Host"] = "IP:",
        ["Fld_Port"] = "Port:",
        ["Fld_Protocol"] = "Protocol:",
        ["Fld_Template"] = "Connection Template:",
        ["Fld_TemplateNone"] = "(No template)",
        ["Fld_Username"] = "Username:",
        ["Fld_Password"] = "Password:",
        ["Fld_RememberPassword"] = "Remember password on this PC (DPAPI encrypted, local only)",
        ["Fld_SshPort"] = "SSH Port:",
        ["Fld_KeyPath"] = "SSH Key Path:",
        ["Fld_Passphrase"] = "Key Passphrase:",
        ["Fld_JumpHost"] = "Jump host (SSH):",
        ["Fld_JumpHostNone"] = "(No jump host)",
        ["Msg_JumpCredMissing"] = "Jump host has no usable credential. Set a password (with remember-password enabled) or a private key first.",
        ["Term_JumpConnecting"] = "Connecting to {1}:{2} via jump host {0} …",
        ["Fld_Owner"] = "Owner:",
        ["Fld_ExpiresOn"] = "Expires On:",
        ["Fld_Note1"] = "Note 1:",
        ["Fld_Note2"] = "Note 2:",
        ["Fld_Note3"] = "Note 3:",
        ["Fld_Note4"] = "Note 4:",
        ["Dlg_RdpRedirect"] = "RDP Redirection & Options",
        ["Dlg_DriveMap"] = "Drive mapping",
        ["Dlg_Audio"] = "Remote audio",
        ["Dlg_Clipboard"] = "Use clipboard",
        ["Msg_HostRequired"] = "Server IP is required.",
        ["Msg_PortInvalid"] = "Please enter a valid port (1-65535).",
        ["Dlg_BrowseKey"] = "Select SSH Private Key",
        ["Dlg_AllFilesFilter"] = "All files (*.*)|*.*",

        // Connection plan drawer
        ["Drawer_Title"] = "Connection Plan",
        ["Drawer_Sec1"] = "1. Overview & Inheritance",
        ["Drawer_AssetName"] = "Asset: ",
        ["Drawer_Target"] = "Target: ",
        ["Drawer_ProtocolType"] = "Protocol: ",
        ["Drawer_ApplyTemplate"] = "Apply Template:",
        ["Drawer_ParamSource"] = "Source: ",
        ["Drawer_SourceCustom"] = "Custom override",
        ["Drawer_SourceInherit"] = "Inherited from template: {0}",
        ["Drawer_SourceFullCustom"] = "Fully custom",
        ["Drawer_SourceTemplateOnly"] = "Template only",
        ["Drawer_TemplatePrefix"] = "[Template] {0}",
        ["Drawer_PortLabel"] = "Port: {0}",
        ["Drawer_Sec2"] = "2. Display",
        ["Drawer_ResolutionMode"] = "Resolution Mode",
        ["Drawer_ResFit"] = "Fit container (default)",
        ["Drawer_Res1080p"] = "Standard 1080p (1920x1080)",
        ["Drawer_Res2k"] = "Standard 2K (2560x1440)",
        ["Drawer_Res4k"] = "Standard 4K (3840x2160)",
        ["Drawer_ResCustom"] = "Custom size",
        ["Drawer_Width"] = "Width",
        ["Drawer_Height"] = "Height",
        ["Drawer_ColorDepth"] = "Color Depth",
        ["Drawer_Depth8"] = "8-bit (256 colors)",
        ["Drawer_Depth15"] = "15-bit (High Color)",
        ["Drawer_Depth16"] = "16-bit (High Color)",
        ["Drawer_Depth24"] = "24-bit (True Color)",
        ["Drawer_Depth32"] = "32-bit (Best)",
        ["Drawer_NegotiationWarn"] = "⚠️ Some negotiated options (color depth, resolution) take effect on reconnect.",
        ["Drawer_Sec3"] = "3. Local Resources & Redirection",
        ["Drawer_ChkDrive"] = "Local drive mapping (map disks to remote)",
        ["Drawer_ChkAudio"] = "Audio redirection (play locally / mute remote)",
        ["Drawer_ChkClipboard"] = "Use clipboard (two-way sync)",
        ["Drawer_ComplianceHint"] = "💡 Note: clipboard or drive mapping may violate corporate security policy. Stay compliant.",
        ["Drawer_Sec4"] = "4. Protocol Specifics",
        ["Drawer_Gateway"] = "RDP Gateway",
        ["Drawer_GatewayHint"] = "Enter gateway address if using a domain or bastion host",
        ["Drawer_SshPort"] = "SSH Port",
        ["Drawer_SshUser"] = "Username",
        ["Drawer_KeyPath"] = "Private Key File",
        ["Drawer_SaveAsTemplate"] = "Save as Template",
        ["Drawer_SaveApply"] = "Save & Apply",
        ["Drawer_TemplateNameSuffix"] = "{0} Template",
        ["Drawer_NewTemplate"] = "New Connection Template",
        ["Drawer_TemplateSaved"] = "Saved as a new connection template!",
        ["Drawer_KeyFilter"] = "Key files (*.pem;*.key;*.id_rsa;*.*)|*.pem;*.key;*.id_rsa;*.*|All files (*.*)|*.*",

        // Main window extras
        ["App_Title"] = "RemotePilot RemoteOps Console",
        ["Theme_Toggle"] = "🌓 Theme",
        ["Lang_Toggle"] = "中 / EN",
        ["Btn_ExpiryBanner"] = "⚠️ Expiring Assets",
        ["Tip_Favorite"] = "Favorite / Unfavorite",
        ["Tip_MaximizeRestore"] = "Maximize/Restore (double-click the title bar)",
        ["Tip_CloseSession"] = "Close session",
        ["Panel_Overview"] = "Asset Overview",
        ["Btn_Collapse"] = "◀ Collapse",
        ["Tag_Total"] = "All ({0})",
        ["Group_Default"] = "Default Group",
        ["Status_Online"] = "Online",
        ["Status_Offline"] = "Offline",
        ["Status_Probing"] = "Probing...",
        ["Status_NotProbed"] = "Not probed",
        ["Status_Connecting"] = "Connecting...",
        ["Status_Disconnected"] = "Disconnected",
        ["Status_Connected"] = "Connected",
        ["Status_Failed"] = "Failed",
        ["Term_Disconnected"] = "Connection closed",
        ["Term_Connecting"] = "Connecting to {0}:{1} …",
        ["Term_ConnectFailed"] = "Connection failed: {0}",
        ["Term_StatusFailed"] = "Connection failed",
        ["Term_SchemeDark"] = "Colors: Dark",
        ["Term_SchemeGreen"] = "Colors: Green",
        ["Term_SchemeLight"] = "Colors: Light",
        ["Term_Copy"] = "Copy",
        ["Term_Paste"] = "Paste",
        ["Term_SelectAll"] = "Select All",
        ["Term_Clear"] = "Clear",
        ["Msg_FitWindowDone"] = "The session view has been fitted to the workspace.",
        ["Msg_RdpLaunchFailed"] = "Failed to launch RDP: {0}",
        ["Status_ExpiryBanner"] = "⚠️ {0} server(s) expiring or expired, click to filter",
        ["Status_DrawerApplied"] = "Connection plan saved and applied to the current asset.",
        ["Status_TemplateSaved"] = "New connection template [{0}] saved.",
        ["Status_ExitFullscreen"] = "Exited fullscreen mode",
        ["Status_EnterFullscreen"] = "Entered fullscreen workspace",
        ["Status_FavoritesView"] = "Favorites: {0} server(s)",
        ["Status_RecentView"] = "Recent: {0} server(s)",
        ["Status_GroupsView"] = "Groups: {0} group(s), {1} server(s)",
        ["Status_ExpiryFilter"] = "Filtered expiring/expired assets: {0} server(s)",
        ["Status_Retrying"] = "Retrying connection to {0} ({1})...",
        ["Status_Favorited"] = "Added {0} to favorites",
        ["Status_Unfavorited"] = "Removed {0} from favorites",
        ["Status_ProbingAll"] = "Probing all servers for reachability and latency...",
        ["Status_ProbeDone"] = "Probe completed.",
        ["Status_ProbeScheduled"] = "Scheduled probe completed at {0}",
        ["Status_SshOpened"] = "SSH terminal opened in workspace: {0}",
        ["Status_RdpOpened"] = "Remote desktop opened in workspace: {0}",
        ["Status_ConnectBackground"] = "Connection started: {0}. Open the Sessions workspace to view it.",
        ["Status_ClipboardSecurityHint"] = "Windows will show one more security dialog — tick \"Clipboard\" there to enable it.",
        ["Status_ConnectFailed"] = "Connection failed: {0}",
        ["Msg_SelectAssetForDrawer"] = "Select an asset first to configure its connection plan.",
        ["Msg_SelectAssetForRetry"] = "Select an asset to retry the connection.",
        ["Msg_SelectToConnect"] = "Please select a server to connect.",
        ["Msg_ErrorTitle"] = "Error",
        ["Msg_ImportDoneTitle"] = "Import Completed",
        ["Msg_ImportDone"] = "Imported {0} server(s).",
        ["Msg_ImportSkipped"] = "{0} row(s) failed to parse and were skipped.",
        ["Msg_ImportFailed"] = "Import failed: {0}",
        ["Msg_ExportDoneTitle"] = "Export Completed",
        ["Msg_ExportDone"] = "Exported {0} record(s) to:\n{1}",
        ["Msg_ExportFailed"] = "Export failed: {0}",
        ["Msg_SettingsSaved"] = "Settings and asset list saved.",
        ["Msg_SyncDoneTitle"] = "Import Completed",
        ["Msg_SyncDone"] = "Fetched and imported {0} server(s) from the API!",
        ["Msg_SshConnectFailed"] = "SSH connection failed: {0}",
        ["Msg_RdpConnectFailed"] = "Remote desktop connection failed: {0}",
        ["Msg_BrowserFailed"] = "Failed to open browser: {0}",
        ["Dlg_ImportTitle"] = "Import Server Assets (ISS7 export / standard CSV)",
        ["Dlg_ImportFilter"] = "ISS7 export (*.xls;*.xlsx)|*.xls;*.xlsx|Standard CSV (*.csv)|*.csv|All files (*.*)|*.*",
        ["Msg_ImportFormatHint"] = "No importable data found. Standard header: 分组,服务器名称,服务器IP和端口,账号,密码,到期时间,负责人,其他备注1,其他备注2,其他备注3.",
        ["Dlg_ExportTitle"] = "Export Server Assets (CSV)",
        ["Dlg_ExportFilter"] = "CSV files (*.csv)|*.csv",
        ["Csv_ErrorIpPort"] = "Server IP and port are missing or invalid",

        // Export field selection
        ["Dlg_ExportFieldsTitle"] = "Select Export Fields",
        ["ExpFld_Hint"] = "Choose the fields to export. Passwords are excluded by default; enabling the password column writes plaintext to the file and may leak credentials.",
        ["ExpFld_PasswordWarn"] = "⚠️ Enabling \"Password\" or \"Key Path\" writes server credentials / key paths to the CSV in plaintext. Keep the exported file safe.",
        ["ExpFld_RiskTitle"] = "Plaintext Password Risk",
        ["ExpFld_RiskBody"] = "You chose to export the password column. Passwords will be written to the CSV in plaintext, and anyone with the file can read the server credentials.\n\nExport plaintext passwords anyway?",
        ["Col_SshKeyPath"] = "Key Path",
        ["ExpFld_KeyPathRiskTitle"] = "Key Path Disclosure Risk",
        ["ExpFld_KeyPathRiskBody"] = "You chose to export the key path column. Key file paths will be written to the CSV in plaintext, which may expose how to access the server.\n\nExport key paths anyway?",
        ["Msg_ExportDoneNoPwd"] = "Credentials are not included in this export; please back them up separately.",

        // Import-from-API dialog (user-defined endpoint, no platform login)
        ["Sync_Title"] = "Import Assets from API - RemotePilot",
        ["Sync_Header"] = "Import Assets from Your Own API",
        ["Sync_Desc"] = "Enter your own asset API URL; RemotePilot sends a plain GET. JSON array or CSV text are both accepted. No platform login required.",
        ["Sync_BaseUrl"] = "API URL:",
        ["Sync_Token"] = "Token (optional):",
        ["Sync_Preview"] = "Asset preview:",
        ["Sync_ColImport"] = "Import",
        ["Sync_ColName"] = "Name",
        ["Sync_ColHost"] = "Host",
        ["Sync_BtnSync"] = "Fetch Assets",
        ["Sync_BtnConfirm"] = "Import Selected",
        ["Sync_MsgUrlRequired"] = "Please enter the API URL.",
        ["Sync_Connecting"] = "Requesting API...",
        ["Sync_Fetching"] = "Parsing API response...",
        ["Sync_Fetched"] = "Fetched {0} asset(s) ({1} after de-duplication). Select and confirm to import.",
        ["Sync_Imported"] = "Imported {0} asset(s)!",
        ["Sync_Error"] = "Import error: {0}",

        // Select all / invert & selected count
        ["Btn_InvertSelect"] = "Invert",
        ["Status_SelectedCount"] = "{0} selected",
        ["Btn_BatchProbe"] = "Probe All",
        ["Tip_StatusNotProbed"] = "No connectivity probe yet. Click \"Probe All\" to test reachability for all assets.",
        ["Tip_ExpiryBannerItem"] = "{0} ({1}) expires {2}",
        ["Msg_DeleteListMore"] = "… and {0} more not listed",

        // Template management
        ["TmplMgmt_BtnDelete"] = "Delete",
        ["TmplMgmt_WinTitle"] = "Connection Plan Templates",
        ["TmplMgmt_Header"] = "Connection Plan Templates",
        ["TmplMgmt_TemplateList"] = "Templates",
        ["TmplMgmt_SortByName"] = "Sort by Name",
        ["TmplMgmt_SortByUsage"] = "Sort by Usage",
        ["TmplMgmt_DetailTitle"] = "Template Details",
        ["TmplMgmt_RdpSettings"] = "RDP Settings",
        ["TmplMgmt_Resolution"] = "Resolution",
        ["TmplMgmt_ColorDepth"] = "Color Depth",
        ["TmplMgmt_Gateway"] = "Gateway",
        ["TmplMgmt_RedirectSettings"] = "Device Redirection",
        ["TmplMgmt_NoValue"] = "-",
        ["TmplMgmt_Bit"] = "bit",
        ["TmplMgmt_NoGateway"] = "No gateway",
        ["TmplMgmt_Copy"] = "Copy",
        ["TmplMgmt_DeleteWarning"] = "This template is used by {0} asset(s). After deletion these assets will use the top-bar defaults.\n\nDelete it?",
        ["TmplMgmt_DeleteTitle"] = "Confirm Delete",
        ["TmplMgmt_DeleteConfirm"] = "Delete this template?",
        ["Btn_New"] = "New",
        ["Btn_Copy"] = "Copy",
        ["Btn_Close"] = "Close",

        // VNC (externally installed client)
        ["Status_VncOpened"] = "VNC client mounted in workspace: {0}",
        ["Msg_VncConnectFailed"] = "VNC connection failed: {0}",
        ["Msg_VncLaunchFailed"] = "Failed to start the VNC client: {0}",
        ["Msg_VncViewerMissing"] = "No VNC client program found. This app does not bundle a VNC viewer. Install TightVNC / UltraVNC / RealVNC / TigerVNC first, then set the client executable path in Help Manual on the left.",
        ["Layout_VncTitle"] = "🖥 External VNC Session",
        ["Layout_VncTarget"] = "Target {0}:{1} · Client {2}",
        ["Layout_VncViewerMissing"] = "No VNC client found. Configure the client path in Help Manual.",
        ["Layout_LaunchVnc"] = "Launch VNC Client",

        // Help manual
        ["Help_Title"] = "Help Manual",
        ["Help_Subtitle"] = "Common questions and notes for each protocol, plus the configuration entry for the externally installed VNC client (path + launch arguments).",
        ["Help_VncConfigTitle"] = "VNC Client Configuration",
        ["Help_ViewerLabel"] = "VNC client program",
        ["Help_ArgsLabel"] = "Launch argument template",
        ["Help_ArgsHint"] = "Placeholders: {host}, {port}, {username}, {password}. When no password is saved, any argument containing {password} is dropped automatically; for the separate form (-password then the value) the -password switch is dropped as well.",
        ["Help_BtnBrowse"] = "Browse...",
        ["Help_BtnDetect"] = "Auto Detect",
        ["Help_BtnResetArgs"] = "Reset to Default",
        ["Help_BtnSave"] = "Save Settings",
        ["Help_ViewerFilter"] = "VNC client (*.exe)|*.exe|All files (*.*)|*.*",
        ["Help_ViewerDetected"] = "Located: {0} ({1})",
        ["Help_ViewerNotDetected"] = "No installed VNC client was detected. Specify the path to vncviewer.exe / tvnviewer.exe manually.",
        ["Help_ViewerNotExists"] = "The specified file does not exist. Please choose again.",
        ["Help_Saved"] = "VNC client settings saved.",
        ["Help_VncMoved"] = "The VNC client path and launch arguments have moved to the Settings page in the left navigation.",

        // Settings
        ["Settings_Subtitle"] = "Configure the externally installed VNC client and SSH terminal appearance.",
        ["Settings_VncSection"] = "VNC Client",
        ["Settings_SshSection"] = "SSH Terminal Appearance",
        ["Settings_SshFontSize"] = "Font Size",
        ["Settings_SshScheme"] = "Color Scheme",
        ["Settings_SshStandalone"] = "Open SSH in a separate window",
        ["Settings_BtnSave"] = "Save Settings",
        ["Settings_Saved"] = "Settings saved.",

        ["Help_SecVncUsage_Title"] = "How VNC Works Here",
        ["Help_SecVncUsage_Body"] = "RemotePilot does not bundle a VNC client. Connecting only launches the VNC viewer installed on this machine, and the remote screen is hosted by that viewer's own window. That is why the workspace shows a placeholder instead of the remote desktop.\n\n· Default port is 5900, which maps to display :0 on the server. Display :1 means port 5901, and so on.\n· In the argument template, {host} {port} {username} {password} are replaced with the asset values. Syntax differs per client: TightVNC uses host::port, UltraVNC uses -connect host::port.\n· Password: with \"Remember password\" enabled the password is passed as a launch argument. It then appears in plain text in the process command line, visible to other processes and security software on the same machine, so weigh the risk yourself. Without it, the client prompts for the password.\n· When creating an asset, pick vnc as the protocol and set the port to 5900 (or the actual port).",

        ["Help_SecVncFaq_Title"] = "VNC FAQ",
        ["Help_SecVncFaq_Body"] = "Q: Connecting reports \"No VNC client program found\".\nA: No VNC viewer is installed, or it sits in an unusual location. Install TightVNC / UltraVNC / RealVNC / TigerVNC and click Auto Detect on the Settings page; if that still fails, point to the executable manually: vncviewer.exe for RealVNC, TigerVNC and UltraVNC, tvnviewer.exe for TightVNC.\n\nQ: The client opens but still asks for a password.\nA: Two possibilities. Either \"Remember password\" is off for the asset, so no plain password is available to pass; or the client does not accept a command-line password (RealVNC and TigerVNC are in that group), so it must be typed in the client window.\n\nQ: After editing the argument template the connection stops working.\nA: The template must match the client you use. Click Reset to Default to restore the default form for that client family, then adjust from there.\n\nQ: Clicking connect does nothing at all.\nA: First check the client path validation result on the Settings page, then check whether the address separator in the template is one your client accepts (:: and : are not interchangeable).",

        ["Help_SecRdp_Title"] = "RDP FAQ",
        ["Help_SecRdp_Body"] = "Q: Drive mapping, clipboard and sound are enabled, yet the mstsc window still shows them unchecked.\nA: That is Windows behaviour, not the app failing. After recent Windows security updates, an unsigned .rdp file triggers the \"Note: unknown remote connection\" dialog where all redirections default to off and must be ticked manually each time; the .rdp file cannot pre-tick them. Options: tick them by hand each time (simplest), sign the .rdp with rdpsign (requires a code-signing certificate), or set RedirectionWarningDialogVersion to 1 via group policy (weakens that protection and needs admin rights).\n\nQ: Why does the RDP window title read \"Name (IP)\"?\nA: mstsc takes its title from the .rdp file name. The app names the temp file \"<asset name> (<host>).rdp\", so the title becomes \"Name (IP)\"; when the asset has no name only the IP is shown.\n\nQ: The connection failed but the app shows no reason.\nA: mstsc is a separate process. The app cannot detect authentication failures inside it, so rely on the dialog mstsc itself shows.",

        ["Help_SecRedirection_Title"] = "Why redirection does not work",
        ["Help_SecRedirection_Body"] = "Q: Clipboard / drives are ticked in the toolbar or connection profile, yet they still do not work in the mstsc window.\nA: That is a Windows security restriction, not the app failing to write the settings. Since the Windows April 2026 security update (CVE-2026-26151), Remote Desktop shows a security dialog before opening any .rdp file; inside it every redirection (clipboard, drives, printers, microphone, camera, etc.) is off by default and must be ticked manually on each connection. An unsigned .rdp has no \"don't ask again\" option.\n\nQ: What does the app actually do?\nA: Based on your toolbar / connection-profile choices the app writes keys such as redirectclipboard:i:1 into the temporary .rdp; but Windows still asks you to confirm again in the security dialog, and the app cannot tick that box for you.\n\nQ: What should I do?\nA: When connecting, tick \"Clipboard\" (and any other items you need) in the security dialog, then continue.\n\nQ: Does this relate to \"Remember password\"?\nA: No. \"Remember password\" only decides whether credentials are written into the .rdp (password 51:b); it does not affect whether the redirection toggles appear in the dialog.\n\nQ: Is there a way to set it once and stop ticking every time?\nA: Yes, but it needs an organisation-level action: sign the .rdp with rdpsign, or have IT trust the publisher certificate thumbprint (SHA-2) via group policy. After signing / trusting, Windows no longer intercepts and the redirection selections take effect directly.",

        ["Help_SecSsh_Title"] = "SSH FAQ",
        ["Help_SecSsh_Body"] = "Q: What happens when the SSH password is wrong?\nA: The app prompts for credentials again and reconnects. Each asset is prompted only once, so a still-wrong password will not loop dialogs.\n\nQ: What does a jump host require?\nA: The jump host asset itself needs a usable password or private key. When both are missing the connection fails before it starts.\n\nQ: Can a key and a password be kept at the same time?\nA: Yes. The connection uses the private key file when it exists, and only falls back to the password when the key is unavailable.",

        ["Help_SecGeneral_Title"] = "General Notes",
        ["Help_SecGeneral_Body"] = "· Assets and settings live in %LOCALAPPDATA%\\FastRdp\\RemoteOps\\assets.json.\n· Passwords are encrypted with Windows DPAPI for the current user before being written to disk. After switching Windows user or machine the ciphertext cannot be decrypted and must be entered again.\n· Each RDP connection creates a temporary .rdp file under %TEMP%\\RemotePilot\\{random folder}\\ containing the DPAPI-encrypted password field; it is safe to delete.\n· With \"Remember password\" off, the app stores no credentials for any protocol."
    };

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
