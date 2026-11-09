using System.IO;
using System.Text;
using RemotePilot.Models;

namespace RemotePilot.Services;

public sealed record CsvImportResult
{
    public List<ServerAsset> Assets { get; init; } = [];
    public List<CsvImportError> Errors { get; init; } = [];
}

public sealed record CsvImportError
{
    public int LineNumber { get; init; }
    public string LineContent { get; init; } = "";
    public string Message { get; init; } = "";
}

public static class AssetCsvService
{
    private static readonly string[] DangerousFormulaPrefixes = ["=", "+", "-", "@", "\t", "\r", "\n"];

    public static CsvImportResult ImportCsv(string csvContent)
    {
        var assets = new List<ServerAsset>();
        var errors = new List<CsvImportError>();

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return new CsvImportResult { Assets = assets, Errors = errors };
        }

        using var reader = new StringReader(csvContent);
        string? headerLine = null;
        int currentLineNumber = 0;

        // Find first non-empty line as header
        while (true)
        {
            var rawLine = reader.ReadLine();
            currentLineNumber++;
            if (rawLine == null)
            {
                break;
            }
            if (!string.IsNullOrWhiteSpace(rawLine))
            {
                headerLine = rawLine.Trim();
                break;
            }
        }

        if (headerLine == null)
        {
            return new CsvImportResult { Assets = assets, Errors = errors };
        }

        var headers = ParseCsvLine(headerLine);
        var headerMap = BuildHeaderMap(headers);

        while (true)
        {
            var rawLine = reader.ReadLine();
            currentLineNumber++;
            if (rawLine == null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            try
            {
                var fields = ParseCsvLine(rawLine);
                // 客户手工编辑的表格常留空行（如 ",,,,,"），直接跳过
                if (fields.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }
                var asset = MapFieldsToAsset(headerMap, fields, headers);
                if (asset != null)
                {
                    assets.Add(asset);
                }
                else
                {
                    errors.Add(new CsvImportError
                    {
                        LineNumber = currentLineNumber,
                        LineContent = rawLine,
                        Message = LocalizationManager.Instance["Csv_ErrorIpPort"]
                    });
                }
            }
            catch (Exception ex)
            {
                errors.Add(new CsvImportError
                {
                    LineNumber = currentLineNumber,
                    LineContent = rawLine,
                    Message = ex.Message
                });
            }
        }

        return new CsvImportResult { Assets = assets, Errors = errors };
    }

    public static string ExportCsv(IEnumerable<ServerAsset> assets)
    {
        var sb = new StringBuilder();
        sb.AppendLine("分组,服务器名称,服务器IP和端口,账号,到期时间,负责人,其他备注1,其他备注2,其他备注3,协议,端口");

        foreach (var asset in assets)
        {
            var group = SanitizeForCsv(asset.Group);
            var name = SanitizeForCsv(asset.Name);
            var ipPort = SanitizeForCsv(FormatIpPort(asset.Host, asset.Port));
            var username = SanitizeForCsv(asset.Username);
            var expires = SanitizeForCsv(asset.ExpiresOn?.ToString("yyyy-MM-dd") ?? "");
            var owner = SanitizeForCsv(asset.Owner);
            var note1 = SanitizeForCsv(asset.CustomNote1);
            var note2 = SanitizeForCsv(asset.CustomNote2);
            var note3 = SanitizeForCsv(asset.CustomNote3);
            var protocol = SanitizeForCsv(asset.Protocol);
            var portStr = SanitizeForCsv(asset.Port.ToString());

            sb.AppendLine($"{group},{name},{ipPort},{username},{expires},{owner},{note1},{note2},{note3},{protocol},{portStr}");
        }

        return sb.ToString();
    }

