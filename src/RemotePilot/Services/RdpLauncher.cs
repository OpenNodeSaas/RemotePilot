using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using RemotePilot.Models;

namespace RemotePilot.Services;

public static class RdpLauncher
{
    private static readonly char[] DangerousChars = ['\r', '\n'];

    public static string SanitizeRdpValue(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "";
        }

        if (input.IndexOfAny(DangerousChars) >= 0)
        {
            throw new ArgumentException("RDP configuration injection detected: CR/LF characters are not allowed in RDP parameters.", nameof(input));
        }

        return input;
    }

    /// <summary>把明文密码转成 mstsc 能识别的 DPAPI 密文（password 51:b 字段）。</summary>
    private static string ProtectForRdp(string password)
    {
        var bytes = ProtectedData.Protect(Encoding.Unicode.GetBytes(password), null, DataProtectionScope.CurrentUser);
        return Convert.ToHexString(bytes);
    }

    public static string GenerateRdpContent(ServerAsset asset, ConnectionTemplate? template = null, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(asset);

        // Merge configuration: asset takes precedence or fallback to template/defaults
        string host = SanitizeRdpValue(asset.Host);
        int port = asset.Port > 0 ? asset.Port : (template?.Port > 0 ? template.Port : 3389);
        string username = SanitizeRdpValue(!string.IsNullOrEmpty(asset.Username) ? asset.Username : (template?.Username ?? ""));
        
        int width = asset.RdpWidth ?? template?.RdpWidth ?? 1600;
        int height = asset.RdpHeight ?? template?.RdpHeight ?? 900;
        int colorDepth = asset.RdpColorDepth ?? template?.RdpColorDepth ?? 32;

        bool redirectDrives = asset.RedirectDrives ?? template?.RedirectDrives ?? false;
        bool redirectAudio = asset.RedirectAudio ?? template?.RedirectAudio ?? true;
        bool redirectClipboard = asset.RedirectClipboard ?? template?.RedirectClipboard ?? true;

        // screen mode id: 1 for windowed, 2 for full screen. Default 1 (windowed with width/height)
        int screenModeId = 1;

        // audiomode: 0 = bring to this computer, 1 = leave at remote computer, 2 = do not play
        int audioModeInt = redirectAudio ? 0 : 2;
        // redirectclipboard: i:1 enabled, i:0 disabled
        int redirectClipboardInt = redirectClipboard ? 1 : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"full address:s:{host}:{port}");
        sb.AppendLine($"server port:i:{port}");
        if (!string.IsNullOrEmpty(username))
        {
            sb.AppendLine($"username:s:{username}");
        }
        sb.AppendLine($"screen mode id:i:{screenModeId}");
        sb.AppendLine($"desktopwidth:i:{width}");
        sb.AppendLine($"desktopheight:i:{height}");
        sb.AppendLine($"session bpp:i:{colorDepth}");

        // 设备重定向：显式输出所有相关键，避免 mstsc 用默认值覆盖
        sb.AppendLine($"redirectclipboard:i:{redirectClipboardInt}");
        if (redirectDrives)
        {
            sb.AppendLine("drivestoredirect:s:*");
        }
        // 其他设备默认不重定向，显式写出防止 mstsc 沿用上次连接的缓存
        sb.AppendLine("redirectcomports:i:0");
        sb.AppendLine("redirectprinters:i:0");
        sb.AppendLine("redirectsmartcards:i:0");
        sb.AppendLine("redirectposdevices:i:0");

        sb.AppendLine($"audiomode:i:{audioModeInt}");
        sb.AppendLine("audiocapturemode:i:0");
        // 有记住的密码时直接写入 .rdp 并关闭凭据提示，实现免输入登录
        if (!string.IsNullOrEmpty(password))
        {
            sb.AppendLine($"password 51:b:{ProtectForRdp(password)}");
            sb.AppendLine("prompt for credentials:i:0");
        }
        else
        {
            sb.AppendLine("prompt for credentials:i:1");
        }

        // Ensure gateway if present in template
        if (template != null && !string.IsNullOrEmpty(template.RdpGateway))
        {
            string gateway = SanitizeRdpValue(template.RdpGateway);
            sb.AppendLine($"gatewayhostname:s:{gateway}");
            sb.AppendLine("gatewayusagemethod:i:1");
            sb.AppendLine("gatewaycredentialssource:i:4");
        }

        return sb.ToString();
    }

    /// <summary>mstsc 窗口标题取自 .rdp 文件名，这里生成「资产名称 (主机地址).rdp」。</summary>
    private static string BuildRdpFileName(ServerAsset asset)
    {
        var name = StripInvalidFileNameChars(asset.Name);
        var host = StripInvalidFileNameChars(asset.Host);
        var label = name.Length == 0 || name == host ? host : $"{name} ({host})";
        return $"{label}.rdp";
    }

    private static string StripInvalidFileNameChars(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Trim())
        {
            if (Array.IndexOf(invalid, ch) < 0) sb.Append(ch);
        }
        return sb.ToString();
    }

    public static string CreateTempRdpFile(ServerAsset asset, ConnectionTemplate? template = null, string? password = null)
    {
        var content = GenerateRdpContent(asset, template, password);
        // 每次连接使用独立子目录：文件名保持可读，同时不覆盖上一次可能仍被 mstsc 占用的文件
        var dir = Path.Combine(Path.GetTempPath(), "RemotePilot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var tempFilePath = Path.Combine(dir, BuildRdpFileName(asset));
        // mstsc 原生以 UTF-16LE（含 BOM）读写 .rdp，用 UTF-8 会导致中文主机名在标题栏乱码
        File.WriteAllText(tempFilePath, content, Encoding.Unicode);
        return tempFilePath;
    }

    public static Process? Launch(ServerAsset asset, out string tempFilePath, ConnectionTemplate? template = null, string? password = null)
    {
        tempFilePath = CreateTempRdpFile(asset, template, password);
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "mstsc.exe",
                ArgumentList = { tempFilePath },
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            return process;
        }
        catch
        {
            try { Directory.Delete(Path.GetDirectoryName(tempFilePath)!, true); } catch { }
            throw;
        }
    }
}
