using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RemotePilot.Models;

namespace RemotePilot.Services;

public sealed record AssetStoreData
{
    public int Version { get; init; } = 1;
    public List<ServerAsset> Assets { get; init; } = [];
    public List<ConnectionTemplate> Templates { get; init; } = [];
    public int ExpiryLeadDays { get; init; } = 30;

    // 本机是否记住登录密码（密码以 DPAPI 密文写入 EncryptedPassword）
    public bool RememberPassword { get; init; }

    // 用户自定义的资产接口地址（"接口导入"上次使用的 URL）
    public string AssetApiUrl { get; init; } = "";

    // 左侧导航是否处于折叠（图标条）状态
    public bool IsLeftNavCollapsed { get; init; }

    // 外装 VNC 客户端：可执行文件路径与启动参数模板；留空时按常见安装位置与 PATH 自动探测
    public string VncViewerPath { get; init; } = "";
    public string VncArgumentTemplate { get; init; } = "";

    // 资产列表（表视图）展示偏好：隐藏列 key、列显示顺序 key、行字号
    public List<string> AssetListHiddenColumns { get; init; } = [];
    public List<string> AssetListColumnOrder { get; init; } = [];
    public double AssetListFontSize { get; init; } = 14;

    // SSH 相关本地偏好：是否以独立窗口打开 SSH 会话、终端默认字号、终端配色方案档位
    public bool SshStandaloneWindow { get; init; }
    public double SshTerminalFontSize { get; init; } = 13;
    public int SshTerminalScheme { get; init; }
}

public sealed class AssetStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AssetStore(string? filePath = null)
    {
        if (filePath != null)
        {
            _filePath = filePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _filePath = Path.Combine(appData, "FastRdp", "RemoteOps", "assets.json");
        }
    }

    public string FilePath => _filePath;

    public AssetStoreData Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AssetStoreData();
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AssetStoreData();
            }

            var data = JsonSerializer.Deserialize<AssetStoreData>(json, JsonOptions);
            if (data == null)
            {
                return new AssetStoreData();
            }

            // 完善的校验逻辑（防止非法格式覆盖与数据污染）
            var validAssets = new List<ServerAsset>();
            var seenAssetIds = new HashSet<Guid>();
            foreach (var asset in data.Assets)
            {
                if (asset == null || asset.Id == Guid.Empty || seenAssetIds.Contains(asset.Id))
                    continue;
                if (string.IsNullOrWhiteSpace(asset.Name) || string.IsNullOrWhiteSpace(asset.Host))
                    continue;
                if (asset.Port <= 0 || asset.Port > 65535)
                    continue;

                seenAssetIds.Add(asset.Id);
                validAssets.Add(asset);
            }

            var validTemplates = new List<ConnectionTemplate>();
            var seenTemplateIds = new HashSet<Guid>();
            foreach (var tmpl in data.Templates)
            {
                if (tmpl == null || tmpl.Id == Guid.Empty || seenTemplateIds.Contains(tmpl.Id))
                    continue;
                if (string.IsNullOrWhiteSpace(tmpl.Name))
                    continue;
                if (tmpl.Port <= 0 || tmpl.Port > 65535)
                    continue;

                seenTemplateIds.Add(tmpl.Id);
                validTemplates.Add(tmpl);
            }

            // 列 key 白名单：过滤掉不认识/重复的列 key
            var validColumnKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "Favorite", "Select", "Group", "Owner", "Name", "IpPort", "Username",
                "Status", "ExpiresOn", "Note1", "Note2", "Note3", "Note4", "Template", "Protocol", "Action"
            };

            static List<string> SanitizeColumns(IEnumerable<string>? keys, HashSet<string> whitelist)
            {
                var result = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (keys == null)
                {
                    return result;
                }

                foreach (var key in keys)
                {
                    if (string.IsNullOrEmpty(key) || !whitelist.Contains(key) || !seen.Add(key))
                        continue;
                    result.Add(key);
                }

                return result;
            }

            return new AssetStoreData
            {
                Version = data.Version > 0 ? data.Version : 1,
                Assets = validAssets,
                Templates = validTemplates,
                ExpiryLeadDays = data.ExpiryLeadDays > 0 ? data.ExpiryLeadDays : 30,
                RememberPassword = data.RememberPassword,
                AssetApiUrl = data.AssetApiUrl ?? "",
                IsLeftNavCollapsed = data.IsLeftNavCollapsed,
                VncViewerPath = data.VncViewerPath ?? "",
                VncArgumentTemplate = data.VncArgumentTemplate ?? "",
                AssetListHiddenColumns = SanitizeColumns(data.AssetListHiddenColumns, validColumnKeys),
                AssetListColumnOrder = SanitizeColumns(data.AssetListColumnOrder, validColumnKeys),
                AssetListFontSize = (data.AssetListFontSize >= 10 && data.AssetListFontSize <= 24) ? data.AssetListFontSize : 14,
                SshStandaloneWindow = data.SshStandaloneWindow,
                SshTerminalFontSize = (data.SshTerminalFontSize >= 9 && data.SshTerminalFontSize <= 28) ? data.SshTerminalFontSize : 13,
                SshTerminalScheme = (data.SshTerminalScheme >= 0 && data.SshTerminalScheme <= 2) ? data.SshTerminalScheme : 0
            };
        }
        catch
        {
            // 解析失败或格式损坏时返回默认空实例，避免程序崩溃
            return new AssetStoreData();
        }
    }

    public void Save(AssetStoreData data)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(data, JsonOptions);
        var tempFile = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        // 写入临时文件
        File.WriteAllText(tempFile, json);

        // 临时文件原子替换
        if (File.Exists(_filePath))
        {
            File.Replace(tempFile, _filePath, null);
        }
        else
        {
            File.Move(tempFile, _filePath);
        }
    }
}
