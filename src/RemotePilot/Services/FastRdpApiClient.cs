using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using RemotePilot.Models;

namespace RemotePilot.Services;

/// <summary>从用户自定义的资产接口拉取数据（GET），兼容 JSON 与 CSV 两种返回。</summary>
public sealed class FastRdpApiClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public FastRdpApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>GET 指定接口地址并按返回内容解析出资产列表。</summary>
    public async Task<List<ServerAsset>> FetchAssetsFromEndpointAsync(
        string url, string? bearerToken = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken.Trim());
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
        {
            try
            {
                return ParseJsonAssets(content);
            }
            catch (JsonException)
            {
                // 不是合法 JSON，落到 CSV 解析
            }
        }

        return AssetCsvService.ImportCsv(content).Assets;
    }

    private static List<ServerAsset> ParseJsonAssets(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 支持：[...]、{"assets":[...]}、{"data":[...]}、{"targets":[...]}
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "assets", "data", "targets", "list", "servers" })
            {
                if (root.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    root = arr;
                    break;
                }
            }
        }

        var assets = new List<ServerAsset>();
        if (root.ValueKind != JsonValueKind.Array)
        {
            return assets;
        }

        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in item.EnumerateObject())
            {
                fields[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.ToString();
            }

            var host = Pick(fields, "host", "ip", "address", "server", "服务器ip", "服务器ip和端口", "ip和端口");
            if (string.IsNullOrWhiteSpace(host))
            {
                continue;
            }

            int port = 3389;
            var portText = Pick(fields, "port", "端口");
            if (!int.TryParse(portText, out port) || port <= 0)
            {
                port = 3389;
            }

            SplitHostPort(host, ref host, ref port);

            assets.Add(new ServerAsset
            {
                Name = DefaultIfEmpty(Pick(fields, "name", "title", "alias", "服务器名称", "服务器名", "名称"), host),
                Host = host.Trim(),
                Port = port,
                Protocol = DefaultIfEmpty(Pick(fields, "protocol", "协议"), "rdp"),
                Username = Pick(fields, "username", "user", "account", "账号", "用户"),
                Password = NullIfEmpty(Pick(fields, "password", "pwd", "psw", "密码", "口令")),
                Group = Pick(fields, "group", "分组"),
                Owner = Pick(fields, "owner", "负责人"),
                Notes = Pick(fields, "notes", "note", "remark", "备注"),
                CustomNote1 = Pick(fields, "备1", "note1", "remark1"),
                CustomNote2 = Pick(fields, "备2", "note2", "remark2"),
                CustomNote3 = Pick(fields, "备3", "note3", "remark3"),
                ExpiresOn = ParseDate(Pick(fields, "expires", "expire", "expires_on", "到期", "到期时间")),
                RemoteAssetId = Pick(fields, "id"),
                LastSyncedAt = DateTimeOffset.UtcNow
            });
        }

        return assets;
    }

    private static string Pick(Dictionary<string, string> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }
        return "";
    }

    private static void SplitHostPort(string raw, ref string host, ref int port)
    {
        var idx = raw.LastIndexOf(':');
        if (idx > 0 && idx < raw.Length - 1 && int.TryParse(raw[(idx + 1)..], out var parsed) && parsed > 0)
        {
            host = raw[..idx];
            port = parsed;
        }
    }

    private static DateOnly? ParseDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
            DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
        {
            return DateOnly.FromDateTime(dt);
        }
        return null;
    }

    private static string DefaultIfEmpty(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public void Dispose()
    {
    }
}
