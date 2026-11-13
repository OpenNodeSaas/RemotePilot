using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RemotePilot.Models;
using RemotePilot.Services;
using Renci.SshNet;
using VirtualTerminal;

namespace RemotePilot.Controls;

/// <summary>
/// SSH 终端会话宿主：错误提示条 + 原生终端控件，并负责建立 SSH 连接（含跳板机与密钥/口令凭据）。
/// 工作台内嵌会话与独立窗口共用此实现，避免连接逻辑重复。
/// </summary>
public sealed class SshTerminalHost : Border
{
    private static LocalizationManager L10n => LocalizationManager.Instance;

    private readonly Border _messageBar;
    private readonly TextBlock _messageText;
    private readonly SshTerminalView _terminal;
    private SecureShellSession? _session;
    private SshJumpConnection? _jumpConnection;
    private bool _connectStarted;

    /// <summary>开始连接时触发。</summary>
    public event Action? Connecting;
    /// <summary>连接成功时触发（UI 线程）。</summary>
    public event Action<SecureShellSession, SshJumpConnection?>? Connected;
    /// <summary>连接失败时触发（UI 线程），携带错误信息。</summary>
    public event Action<string?>? ConnectFailed;
    /// <summary>会话断开时触发（UI 线程）。</summary>
    public event Action? Disconnected;

    public SshTerminalHost(ServerAsset asset, string? password, string? privateKeyPath, string? privateKeyPassphrase,
        ServerAsset? jumpAsset, string? jumpPassword)
    {
        // 顶部错误提示条 + 原生 WPF 终端（不依赖 WebView2 运行时）
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _messageBar = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A2A2A")),
            Padding = new Thickness(10, 6, 10, 6),
            Visibility = Visibility.Collapsed
        };
        _messageText = new TextBlock
        {
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F48771")),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        _messageBar.Child = _messageText;
        Grid.SetRow(_messageBar, 0);
        grid.Children.Add(_messageBar);

        _terminal = new SshTerminalView();
        Grid.SetRow(_terminal, 1);
        grid.Children.Add(_terminal);

        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
        Child = grid;

        // 加入可视树后再发起 SSH 连接
        _terminal.Loaded += async (s, e) =>
        {
            if (_connectStarted) return;
            _connectStarted = true;

            Connecting?.Invoke();
            ShowMessage(jumpAsset != null
                ? L10n.Format("Term_JumpConnecting", jumpAsset.Host, asset.Host, asset.Port)
                : L10n.Format("Term_Connecting", asset.Host, asset.Port));

            SecureShellSession? session = null;
            SshJumpConnection? jumpConnection = null;
            string? connectError = null;
            try
            {
                string username = !string.IsNullOrEmpty(asset.Username) ? asset.Username : "root";

                if (jumpAsset != null)
                {
                    // 先连跳板机并开本地端口转发，再从 127.0.0.1:随机端口 连目标机
                    var jumpKeyPath = jumpAsset.SshKeyPath;
                    if (string.IsNullOrWhiteSpace(jumpPassword) && string.IsNullOrWhiteSpace(jumpKeyPath))
                    {
                        throw new ArgumentException(L10n["Msg_JumpCredMissing"]);
                    }

                    jumpConnection = await Task.Run(() => SshJumpConnection.Create(
                        jumpAsset, jumpPassword, jumpKeyPath, asset.Host, asset.Port));

                    var targetClient = CreateSshClient(jumpConnection.LoopbackHost, jumpConnection.LoopbackPort,
                        username, password, privateKeyPath, privateKeyPassphrase);
                    session = new SecureShellSession(targetClient);
                }
                else if (!string.IsNullOrEmpty(privateKeyPath) && File.Exists(privateKeyPath))
                {
                    string keyPath = privateKeyPath;
                    PrivateKeyFile keyFile = string.IsNullOrEmpty(privateKeyPassphrase)
                        ? new PrivateKeyFile(keyPath)
                        : new PrivateKeyFile(keyPath, privateKeyPassphrase);
                    session = new SecureShellSession(asset.Host, asset.Port, username, keyFile);
                }
                else if (!string.IsNullOrEmpty(password))
                {
                    session = new SecureShellSession(asset.Host, asset.Port, username, password);
                }
                else
                {
                    throw new ArgumentException(L10n["Msg_SshCredRequired"]);
                }

                await session.ConnectAsync();
            }
            catch (Exception ex)
            {
                connectError = ex.Message;
                jumpConnection?.Dispose();
                jumpConnection = null;
            }

            if (connectError == null && session != null)
            {
                // 连接成功后再挂到终端控件，确保内部的窗口尺寸同步不会在未连接时抛异常
                _session = session;
                _jumpConnection = jumpConnection;
                _terminal.Terminal.Session = session;
                _terminal.SetStatus(L10n["Status_Connected"]);
                _messageBar.Visibility = Visibility.Collapsed;
                _terminal.FocusTerminal();

                // 断连提示：事件可能来自后台线程，统一切回 UI 线程
                session.Disconnected += (s2, e2) => _terminal.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ShowMessage(L10n["Term_Disconnected"]);
                    _terminal.SetStatus(L10n["Status_Disconnected"]);
                    Disconnected?.Invoke();
                }));

                Connected?.Invoke(session, jumpConnection);
            }
            else
            {
                session?.Dispose();
                ShowMessage(L10n.Format("Term_ConnectFailed", connectError ?? ""));
                _terminal.SetStatus(L10n["Term_StatusFailed"]);
                ConnectFailed?.Invoke(connectError);
            }
        };
    }

    /// <summary>释放本次会话（终端被移除或窗口关闭时调用，可重复调用）。</summary>
    public void DisposeSession()
    {
        try { _session?.Dispose(); } catch { }
        try { _jumpConnection?.Dispose(); } catch { }
        _session = null;
        _jumpConnection = null;
    }

    private void ShowMessage(string message)
    {
        _messageText.Text = message;
        _messageBar.Visibility = Visibility.Visible;
    }

    /// <summary>按凭据类型创建 SSH 客户端（密钥优先，其次密码）。</summary>
    private static SshClient CreateSshClient(string host, int port, string username, string? password, string? keyPath, string? passphrase)
    {
        username = string.IsNullOrWhiteSpace(username) ? "root" : username;

        if (!string.IsNullOrEmpty(keyPath) && File.Exists(keyPath))
        {
            var keyFile = string.IsNullOrEmpty(passphrase) ? new PrivateKeyFile(keyPath) : new PrivateKeyFile(keyPath, passphrase);
            return new SshClient(host, port, username, keyFile);
        }

        return new SshClient(host, port, username, password ?? "");
    }
}
