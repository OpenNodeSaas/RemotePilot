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
        ["Nav_Feedback"] = "✉ 留下您的宝贵意见",
        ["Btn_ToggleLeftNav"] = "◀",
        ["Btn_ToggleLeftNavExpand"] = "▶",
        ["Tip_CollapseLeftNav"] = "收起导航栏",
        ["Tip_ExpandLeftNav"] = "展开导航栏",
        ["Msg_FeedbackTitle"] = "RemotePilot 意见反馈",
        ["Msg_FeedbackFallback"] = "感谢您愿意为 RemotePilot 提出宝贵建议，我们会认真阅读每一条反馈。\n\n当前电脑没有检测到可用的邮件客户端，因此未能自动打开发信窗口。\n请手动发送邮件至：\n\nremotemate@protonmail.com\n\n小提示：在此窗口按 Ctrl+C 可复制全文，粘贴到任意邮箱网页版即可发送。",

        // 顶部与操作
        ["Search_Placeholder"] = "全局搜索：名称 / IP / 分组 / 负责人 / 备注",
        ["Btn_Search"] = "搜索",
        ["Btn_AddServer"] = "新增资产",
        ["Btn_Import"] = "导入",
        ["Btn_ImportTemplate"] = "导入模板",
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
        ["Btn_ViewTable"] = "表格视图",
        ["Btn_ViewGrid"] = "宫格视图",
        ["Btn_OpenDrawer"] = "连接方案",
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
        ["Col_Protocol"] = "协议",
        ["Col_Username"] = "账号",
        ["Col_Status"] = "状态",
        ["Col_ExpiresOn"] = "到期时间",
        ["Col_Note1"] = "备注1",
        ["Col_Note2"] = "备注2",
        ["Col_Note3"] = "备注3",

        // 工具栏配置项
        ["Setting_WindowMode"] = "窗口模式:",
        ["Setting_ModeNormal"] = "正常",
        ["Setting_ModeFullscreen"] = "全屏",
        ["Setting_ModeSmall"] = "小屏",
        ["Setting_ModeTab"] = "标签窗口",
        ["Setting_DeviceControl"] = "设备控制:",
        ["Setting_DriveMap"] = "硬盘映射",
        ["Setting_Audio"] = "声音",
        ["Setting_Clipboard"] = "禁止剪切板",
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
        ["Insp_Host"] = "主机/IP",
        ["Insp_Port"] = "端口",
        ["Insp_Group"] = "分组",
        ["Insp_Owner"] = "负责人",
        ["Insp_Protocol"] = "协议",
        ["Insp_Username"] = "账号",
        ["Insp_Password"] = "密码 / 凭据",
        ["Insp_ExpiresOn"] = "到期日期 (yyyy-MM-dd)",
        ["Insp_Note1"] = "业务备注 1",
        ["Insp_Note2"] = "业务备注 2",
        ["Insp_Note3"] = "业务备注 3",
        ["Insp_ExpiryWarn"] = "到期预警状态: ",
        ["Insp_LastProbe"] = "最后检测耗时: ",
        ["Insp_Save"] = "保存当前资产修改",
        ["Insp_Placeholder"] = "请选择一台服务器查看或修改详情",
        ["Insp_Favorite"] = "收藏",
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
        ["Settings_Title"] = "系统设置",
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
        ["Cred_Hint"] = "长期保存使用 Windows DPAPI 按当前系统账户加密，仅存本机、不上传云端；更换机器或系统账户后无法解密。",
        ["Cred_StateSaved"] = "已保存密码（本机加密）",
        ["Cred_StateNone"] = "未保存凭据",
        ["Cred_SessionOnly"] = "仅本次会话",
        ["Cred_LongTerm"] = "长期保存（本机加密）",
        ["Btn_ClearCredential"] = "清除已保存凭据",
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
        ["Status_CredentialCleared"] = "已清除 {0} 的已保存凭据",
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
        ["Dlg_RdpRedirect"] = "RDP 设备重定向与参数",
        ["Dlg_DriveMap"] = "硬盘映射(本地硬盘)",
        ["Dlg_Audio"] = "加载远程声音",
        ["Dlg_Clipboard"] = "禁止剪切板",
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
        ["Drawer_ChkClipboard"] = "剪贴板隔离 (允许双向剪贴板同步)",
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
        ["Dlg_TemplateTitle"] = "导出标准导入模板",
        ["Msg_TemplateSaved"] = "模板已保存至:\n{0}",
        ["Msg_ImportFormatHint"] = "未识别到可导入的数据。标准格式表头：分组,服务器名称,服务器IP和端口,账号,密码,到期时间,负责人,其他备注1,其他备注2,其他备注3（可先用“导入模板”生成一份）。",
        ["Dlg_ExportTitle"] = "导出服务器资产 CSV",
        ["Dlg_ExportFilter"] = "CSV 文件 (*.csv)|*.csv",
        ["Csv_ErrorIpPort"] = "服务器IP和端口缺失或无效",

        // 导出字段选择
        ["Dlg_ExportFieldsTitle"] = "选择导出字段",
        ["ExpFld_Hint"] = "请选择需要导出的字段。密码默认不导出；勾选密码后将以明文写入文件，存在泄露风险。",
        ["ExpFld_PasswordWarn"] = "⚠️ 勾选“密码”会把服务器凭据以明文写入 CSV，请务必妥善保管导出文件。",
        ["ExpFld_RiskTitle"] = "明文密码泄露风险",
        ["ExpFld_RiskBody"] = "您勾选了导出密码列。密码会以明文形式写入 CSV 文件，任何拿到该文件的人都能看到服务器凭据，可能造成安全风险。\n\n确定要明文导出密码吗？",
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
        ["Msg_DeleteListMore"] = "… 其余 {0} 台未列出"
    };

    private static readonly Dictionary<string, string> _enStrings = new()
    {
        // Navigation
        ["Nav_Assets"] = "Asset Hub",
        ["Nav_Sessions"] = "Session Hub",
        ["Nav_Favorites"] = "Favorites",
        ["Nav_Recent"] = "Recent",
        ["Nav_Groups"] = "Groups",
        ["Nav_Feedback"] = "✉ Send Feedback",
        ["Btn_ToggleLeftNav"] = "◀",
        ["Btn_ToggleLeftNavExpand"] = "▶",
        ["Tip_CollapseLeftNav"] = "Collapse navigation",
        ["Tip_ExpandLeftNav"] = "Expand navigation",
        ["Msg_FeedbackTitle"] = "RemotePilot Feedback",
        ["Msg_FeedbackFallback"] = "Thank you for helping make RemotePilot better — every piece of feedback is read carefully.\n\nNo mail client was detected on this PC, so the compose window could not be opened automatically.\nPlease email us at:\n\nremotemate@protonmail.com\n\nTip: press Ctrl+C in this window to copy the text, then paste it into any webmail.",

        // Top command bar & actions
        ["Search_Placeholder"] = "Search name / IP / group / owner / note",
        ["Btn_Search"] = "Search",
        ["Btn_AddServer"] = "Add Asset",
        ["Btn_Import"] = "Import",
        ["Btn_ImportTemplate"] = "Template",
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
        ["Btn_OpenDrawer"] = "Connection Plan",
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
        ["Col_Protocol"] = "Protocol",
        ["Col_Username"] = "User",
        ["Col_Status"] = "Status",
        ["Col_ExpiresOn"] = "Expires On",
        ["Col_Note1"] = "Note 1",
        ["Col_Note2"] = "Note 2",
        ["Col_Note3"] = "Note 3",

        // Config options
        ["Setting_WindowMode"] = "Window Mode:",
        ["Setting_ModeNormal"] = "Normal",
        ["Setting_ModeFullscreen"] = "Fullscreen",
        ["Setting_ModeSmall"] = "Small",
        ["Setting_ModeTab"] = "Tab Window",
        ["Setting_DeviceControl"] = "Device Redirection:",
        ["Setting_DriveMap"] = "Drive Map",
        ["Setting_Audio"] = "Remote Audio",
        ["Setting_Clipboard"] = "Disable Clipboard",
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
        ["Insp_Host"] = "Host / IP",
        ["Insp_Port"] = "Port",
        ["Insp_Group"] = "Group",
        ["Insp_Owner"] = "Owner",
        ["Insp_Protocol"] = "Protocol",
        ["Insp_Username"] = "Username",
        ["Insp_Password"] = "Password / Credential",
        ["Insp_ExpiresOn"] = "Expires On (yyyy-MM-dd)",
        ["Insp_Note1"] = "Business Note 1",
        ["Insp_Note2"] = "Business Note 2",
        ["Insp_Note3"] = "Business Note 3",
        ["Insp_ExpiryWarn"] = "Expiry Status: ",
        ["Insp_LastProbe"] = "Last Probe: ",
        ["Insp_Save"] = "Save Asset Changes",
        ["Insp_Placeholder"] = "Select a server to view or edit its details",
        ["Insp_Favorite"] = "Favorite",
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
        ["Cred_Hint"] = "Permanent storage is encrypted with Windows DPAPI under the current system account, stored on this PC only, and never uploaded. It cannot be decrypted after switching machine or account.",
        ["Cred_StateSaved"] = "Password saved (encrypted on this PC)",
        ["Cred_StateNone"] = "No saved credential",
        ["Cred_SessionOnly"] = "This session only",
        ["Cred_LongTerm"] = "Save permanently (encrypted on this PC)",
        ["Btn_ClearCredential"] = "Clear saved credential",
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
        ["Status_CredentialCleared"] = "Cleared saved credential for {0}",
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
        ["Dlg_RdpRedirect"] = "RDP Redirection & Options",
        ["Dlg_DriveMap"] = "Drive mapping",
        ["Dlg_Audio"] = "Remote audio",
        ["Dlg_Clipboard"] = "Disable clipboard",
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
        ["Drawer_ChkClipboard"] = "Clipboard redirection (two-way sync)",
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
        ["Dlg_TemplateTitle"] = "Export Standard Import Template",
        ["Msg_TemplateSaved"] = "Template saved to:\n{0}",
        ["Msg_ImportFormatHint"] = "No importable data found. Standard header: 分组,服务器名称,服务器IP和端口,账号,密码,到期时间,负责人,其他备注1,其他备注2,其他备注3 (use \"Template\" to generate one first).",
        ["Dlg_ExportTitle"] = "Export Server Assets (CSV)",
        ["Dlg_ExportFilter"] = "CSV files (*.csv)|*.csv",
        ["Csv_ErrorIpPort"] = "Server IP and port are missing or invalid",

        // Export field selection
        ["Dlg_ExportFieldsTitle"] = "Select Export Fields",
        ["ExpFld_Hint"] = "Choose the fields to export. Passwords are excluded by default; enabling the password column writes plaintext to the file and may leak credentials.",
        ["ExpFld_PasswordWarn"] = "⚠️ Enabling \"Password\" writes server credentials to the CSV in plaintext. Keep the exported file safe.",
        ["ExpFld_RiskTitle"] = "Plaintext Password Risk",
        ["ExpFld_RiskBody"] = "You chose to export the password column. Passwords will be written to the CSV in plaintext, and anyone with the file can read the server credentials.\n\nExport plaintext passwords anyway?",
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
        ["Msg_DeleteListMore"] = "… and {0} more not listed"
    };

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
