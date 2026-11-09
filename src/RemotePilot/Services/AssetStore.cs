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

            return new AssetStoreData
            {
                Version = data.Version > 0 ? data.Version : 1,
                Assets = validAssets,
                Templates = validTemplates,
                ExpiryLeadDays = data.ExpiryLeadDays > 0 ? data.ExpiryLeadDays : 30,
                RememberPassword = data.RememberPassword,
                AssetApiUrl = data.AssetApiUrl ?? "",
                IsLeftNavCollapsed = data.IsLeftNavCollapsed
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
