using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using RemotePilot.Models;
using RemotePilot.Services;
using RemotePilot.ViewModels;
using Renci.SshNet;
using VirtualTerminal;

namespace RemotePilot.Controls;

public class TabBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        bool isFocused = value is bool b && b;
        return isFocused 
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0969DA")) { Opacity = 0.15 }
            : (Brush)Application.Current.FindResource("SurfaceBackground");
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class BrushConverterProxy : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        string key = value as string ?? "Warning";
        try
        {
            return Application.Current.FindResource(key) ?? Brushes.Gray;
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public partial class SessionWorkspaceControl : UserControl
{
    public const string ResLayoutLabel = "Layout_Label";
    public const string ResLayoutSingle = "Layout_Single";
    public const string ResLayout1x2 = "Layout_1x2";
    public const string ResLayout2x1 = "Layout_2x1";
    public const string ResLayout2x2 = "Layout_2x2";
    public const string ResLayout2x3 = "Layout_2x3";
    public const string ResLayout3x3 = "Layout_3x3";
    public const string ResLayoutFit = "Layout_Fit";
    public const string ResLayoutFullscreen = "Layout_Fullscreen";
    public const string ResLayoutExitFullscreen = "Layout_ExitFullscreen";
    public const string ResLayoutConfig = "Layout_Config";
    public const string ResLayoutDisconnect = "Layout_Disconnect";
    public const string ResLayoutCloseAll = "Layout_CloseAll";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    private readonly ObservableCollection<SessionItemViewModel> _sessions = [];
    private SessionLayoutMode _layoutMode = SessionLayoutMode.Single;
    private SessionItemViewModel? _activeSession;

    public event Action<SessionItemViewModel?>? ActiveSessionChanged;
    public event Action? RequestOpenSettingsDrawer;
    public event Action? RequestFullscreenToggle;

    public ObservableCollection<SessionItemViewModel> Sessions => _sessions;

    public SessionWorkspaceControl()
    {
        InitializeComponent();
        ItemsTabs.ItemsSource = _sessions;
        RebuildGridLayout();
    }

    public void AddSession(ServerAsset asset, string? password = null, string? privateKeyPath = null, string? privateKeyPassphrase = null,
        ServerAsset? jumpAsset = null, string? jumpPassword = null, ConnectionTemplate? template = null)
    {
        // 检查是否已存在相同资产的会话，若已存在则直接激活
        var existing = _sessions.FirstOrDefault(s => s.Asset.Id == asset.Id);
        if (existing != null)
        {
            SetActiveSession(existing);
            return;
        }

        var vm = new SessionItemViewModel(asset);

        // 创建对应视图元素 (SSH 原生终端 或 RDP 占位)
        if (asset.Protocol.Equals("SSH", StringComparison.OrdinalIgnoreCase))
        {
            vm.ViewElement = CreateSshTerminalView(vm, password, privateKeyPath, privateKeyPassphrase, jumpAsset, jumpPassword);
        }
        else
        {
            vm.ViewElement = CreateRdpPlaceholderView(vm, password, template);
        }

        _sessions.Add(vm);
        SetActiveSession(vm);
        RebuildGridLayout();
    }

    private FrameworkElement CreateSshTerminalView(SessionItemViewModel vm, string? password, string? privateKeyPath, string? privateKeyPassphrase,
        ServerAsset? jumpAsset = null, string? jumpPassword = null)
    {
        // 顶部错误提示条 + 原生 WPF 终端（不依赖 WebView2 运行时）
        var host = new Grid();
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var messageBar = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A2A2A")),
            Padding = new Thickness(10, 6, 10, 6),
            Visibility = Visibility.Collapsed
        };
        var messageText = new TextBlock
        {
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F48771")),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        messageBar.Child = messageText;
        Grid.SetRow(messageBar, 0);
        host.Children.Add(messageBar);

        var terminal = new SshTerminalView();
        Grid.SetRow(terminal, 1);
        host.Children.Add(terminal);

        var border = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")), Child = host };

        void ShowMessage(string message)
        {
            messageText.Text = message;
            messageBar.Visibility = Visibility.Visible;
        }

        var asset = vm.Asset;
        bool connectStarted = false;

        // 加入可视树后再发起 SSH 连接
        terminal.Loaded += async (s, e) =>
        {
            if (connectStarted) return;
            connectStarted = true;

            vm.SetStatus(SessionStatusKind.Connecting);
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
                vm.SshSession = session;
                vm.JumpConnection = jumpConnection;
                terminal.Terminal.Session = session;
                vm.SetStatus(SessionStatusKind.Connected);
                terminal.SetStatus(L10n["Status_Connected"]);
                messageBar.Visibility = Visibility.Collapsed;
                terminal.FocusTerminal();

                // 断连提示：事件可能来自后台线程，统一切回 UI 线程
                session.Disconnected += (s2, e2) => terminal.Dispatcher.BeginInvoke(new Action(() =>
                {
                    vm.SetStatus(SessionStatusKind.Disconnected);
                    ShowMessage(L10n["Term_Disconnected"]);
                    terminal.SetStatus(L10n["Status_Disconnected"]);
                }));
            }
            else
            {
                session?.Dispose();
                vm.SetStatus(SessionStatusKind.Failed);
                var failed = L10n.Format("Term_ConnectFailed", connectError ?? "");
                ShowMessage(failed);
                terminal.SetStatus(L10n["Term_StatusFailed"]);
            }
        };

        return border;
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

    private FrameworkElement CreateRdpPlaceholderView(SessionItemViewModel vm, string? password, ConnectionTemplate? template = null)
    {
        var panel = new StackPanel 
        { 
            VerticalAlignment = VerticalAlignment.Center, 
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(20)
        };

        panel.Children.Add(new TextBlock 
        { 
            Text = L10n["Layout_RdpTitle"], 
            Foreground = Brushes.White, 
            FontSize = 16, 
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        });

        panel.Children.Add(new TextBlock 
        { 
            Text = L10n.Format("Layout_RdpTarget", vm.Asset.Host, vm.Asset.Port, vm.Username), 
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0A0A0")), 
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15)
        });

        void LaunchMstsc()
        {
            try
            {
                RdpLauncher.Launch(vm.Asset, out _, template, password);
                vm.SetStatus(SessionStatusKind.Connected);
            }
            catch (Exception ex)
            {
                MessageBox.Show(L10n.Format("Msg_RdpLaunchFailed", ex.Message), L10n["Msg_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
                vm.SetStatus(SessionStatusKind.Failed);
            }
        }

        var btnLaunchMstsc = new Button 
        { 
            Content = L10n["Layout_LaunchMstsc"], 
            Style = (Style)FindResource("PrimaryButtonStyle"),
            Padding = new Thickness(12, 6, 12, 6)
        };
        btnLaunchMstsc.Click += (s, e) => LaunchMstsc();

        panel.Children.Add(btnLaunchMstsc);
        vm.SetStatus(SessionStatusKind.Connected);

        var border = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526")) };
        border.Child = panel;

        // 加入工作台即直接发起登录，免去再点一次按钮
        bool launchStarted = false;
        border.Loaded += (s, e) =>
        {
            if (launchStarted) return;
            launchStarted = true;
            LaunchMstsc();
        };

        return border;
    }

    public void SetActiveSession(SessionItemViewModel session)
    {
        bool changed = !ReferenceEquals(_activeSession, session);
        _activeSession = session;
        foreach (var s in _sessions)
        {
            s.IsFocused = (s == session);
        }
        ActiveSessionChanged?.Invoke(session);

        // 单会话布局只渲染活动会话，切换标签页后必须重建才会显示新会话
        if (changed && _layoutMode == SessionLayoutMode.Single)
        {
            RebuildGridLayout();
        }
    }

    private void RebuildGridLayout()
    {
        GridMultiContainer.Children.Clear();
        GridMultiContainer.RowDefinitions.Clear();
        GridMultiContainer.ColumnDefinitions.Clear();

        // 布局形状与容量解耦：1x2 与 2x1 容量同为 2，不能再用枚举数值当容量
        (int rows, int cols) = _layoutMode switch
        {
            SessionLayoutMode.Split1x2 => (1, 2),
            SessionLayoutMode.Split2x1 => (2, 1),
            SessionLayoutMode.Grid2x2 => (2, 2),
            SessionLayoutMode.Grid2x3 => (2, 3),
            SessionLayoutMode.Grid3x3 => (3, 3),
            _ => (1, 1)
        };
        int capacity = rows * cols;

        for (int r = 0; r < rows; r++) GridMultiContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        for (int c = 0; c < cols; c++) GridMultiContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < capacity; i++)
        {
            int r = i / cols;
            int c = i % cols;

            FrameworkElement cellContent;

            if (i < _sessions.Count)
            {
                var sessionVm = _sessions[i];

                // 如果某个单元格处于最大化状态（多宫格非单会话模式下的局部最大化，或者单会话模式且有会话）
                if (sessionVm.IsMaximized && _layoutMode != SessionLayoutMode.Single)
                {
                    // 局部最大化：只渲染该会话占满整个容器
                    GridMultiContainer.RowDefinitions.Clear();
                    GridMultiContainer.ColumnDefinitions.Clear();
                    GridMultiContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    GridMultiContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    r = 0; c = 0;

                    var cellControl = new SessionCellControl { DataContext = sessionVm };
                    cellControl.Focused += (s, e) => SetActiveSession(sessionVm);
                    cellControl.Closed += (s, e) => CloseSession(sessionVm);
                    cellControl.MaximizedToggled += (s, e) => ToggleMaximize(sessionVm);
                    cellContent = cellControl;

                    Grid.SetRow(cellContent, r);
                    Grid.SetColumn(cellContent, c);
                    GridMultiContainer.Children.Add(cellContent);
                    break; // 仅渲染最大化会话
                }
                else if (_layoutMode == SessionLayoutMode.Single)
                {
                    // 单会话模式只显示当前活动会话（没有活动会话时退回第一个）
                    var singleVm = _activeSession ?? _sessions[0];
                    var cellControl = new SessionCellControl { DataContext = singleVm };
                    cellControl.Focused += (s, e) => SetActiveSession(singleVm);
                    cellControl.Closed += (s, e) => CloseSession(singleVm);
                    cellControl.MaximizedToggled += (s, e) => ToggleMaximize(singleVm);
                    cellContent = cellControl;

                    Grid.SetRow(cellContent, 0);
                    Grid.SetColumn(cellContent, 0);
                    GridMultiContainer.Children.Add(cellContent);
                    break;
                }
                else
                {
                    var cellControl = new SessionCellControl { DataContext = sessionVm };
                    cellControl.Focused += (s, e) => SetActiveSession(sessionVm);
                    cellControl.Closed += (s, e) => CloseSession(sessionVm);
                    cellControl.MaximizedToggled += (s, e) => ToggleMaximize(sessionVm);
                    cellContent = cellControl;
                }
            }
            else
            {
                // 空余格子显示引导卡片
                cellContent = CreateEmptyCellGuide();
            }

            Grid.SetRow(cellContent, r);
            Grid.SetColumn(cellContent, c);
            GridMultiContainer.Children.Add(cellContent);
        }
    }

    private FrameworkElement CreateEmptyCellGuide()
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceBackground"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(6),
            Cursor = Cursors.Hand
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock 
        { 
            Text = L10n["Layout_EmptyCell"], 
            Foreground = (Brush)FindResource("MutedForeground"), 
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center 
        });

        border.Child = stack;
        border.MouseLeftButtonUp += (s, e) =>
        {
            MessageBox.Show(L10n["Layout_AddSessionHint"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
        };

        return border;
    }

    private void ToggleMaximize(SessionItemViewModel vm)
    {
        vm.IsMaximized = !vm.IsMaximized;
        // 如果取消最大化，重置其他
        if (!vm.IsMaximized && _layoutMode == SessionLayoutMode.Single)
        {
            _layoutMode = SessionLayoutMode.Grid2x2; // 恢复默认四宫格
        }
        RebuildGridLayout();
    }

    public void CloseSession(SessionItemViewModel vm)
    {
        vm.CloseSession();
        _sessions.Remove(vm);
        if (_activeSession == vm)
        {
            _activeSession = _sessions.LastOrDefault();
            if (_activeSession != null) SetActiveSession(_activeSession);
        }
        RebuildGridLayout();
    }

    private void TabItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is SessionItemViewModel vm)
        {
            SetActiveSession(vm);
        }
    }

    private void TabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SessionItemViewModel vm)
        {
            CloseSession(vm);
            e.Handled = true;
        }
    }

    private void BtnCloseAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var s in _sessions.ToList())
        {
            s.CloseSession();
        }
        _sessions.Clear();
        RebuildGridLayout();
    }

    private void BtnLayoutSingle_Click(object sender, RoutedEventArgs e) 
    { 
        foreach (var s in _sessions) s.IsMaximized = false;
        _layoutMode = SessionLayoutMode.Single; 
        RebuildGridLayout(); 
    }
    private void BtnLayout1x2_Click(object sender, RoutedEventArgs e)
    {
        foreach (var s in _sessions) s.IsMaximized = false;
        _layoutMode = SessionLayoutMode.Split1x2;
        RebuildGridLayout();
    }
    private void BtnLayout2x1_Click(object sender, RoutedEventArgs e)
    {
        foreach (var s in _sessions) s.IsMaximized = false;
        _layoutMode = SessionLayoutMode.Split2x1;
        RebuildGridLayout();
    }
    private void BtnLayout2x2_Click(object sender, RoutedEventArgs e) 
    { 
        foreach (var s in _sessions) s.IsMaximized = false;
        _layoutMode = SessionLayoutMode.Grid2x2; 
        RebuildGridLayout(); 
    }
    private void BtnLayout2x3_Click(object sender, RoutedEventArgs e) 
    { 
        foreach (var s in _sessions) s.IsMaximized = false;
        _layoutMode = SessionLayoutMode.Grid2x3; 
        RebuildGridLayout(); 
    }
    private void BtnLayout3x3_Click(object sender, RoutedEventArgs e) 
    { 
        foreach (var s in _sessions) s.IsMaximized = false;
        _layoutMode = SessionLayoutMode.Grid3x3; 
        RebuildGridLayout(); 
    }

    private void BtnFitWindow_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(L10n["Msg_FitWindowDone"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnFullscreen_Click(object sender, RoutedEventArgs e)
    {
        RequestFullscreenToggle?.Invoke();
    }

    /// <summary>全屏状态由主窗口统一管理，这里只同步按钮文案。</summary>
    public void SetFullscreenState(bool isFullscreen)
    {
        BtnFullscreen.SetResourceReference(ContentControl.ContentProperty,
            isFullscreen ? ResLayoutExitFullscreen : ResLayoutFullscreen);
    }

    private void BtnSettingsDrawer_Click(object sender, RoutedEventArgs e)
    {
        RequestOpenSettingsDrawer?.Invoke();
    }

    private void BtnDisconnectActive_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSession != null)
        {
            CloseSession(_activeSession);
        }
        else
        {
            MessageBox.Show(L10n["Layout_NoActiveSession"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
