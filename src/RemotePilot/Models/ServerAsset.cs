using System.Text.Json.Serialization;

namespace RemotePilot.Models;

public sealed record ServerAsset
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public string Host { get; init; } = "";
    public int Port { get; init; } = 3389;
    public string Protocol { get; init; } = "rdp";
    public Guid? TemplateId { get; init; }
    public string Username { get; init; } = "";
    public string Group { get; init; } = "";
    public string Owner { get; init; } = "";
    public DateOnly? ExpiresOn { get; init; }
    public bool IsFavorite { get; init; }
    public DateTimeOffset? LastAccessedAt { get; init; }
    public string Notes { get; init; } = "";
    
    // 扩展字段：自定义备注 1~3 与键值对
    public Dictionary<string, string> CustomNotes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    // 硬件重定向开关
    public bool? RedirectDrives { get; init; }
    public bool? RedirectAudio { get; init; }
    public bool? RedirectClipboard { get; init; }

    // 分辨率与颜色深度
    public int? RdpWidth { get; init; }
    public int? RdpHeight { get; init; }
    public int? RdpColorDepth { get; init; }

    // SSH 密钥路径 (仅本地路径)
    public string SshKeyPath { get; init; } = "";

    // SSH 独立窗口模式的初始窗口尺寸（为空时用默认尺寸）
    public int? SshWindowWidth { get; init; }
    public int? SshWindowHeight { get; init; }

    // 探测与同步扩展字段
    public DateTimeOffset? LastProbeAt { get; init; }
    public bool? LastProbeReachable { get; init; }
    public string? RemoteAssetId { get; init; }
    public DateTimeOffset? LastSyncedAt { get; init; }

    // SSH 跳板机：引用另一台资产的 Id，连接时先连它再中转（仅 SSH 生效）
    public Guid? JumpAssetId { get; init; }
    public Dictionary<string, string> SyncExtensions { get; init; } = new();

    // 本机记住的密码密文（Windows DPAPI 当前用户加密，仅开启“记住密码”时写入）
    public string? EncryptedPassword { get; init; }

    // 运行时内存凭据（绝对不序列化落盘，确保满足敏感密码/私钥绝对不参与序列化）
    [JsonIgnore]
    public string? Password { get; set; }

    [JsonIgnore]
    public string? PrivateKey { get; set; }

    // Custom notes 1, 2, 3 convenience properties
    [JsonIgnore]
    public string CustomNote1
    {
        get => CustomNotes.TryGetValue("CustomNote1", out var val) ? val : "";
        init => CustomNotes["CustomNote1"] = value;
    }

    [JsonIgnore]
    public string CustomNote2
    {
        get => CustomNotes.TryGetValue("CustomNote2", out var val) ? val : "";
        init => CustomNotes["CustomNote2"] = value;
    }

    [JsonIgnore]
    public string CustomNote3
    {
        get => CustomNotes.TryGetValue("CustomNote3", out var val) ? val : "";
        init => CustomNotes["CustomNote3"] = value;
    }

    [JsonIgnore]
    public string CustomNote4
    {
        get => CustomNotes.TryGetValue("CustomNote4", out var val) ? val : "";
        init => CustomNotes["CustomNote4"] = value;
    }
}
