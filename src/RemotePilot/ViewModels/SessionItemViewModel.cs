using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using RemotePilot.Models;
using RemotePilot.Services;
using VirtualTerminal.Session;

namespace RemotePilot.ViewModels;

public enum SessionLayoutMode
{
    Single,
    Split1x2,
    Split2x1,
    Grid2x2,
    Grid2x3,
    Grid3x3
}

public enum SessionStatusKind
{
    Connecting,
    Connected,
    Disconnected,
    Failed
}

public class SessionItemViewModel : INotifyPropertyChanged
{
    private static LocalizationManager L10n => LocalizationManager.Instance;

    private SessionStatusKind _statusKind = SessionStatusKind.Connecting;
    private bool _isFocused;
    private bool _isMaximized;
    private FrameworkElement? _viewElement;

    public Guid Id { get; } = Guid.NewGuid();
    public ServerAsset Asset { get; }

    public string Name => Asset.Name;
    public string Protocol => Asset.Protocol;
    public string HostPort => $"{Asset.Host}:{Asset.Port}";
    public string Username => !string.IsNullOrEmpty(Asset.Username)
        ? Asset.Username
        : Protocol.ToLowerInvariant() switch
        {
            "ssh" => "root",
            // VNC 的认证只针对会话本身，没有用户名概念
            "vnc" => "-",
            _ => "Administrator"
        };

    public SessionStatusKind StatusKind => _statusKind;

    public string Status => _statusKind switch
    {
        SessionStatusKind.Connected => L10n["Status_Connected"],
        SessionStatusKind.Disconnected => L10n["Status_Disconnected"],
        SessionStatusKind.Failed => L10n["Status_Failed"],
        _ => L10n["Status_Connecting"]
    };

    public string StatusColorBrushKey => _statusKind switch
    {
        SessionStatusKind.Connected => "Success",
        SessionStatusKind.Disconnected or SessionStatusKind.Failed => "Danger",
        _ => "Warning"
    };

    public bool IsConnected => _statusKind == SessionStatusKind.Connected;

    public void SetStatus(SessionStatusKind kind)
    {
        if (_statusKind == kind) return;
        _statusKind = kind;
        OnPropertyChanged(nameof(StatusKind));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColorBrushKey));
        OnPropertyChanged(nameof(IsConnected));
    }

    public bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsMaximized
    {
        get => _isMaximized;
        set
        {
            if (_isMaximized != value)
            {
                _isMaximized = value;
                OnPropertyChanged();
            }
        }
    }

    public FrameworkElement? ViewElement
    {
        get => _viewElement;
        set
        {
            if (_viewElement != value)
            {
                _viewElement = value;
                OnPropertyChanged();
            }
        }
    }

    // 后台 SSH 会话持有，用于适时销毁
    public TerminalSession? SshSession { get; set; }

    // SSH 跳板连接（跳板机 SshClient + 端口转发），会话关闭时一并释放
    public IDisposable? JumpConnection { get; set; }

    public SessionItemViewModel(ServerAsset asset)
    {
        Asset = asset;
    }

    public void CloseSession()
    {
        try
        {
            SshSession?.Dispose();
            SshSession = null;
        }
        catch { }
        try
        {
            JumpConnection?.Dispose();
            JumpConnection = null;
        }
        catch { }
        SetStatus(SessionStatusKind.Disconnected);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? prop = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
