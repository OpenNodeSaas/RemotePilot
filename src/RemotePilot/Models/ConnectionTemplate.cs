using System.Text.Json.Serialization;

namespace RemotePilot.Models;

public sealed record ConnectionTemplate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public string Protocol { get; init; } = "rdp";
    public int Port { get; init; } = 3389;
    public string Username { get; init; } = "";

    // 分辨率与颜色深度
    public int RdpWidth { get; init; } = 1600;
    public int RdpHeight { get; init; } = 900;
    public int RdpColorDepth { get; init; } = 32;
    public string RdpGateway { get; init; } = "";

    // 硬件重定向开关
    public bool RedirectDrives { get; init; } = false; // 硬盘映射
    public bool RedirectAudio { get; init; } = true;    // 声音重定向
    public bool RedirectClipboard { get; init; } = true;// 剪切板重定向

    // SSH 密钥路径 (仅存本地路径，绝不存密钥明文)
    public string SshKeyPath { get; init; } = "";

    // SSH 独立窗口模式的初始窗口尺寸
    public int SshWindowWidth { get; init; } = 1000;
    public int SshWindowHeight { get; init; } = 640;
    public bool IsDefault { get; init; }

    // 资产同步扩展字段
    public string? RemoteTemplateId { get; init; }
    public DateTimeOffset? LastSyncedAt { get; init; }
    public Dictionary<string, string> SyncExtensions { get; init; } = new();

    // 运行时内存凭据（绝对不序列化落盘）
    [JsonIgnore]
    public string? Password { get; set; }

    [JsonIgnore]
    public string? PrivateKey { get; set; }

    /// <summary>
    /// 获取模板的显示摘要，格式：模板名 [网关: xxx | 分辨率 色深]
    /// </summary>
    public string GetDisplaySummary()
    {
        var gateway = string.IsNullOrWhiteSpace(RdpGateway) ? "无网关" : $"网关: {RdpGateway}";
        var resolution = $"{RdpWidth}x{RdpHeight} {RdpColorDepth}位";
        return $"{Name} [{gateway} | {resolution}]";
    }
}
