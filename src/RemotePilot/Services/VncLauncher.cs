using System.Diagnostics;
using System.Globalization;
using System.IO;
using RemotePilot.Models;

namespace RemotePilot.Services;

/// <summary>已知的外装 VNC 客户端家族，用于挑选默认启动参数模板。</summary>
public enum VncViewerFamily
{
    Unknown,
    TightVnc,
    UltraVnc,
    RealVnc,
    TigerVnc
}

/// <summary>已定位到的 VNC 客户端可执行文件。</summary>
public sealed record VncViewerInfo(string ExecutablePath, VncViewerFamily Family, string DisplayName);

/// <summary>
/// 外装 VNC 客户端启动器：应用不内置 VNC viewer，只负责定位外部客户端、
/// 渲染启动参数并作为独立进程拉起（与 RDP 调用 mstsc.exe 的思路一致）。
/// </summary>
public static class VncLauncher
{
    public const int DefaultPort = 5900;

    private static readonly (string SubDirectory, string Executable, VncViewerFamily Family, string DisplayName)[] KnownViewers =
    [
        (@"TightVNC", "tvnviewer.exe", VncViewerFamily.TightVnc, "TightVNC Viewer"),
        (@"UltraVNC", "vncviewer.exe", VncViewerFamily.UltraVnc, "UltraVNC Viewer"),
        (@"RealVNC\VNC Viewer", "vncviewer.exe", VncViewerFamily.RealVnc, "RealVNC VNC Viewer"),
        (@"TigerVNC", "vncviewer.exe", VncViewerFamily.TigerVnc, "TigerVNC Viewer")
    ];

    private static readonly string[] PathExecutableNames = ["tvnviewer.exe", "vncviewer.exe"];

    /// <summary>
    /// 定位 VNC 客户端：优先使用设置里指定且真实存在的路径，否则在常见安装目录与 PATH 中探测。
    /// </summary>
    public static VncViewerInfo? ResolveViewer(string? configuredPath = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return Describe(configuredPath);
        }

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            foreach (var (subDirectory, executable, family, displayName) in KnownViewers)
            {
                var candidate = Path.Combine(root, subDirectory, executable);
                if (File.Exists(candidate))
                {
                    return new VncViewerInfo(candidate, family, displayName);
                }
            }
        }

        foreach (var name in PathExecutableNames)
        {
            var found = FindOnPath(name);
            if (found != null) return Describe(found);
        }

        return null;
    }

    /// <summary>按可执行文件所在路径推断客户端家族。</summary>
    public static VncViewerFamily IdentifyFamily(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return VncViewerFamily.Unknown;

        var fileName = Path.GetFileName(executablePath);
        var full = executablePath.ToLowerInvariant();

        if (fileName.Equals("tvnviewer.exe", StringComparison.OrdinalIgnoreCase) || full.Contains("tightvnc"))
            return VncViewerFamily.TightVnc;
        if (full.Contains("ultravnc")) return VncViewerFamily.UltraVnc;
        if (full.Contains("realvnc")) return VncViewerFamily.RealVnc;
        if (full.Contains("tigervnc")) return VncViewerFamily.TigerVnc;
        return VncViewerFamily.Unknown;
    }

    /// <summary>按客户端家族给出默认启动参数模板（占位符：{host} {port} {username} {password}）。</summary>
    public static string DefaultArgumentTemplate(VncViewerFamily family) => family switch
    {
        // TightVNC: <host>::<port> -password=<pw>
        VncViewerFamily.TightVnc => "{host}::{port} -password={password}",
        // UltraVNC: -connect <host>::<port> -password <pw>
        VncViewerFamily.UltraVnc => "-connect {host}::{port} -password {password}",
        // RealVNC / TigerVNC / 未知客户端只接受地址，密码由客户端窗口自行弹出输入
        _ => "{host}::{port}"
    };

    /// <summary>家族的可读名称，用于界面提示。</summary>
    public static string DescribeFamily(VncViewerFamily family) => family switch
    {
        VncViewerFamily.TightVnc => "TightVNC",
        VncViewerFamily.UltraVnc => "UltraVNC",
        VncViewerFamily.RealVnc => "RealVNC",
        VncViewerFamily.TigerVnc => "TigerVNC",
        _ => "Unknown"
    };

    /// <summary>
    /// 把参数模板渲染为进程参数列表。模板按空白切分；含 {password} 的片段在无密码时整体丢弃，
    /// 若是「-password 值」这种分离写法，则连同前面的 -password 开关一起丢弃。
    /// 返回列表而非拼接字符串，交给 ProcessStartInfo 处理引号，避免主机名/密码里的特殊字符注入。
    /// </summary>
    public static IReadOnlyList<string> RenderArguments(string? template, ServerAsset asset, string? password)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var host = (asset.Host ?? "").Trim();
        int port = asset.Port > 0 ? asset.Port : DefaultPort;
        var username = (asset.Username ?? "").Trim();
        var pw = password ?? "";

        var effective = string.IsNullOrWhiteSpace(template)
            ? DefaultArgumentTemplate(VncViewerFamily.Unknown)
            : template;

        var rendered = new List<string>();
        foreach (var token in effective.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Contains("{password}", StringComparison.Ordinal) && pw.Length == 0)
            {
                if (!token.Contains('=') && rendered.Count > 0 && IsPasswordSwitch(rendered[^1]))
                {
                    rendered.RemoveAt(rendered.Count - 1);
                }
                continue;
            }

            rendered.Add(token
                .Replace("{host}", host, StringComparison.Ordinal)
                .Replace("{port}", port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{username}", username, StringComparison.Ordinal)
                .Replace("{password}", pw, StringComparison.Ordinal));
        }

        return rendered;
    }

    /// <summary>用给定客户端与参数直接拉起 VNC 会话窗口。</summary>
    public static Process? Launch(string executablePath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo);
    }

    private static bool IsPasswordSwitch(string token)
    {
        if (token.Length < 2 || (token[0] != '-' && token[0] != '/')) return false;

        var name = token[1..];
        return name.Equals("password", StringComparison.OrdinalIgnoreCase)
            || name.Equals("passwd", StringComparison.OrdinalIgnoreCase);
    }

    private static VncViewerInfo Describe(string executablePath)
    {
        var family = IdentifyFamily(executablePath);
        foreach (var (_, _, knownFamily, displayName) in KnownViewers)
        {
            if (knownFamily == family)
            {
                return new VncViewerInfo(executablePath, family, displayName);
            }
        }

        return new VncViewerInfo(executablePath, family, Path.GetFileNameWithoutExtension(executablePath));
    }

    private static string? FindOnPath(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), executableName);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // PATH 中存在非法字符的条目，跳过
            }
        }

        return null;
    }
}