    /// <summary>生成给客户自行编辑的标准导入模板（表头 + 一行示例）。</summary>
    public static string BuildStandardTemplateCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("分组,服务器名称,服务器IP和端口,账号,密码,到期时间,负责人,其他备注1,其他备注2,其他备注3");
        sb.AppendLine("示例分组,示例服务器,127.0.0.1:3389,administrator,,2027-12-31,张三,可留空,,");
        return sb.ToString();
    }

    private static string SanitizeForCsv(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "\"\"";
        }

        // Formula injection protection: prefix with single quote '\'' if starts with '=', '+', '-', '@'
        var val = input;
        if (val.Length > 0 && DangerousFormulaPrefixes.Contains(val[0].ToString()))
        {
            val = "'" + val;
        }

        // Escape double quotes by doubling them
        if (val.Contains('"') || val.Contains(',') || val.Contains('\n') || val.Contains('\r'))
        {
            val = "\"" + val.Replace("\"", "\"\"") + "\"";
        }
        else
        {
            // Even if no special chars, wrapping in quotes for robust CSV structure is safe, or quote if needed.
            // Let's wrap in quotes if it contains spaces or just general safety
            val = "\"" + val + "\"";
        }

        return val;
    }

    private static string FormatIpPort(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "";
        }
        return $"{host}:{port}";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var cur = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cur.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cur.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',' || c == '\t')
                {
                    result.Add(cur.ToString().Trim());
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }
        }

        result.Add(cur.ToString().Trim());
        return result;
    }

    private static Dictionary<string, int> BuildHeaderMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim();
            if (!map.ContainsKey(h))
            {
                map[h] = i;
            }
        }
        return map;
    }

    private static ServerAsset? MapFieldsToAsset(Dictionary<string, int> headerMap, List<string> fields, List<string> headers)
    {
        string GetField(params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (headerMap.TryGetValue(name, out var idx) && idx < fields.Count)
                {
                    return fields[idx];
                }
            }
            // Fallback by index or substring match if exact header not found
            for (int i = 0; i < headers.Count; i++)
            {
                foreach (var name in possibleNames)
                {
                    if (headers[i].Contains(name, StringComparison.OrdinalIgnoreCase) && i < fields.Count)
                    {
                        return fields[i];
                    }
                }
            }
            return "";
        }

        // IIS7 Remote Desktop Manager style or standard column recognition
        var group = GetField("分组", "Group", "目录", "分类");
        var name = GetField("服务器名称", "服务器名", "名称", "Name", "主机名", "Title");
        var ipPortRaw = GetField("服务器IP和端口", "IP和端口", "IP", "Host", "地址", "ServerIP");
        var username = GetField("账号", "用户名", "用户", "Username", "User", "账号/用户名");
        var password = GetField("密码", "Password", "PSW", "口令");
        var expiresStr = GetField("到期时间", "过期时间", "到期", "Expires", "ExpireTime");
        var owner = GetField("负责人", "Owner", "管理员", "Responsible");
        var note1 = GetField("其他备注1", "备注1", "备1", "Note1", "CustomNote1");
        var note2 = GetField("其他备注2", "备注2", "备2", "Note2", "CustomNote2");
        var note3 = GetField("其他备注3", "备注3", "备3", "Note3", "CustomNote3");
        var protocol = GetField("协议", "Protocol");
        var portStr = GetField("端口", "Port");

        // Fallback positional if header mapping is ambiguous or standard IIS7 10-column layout
        // IIS7 common export: 分组, 服务器名称, 服务器IP和端口, 账号, 密码(ignored), 到期时间, 负责人, 其他备注1, 其他备注2, 其他备注3 ...
        if (string.IsNullOrWhiteSpace(name) && fields.Count > 1)
        {
            // Try positional fallback
            if (string.IsNullOrWhiteSpace(group) && fields.Count > 0) group = fields[0];
            if (string.IsNullOrWhiteSpace(name) && fields.Count > 1) name = fields[1];
            if (string.IsNullOrWhiteSpace(ipPortRaw) && fields.Count > 2) ipPortRaw = fields[2];
            if (string.IsNullOrWhiteSpace(username) && fields.Count > 3) username = fields[3];
            // fields[4] 在 ISS7 导出里是 PSW 密码列
            if (string.IsNullOrWhiteSpace(password) && fields.Count > 4) password = fields[4];
            if (string.IsNullOrWhiteSpace(expiresStr) && fields.Count > 5) expiresStr = fields[5];
            if (string.IsNullOrWhiteSpace(owner) && fields.Count > 6) owner = fields[6];
            if (string.IsNullOrWhiteSpace(note1) && fields.Count > 7) note1 = fields[7];
            if (string.IsNullOrWhiteSpace(note2) && fields.Count > 8) note2 = fields[8];
            if (string.IsNullOrWhiteSpace(note3) && fields.Count > 9) note3 = fields[9];
        }

        if (string.IsNullOrWhiteSpace(ipPortRaw))
        {
            // If ipPortRaw is empty, check if any field looks like IP or IP:port
            foreach (var f in fields)
            {
                if (!string.IsNullOrWhiteSpace(f) && (f.Contains('.') || f.Contains(':') || f.Contains("localhost")))
                {
                    ipPortRaw = f;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(ipPortRaw))
        {
            return null;
        }

        ParseHostAndPort(ipPortRaw, out var host, out var port);
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(portStr) && int.TryParse(portStr, out var explicitPort) && explicitPort > 0 && explicitPort <= 65535)
        {
            port = explicitPort;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = host;
        }

        DateOnly? expiresOn = null;
        if (!string.IsNullOrWhiteSpace(expiresStr) && DateOnly.TryParse(expiresStr.Trim(), out var parsedDate))
        {
            expiresOn = parsedDate;
        }
        else if (double.TryParse(expiresStr.Trim(), out var oaDate) && oaDate > 0 && oaDate < 2958466)
        {
            // ISS7 导出未套日期格式时，“到期”会是一串 Excel 日期序列号
            expiresOn = DateOnly.FromDateTime(DateTime.FromOADate(oaDate));
        }

        var proto = !string.IsNullOrWhiteSpace(protocol) ? protocol.ToLowerInvariant() : "rdp";

        var asset = new ServerAsset
        {
            Group = group.Trim(),
            Name = name.Trim(),
            Host = host.Trim(),
            Port = port,
            Username = username.Trim(),
            Owner = owner.Trim(),
            ExpiresOn = expiresOn,
            Protocol = proto,
            CustomNote1 = note1.Trim(),
            CustomNote2 = note2.Trim(),
            CustomNote3 = note3.Trim(),
            // 仅在内存中携带，落盘与否由调用方按“本机记住密码”决定
            Password = string.IsNullOrWhiteSpace(password) ? null : password.Trim()
        };

        return asset;
    }

    private static void ParseHostAndPort(string ipPortRaw, out string host, out int port)
    {
        host = "";
        port = 3389;

        if (string.IsNullOrWhiteSpace(ipPortRaw))
        {
            return;
        }

        var trimmed = ipPortRaw.Trim();
        // Handle IPv6 like [::1]:3389 or IPv4 like 192.168.1.1:3389
        int lastColon = trimmed.LastIndexOf(':');
        if (lastColon > 0 && !trimmed.EndsWith(']') && !trimmed.Contains('/'))
        {
            var hostPart = trimmed[..lastColon].Trim();
            var portPart = trimmed[(lastColon + 1)..].Trim();
            if (int.TryParse(portPart, out var p) && p > 0 && p <= 65535)
            {
                port = p;
            }
            host = hostPart;
        }
        else
        {
            host = trimmed;
        }

        // Clean surrounding quotes or brackets if any
        if (host.StartsWith('"') && host.EndsWith('"') && host.Length >= 2)
        {
            host = host[1..^1];
        }
    }
}
