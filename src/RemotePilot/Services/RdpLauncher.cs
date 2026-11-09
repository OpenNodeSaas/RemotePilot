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

        // drivestoredirect: i:1 enables drive redirection, i:0 disables
        int driveStoreRedirectInt = redirectDrives ? 1 : 0;
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
        sb.AppendLine($"drivestoredirect:i:{driveStoreRedirectInt}");
        sb.AppendLine($"audiomode:i:{audioModeInt}");
        sb.AppendLine($"redirectclipboard:i:{redirectClipboardInt}");
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

    public static string CreateTempRdpFile(ServerAsset asset, ConnectionTemplate? template = null, string? password = null)
    {
        var content = GenerateRdpContent(asset, template, password);
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"remotepilot_{Guid.NewGuid():N}.rdp");
        File.WriteAllText(tempFilePath, content, Encoding.UTF8);
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
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
            throw;
        }
    }
}
