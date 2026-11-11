using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RemotePilot.Models;
using RemotePilot.Services;
using RemotePilot.ViewModels;
using Microsoft.Win32;

namespace RemotePilot;

public enum AssetStatusKind { NotProbed, Online, Offline, Probing, Latency }

public class ServerAssetViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private AssetStatusKind _statusKind = AssetStatusKind.NotProbed;
    private int _latencyMs;
    private bool _isExpiredOrNear;

    public ServerAsset Model { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public Guid Id => Model.Id;
    public string Name => Model.Name;
    public string Host => Model.Host;
    public int Port => Model.Port;
    public string Protocol => Model.Protocol;
    public string Username => Model.Username;
    public string Group => string.IsNullOrWhiteSpace(Model.Group) ? LocalizationManager.Instance["Group_Default"] : Model.Group;
    public string TemplateName { get; set; } = "-";
    public string Owner => Model.Owner;
    public DateOnly? ExpiresOn => Model.ExpiresOn;
    public bool IsFavorite => Model.IsFavorite;
    public DateTimeOffset? LastAccessedAt => Model.LastAccessedAt;
    public string CustomNote1 => Model.CustomNote1;
    public string CustomNote2 => Model.CustomNote2;
    public string CustomNote3 => Model.CustomNote3;
    public string FavoriteGlyph => Model.IsFavorite ? "★" : "☆";
    public Brush FavoriteBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Model.IsFavorite ? "#D4A72C" : "#8B949E"));

    public void NotifyFavoriteChanged()
    {
        OnPropertyChanged(nameof(FavoriteGlyph));
        OnPropertyChanged(nameof(FavoriteBrush));
    }
    public string IpPortDisplay => $"{Model.Host}:{Model.Port}";
    public string ExpiresOnDisplay => Model.ExpiresOn.HasValue ? Model.ExpiresOn.Value.ToString("yyyy-MM-dd") : "-";

    public AssetStatusKind StatusKind => _statusKind;

    public string StatusDisplay => _statusKind switch
    {
        AssetStatusKind.Online => LocalizationManager.Instance["Status_Online"],
        AssetStatusKind.Offline => LocalizationManager.Instance["Status_Offline"],
        AssetStatusKind.Probing => LocalizationManager.Instance["Status_Probing"],
        AssetStatusKind.Latency => $"{_latencyMs}ms",
        _ => LocalizationManager.Instance["Status_NotProbed"]
    };

    public void SetStatus(AssetStatusKind kind, int latencyMs = 0)
    {
        _statusKind = kind;
        _latencyMs = latencyMs;
        OnPropertyChanged(nameof(StatusKind));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(StatusBadgeBackground));
        OnPropertyChanged(nameof(StatusBadgeForeground));
    }

    public Brush StatusBadgeBackground
    {
        get
        {
            bool isDark = ThemeManager.Instance.IsDark;
            if (_statusKind == AssetStatusKind.Online || _statusKind == AssetStatusKind.Latency)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#14532D" : "#E6F4EA"));
            if (_statusKind == AssetStatusKind.Offline)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#7F1D1D" : "#FCE8E6"));
            if (_statusKind == AssetStatusKind.Probing)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#78350F" : "#FEF7E0"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#374151" : "#F1F3F4"));
        }
    }

    public Brush StatusBadgeForeground
    {
        get
        {
            bool isDark = ThemeManager.Instance.IsDark;
            if (_statusKind == AssetStatusKind.Online || _statusKind == AssetStatusKind.Latency)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#86EFAC" : "#137333"));
            if (_statusKind == AssetStatusKind.Offline)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#FCA5A5" : "#C5221F"));
            if (_statusKind == AssetStatusKind.Probing)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#FDE047" : "#B06000"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#E5E7EB" : "#5F6368"));
        }
    }

    public void NotifyThemeChanged()
    {
        OnPropertyChanged(nameof(StatusBadgeBackground));
        OnPropertyChanged(nameof(StatusBadgeForeground));
    }

    /// <summary>界面语言切换后刷新依赖语言的展示文案。</summary>
    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(Group));
    }

    public bool IsExpiredOrNear
    {
        get => _isExpiredOrNear;
        set
        {
            if (_isExpiredOrNear != value)
            {
                _isExpiredOrNear = value;
                OnPropertyChanged();
            }
        }
    }

    public ServerAssetViewModel(ServerAsset model)
    {
        Model = model;
        UpdateExpiryStatus();
    }

    public void UpdateExpiryStatus()
    {
        if (Model.ExpiresOn.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var daysRemaining = Model.ExpiresOn.Value.DayNumber - today.DayNumber;
            IsExpiredOrNear = daysRemaining <= 7;
        }
        else
        {
            IsExpiredOrNear = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public partial class MainWindow : Window
{
    private readonly AssetStore _assetStore;
    private readonly ReachabilityProbe _probe;
    private readonly ObservableCollection<ServerAssetViewModel> _allAssets = [];
    private readonly ObservableCollection<ServerAssetViewModel> _viewAssets = [];
    private string _activeGroupFilter = "";
    private string _searchKeyword = "";
    private string _inspectorProtocol = "rdp";
    private NavMode _navMode = NavMode.Assets;
    private bool _isGridView;
    private bool _rememberPassword;
    private bool _suppressRememberToggle;
    private string _assetApiUrl = "";
    private bool _isWorkspaceFullscreen;
    private bool _detailsWasVisibleBeforeFullscreen;
    private WindowState _windowStateBeforeFullscreen = WindowState.Normal;
    /// <summary>左侧导航是否折叠为图标条（持久化）。</summary>
    private bool _leftNavCollapsed;
    private const double LeftNavExpandedWidth = 220;
    private const double LeftNavCollapsedWidth = 56;
    /// <summary>抑制表头三态/计数联动，避免程序化改选时事件递归。</summary>
    private bool _suppressSelectAllSync;
    /// <summary>顶栏「设备重定向」是否被用户主动改动过；改动后作为本次连接的显式覆盖。</summary>
    private bool _redirectToolbarTouched;

    /// <summary>已就认证失败弹过一次凭据补录的资产，避免同一会话反复弹窗（每个资产仅提示一次）。</summary>
    private readonly HashSet<Guid> _authRetryPrompted = [];

    private enum NavMode { Assets, Sessions, Favorites, Recent, Groups }

    // Localization Keys
    public const string ResNavAssets = "Nav_Assets";
    public const string ResNavSessions = "Nav_Sessions";
    public const string ResNavFavorites = "Nav_Favorites";
    public const string ResNavRecent = "Nav_Recent";
    public const string ResNavGroups = "Nav_Groups";
    public const string ResNavFeedback = "Nav_Feedback";
    public const string ResBtnToggleLeftNav = "Btn_ToggleLeftNav";
    public const string ResBtnToggleLeftNavExpand = "Btn_ToggleLeftNavExpand";
    public const string ResBtnSearch = "Btn_Search";
    public const string ResBtnAddServer = "Btn_AddServer";
    public const string ResBtnImport = "Btn_Import";
    public const string ResBtnExportSelected = "Btn_ExportSelected";
    public const string ResBtnInvertSelect = "Btn_InvertSelect";
    public const string ResBtnDelete = "Btn_Delete";
    public const string ResBtnOpen = "Btn_Open";
    public const string ResBtnShowAll = "Btn_ShowAll";
    public const string ResBtnSyncCloud = "Btn_SyncCloud";
    public const string ResBtnSaveSettings = "Btn_SaveSettings";
    public const string ResBtnProbe = "Btn_Probe";
    public const string ResAppTitle = "App_Title";
    public const string ResThemeToggle = "Theme_Toggle";
    public const string ResLangToggle = "Lang_Toggle";
    public const string ResBtnViewTable = "Btn_ViewTable";
    public const string ResBtnViewGrid = "Btn_ViewGrid";
    public const string ResBtnOpenDrawer = "Btn_OpenDrawer";
    public const string ResBtnEdit = "Btn_Edit";
    public const string ResBtnRemove = "Btn_Remove";
    public const string ResBtnQuickConnect = "Btn_QuickConnect";
    public const string ResBtnExpiryBanner = "Btn_ExpiryBanner";
    public const string ResBtnBatchProbe = "Btn_BatchProbe";
    public const string ResTipFavorite = "Tip_Favorite";
    public const string ResSettingWindowMode = "Setting_WindowMode";
    public const string ResSettingModeNormal = "Setting_ModeNormal";
    public const string ResSettingModeFullscreen = "Setting_ModeFullscreen";
    public const string ResSettingModeSmall = "Setting_ModeSmall";
    public const string ResSettingModeTab = "Setting_ModeTab";
    public const string ResSettingDeviceControl = "Setting_DeviceControl";
    public const string ResSettingDriveMap = "Setting_DriveMap";
    public const string ResSettingAudio = "Setting_Audio";
    public const string ResSettingClipboard = "Setting_Clipboard";
    public const string ResSettingColorDepth = "Setting_ColorDepth";
    public const string ResTipDeviceRedirect = "Tip_DeviceRedirect";
    public const string ResSettingBit8 = "Setting_Bit8";
    public const string ResSettingBit15 = "Setting_Bit15";
    public const string ResSettingBit16 = "Setting_Bit16";
    public const string ResSettingBit24 = "Setting_Bit24";
    public const string ResSettingBit32 = "Setting_Bit32";
    public const string ResCardOwner = "Card_Owner";
    public const string ResCardExpires = "Card_Expires";
    public const string ResPanelDetails = "Panel_Details";
    public const string ResPanelCollapse = "Panel_Collapse";
    public const string ResPanelExpand = "Panel_Expand";
    public const string ResPanelOverview = "Panel_Overview";
    public const string ResInspExpiryWarn = "Insp_ExpiryWarn";
    public const string ResInspLastProbe = "Insp_LastProbe";
    public const string ResInspPlaceholder = "Insp_Placeholder";
    public const string ResDiagTitle = "Diag_Title";
    public const string ResDiagReason = "Diag_Reason";
    public const string ResDiagAdvice = "Diag_Advice";
    public const string ResDiagAdviceDetail = "Diag_AdviceDetail";
    public const string ResDiagRetry = "Diag_Retry";
    public const string ResSettingResolution = "Setting_Resolution";
    public const string ResBtnScreen14 = "Btn_Screen14";
    public const string ResBtnScreen16 = "Btn_Screen16";
    public const string ResBtnScreen19 = "Btn_Screen19";
    public const string ResBtnSplitH = "Btn_SplitH";
    public const string ResBtnSplitV = "Btn_SplitV";
    public const string ResBtnResetResolution = "Btn_ResetResolution";
    public const string ResSettingProbeInterval = "Setting_ProbeInterval";
    public const string ResSettingRememberPassword = "Setting_RememberPassword";
    public const string ResSettingRememberPasswordHint = "Setting_RememberPasswordHint";
    public const string ResStatusReady = "Status_Ready";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    public MainWindow()
    {
        InitializeComponent();

        // 顶栏「设备重定向」被用户主动改动后，作为本次连接的显式覆盖
        ChkDriveMap.Checked += RedirectToolbar_Changed;
        ChkDriveMap.Unchecked += RedirectToolbar_Changed;
        ChkAudio.Checked += RedirectToolbar_Changed;
        ChkAudio.Unchecked += RedirectToolbar_Changed;
        ChkClipboard.Checked += RedirectToolbar_Changed;
        ChkClipboard.Unchecked += RedirectToolbar_Changed;

        _assetStore = new AssetStore();
        _probe = new ReachabilityProbe(10);
        _probe.ProbeCompleted += OnProbeCompleted;

        GridAssets.ItemsSource = _viewAssets;
        ItemsCardsGrid.ItemsSource = _viewAssets;

        ThemeManager.Instance.ThemeChanged += (s, e) => {
            foreach (var vm in _allAssets)
            {
                vm.NotifyThemeChanged();
            }
        };
        LocalizationManager.Instance.LanguageChanged += (s, e) =>
        {
            // 状态文案、分组默认名与分组徽章都依赖当前语言，切换后重算一次
            foreach (var vm in _allAssets) vm.NotifyLanguageChanged();
            ApplyFilter();
            BtnToggleLeftNav.ToolTip = L10n[_leftNavCollapsed ? "Tip_ExpandLeftNav" : "Tip_CollapseLeftNav"];
        };

        LoadAssetsFromStore();
        UpdateNavSelection();

        // 绑定抽屉回调与工作台事件
        SettingsDrawer.Applied += (updatedAsset) =>
        {
            var existingVm = _allAssets.FirstOrDefault(a => a.Id == updatedAsset.Id);
            if (existingVm != null)
            {
                existingVm.Model = updatedAsset;
                existingVm.UpdateExpiryStatus();
            }
            SaveAssetsToStore();
            ApplyFilter();
            TxtStatusBarInfo.Text = L10n["Status_DrawerApplied"];
        };
        SettingsDrawer.TemplateSaved += (newTmpl) =>
        {
            var data = _assetStore.Load();
            var updatedTemplates = data.Templates.Append(newTmpl).ToList();
            _assetStore.Save(data with { Templates = updatedTemplates });
            TxtStatusBarInfo.Text = L10n.Format("Status_TemplateSaved", newTmpl.Name);
        };

        SessionWorkspace.RequestOpenSettingsDrawer += () =>
        {
            var target = _viewAssets.FirstOrDefault(a => a.IsSelected) ?? (GridAssets.SelectedItem as ServerAssetViewModel);
            if (target != null)
            {
                var storeData = _assetStore.Load();
                SettingsDrawer.BindAsset(target.Model, storeData.Templates);
            }
            else
            {
                MessageBox.Show(L10n["Msg_SelectAssetForDrawer"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };

        SessionWorkspace.RequestFullscreenToggle += ToggleWorkspaceFullscreen;
        SessionWorkspace.SessionConnectFailed += OnSessionConnectFailed;
    }

    /// <summary>全屏只保留会话工作台：左侧导航、顶部三栏、右侧详情面板全部隐藏，退出时精确恢复原始状态。</summary>
    private void ToggleWorkspaceFullscreen()
    {
        if (!_isWorkspaceFullscreen)
        {
            _windowStateBeforeFullscreen = WindowState;
            _detailsWasVisibleBeforeFullscreen = ColDetailsPanel.Width.Value > 0;

            // 全屏一定是操作会话，先切到会话工作台
            _navMode = NavMode.Sessions;
            GridAssets.Visibility = Visibility.Collapsed;
            ScrollCardsArea.Visibility = Visibility.Collapsed;
            SessionWorkspace.Visibility = Visibility.Visible;
            UpdateNavSelection();

            ColLeftNav.Width = new GridLength(0);
            BorderLeftNav.Visibility = Visibility.Collapsed;
            RowTopCommand.Height = new GridLength(0);
            RowTopSettings.Height = new GridLength(0);
            RowTopGroup.Height = new GridLength(0);
            BorderTopCommand.Visibility = Visibility.Collapsed;
            BorderTopSettings.Visibility = Visibility.Collapsed;
            BorderTopGroup.Visibility = Visibility.Collapsed;
            SetDetailsPanelCollapsed(true);

            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isWorkspaceFullscreen = true;
            SessionWorkspace.SetFullscreenState(true);
            TxtStatusBarInfo.Text = L10n["Status_EnterFullscreen"];
        }
        else
        {
            // 依据持久化的折叠状态恢复左侧导航（宽度 + 图标/文字可见性），不要硬编码 220
            ApplyLeftNavState(_leftNavCollapsed);
            RowTopCommand.Height = new GridLength(56);
            RowTopSettings.Height = GridLength.Auto;
            RowTopGroup.Height = GridLength.Auto;
            BorderTopCommand.Visibility = Visibility.Visible;
            BorderTopSettings.Visibility = Visibility.Visible;
            BorderTopGroup.Visibility = Visibility.Visible;
            if (_detailsWasVisibleBeforeFullscreen) SetDetailsPanelCollapsed(false);

            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _windowStateBeforeFullscreen == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
            _isWorkspaceFullscreen = false;
            SessionWorkspace.SetFullscreenState(false);
            TxtStatusBarInfo.Text = L10n["Status_ExitFullscreen"];
        }
    }

    private void BtnOpenSettingsDrawer_Click(object sender, RoutedEventArgs e)
    {
        var target = _viewAssets.FirstOrDefault(a => a.IsSelected) ?? (GridAssets.SelectedItem as ServerAssetViewModel);
        if (target == null)
        {
            MessageBox.Show(L10n["Msg_SelectAssetForDrawer"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var storeData = _assetStore.Load();
        SettingsDrawer.BindAsset(target.Model, storeData.Templates);
    }

    private void BtnTemplateName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerAssetViewModel vm)
        {
            // 取消其他资产的选中状态
            foreach (var asset in _viewAssets)
            {
                asset.IsSelected = false;
            }
            
            // 选中当前资产
            vm.IsSelected = true;
            GridAssets.SelectedItem = vm;
            
            // 打开连接方案抽屉
            var storeData = _assetStore.Load();
            SettingsDrawer.BindAsset(vm.Model, storeData.Templates);
        }
    }

    private void BtnToggleTheme_Click(object sender, RoutedEventArgs e) => ThemeManager.Instance.ToggleTheme();
    private void BtnToggleLang_Click(object sender, RoutedEventArgs e) => LocalizationManager.Instance.ToggleLanguage();

    private void BtnToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        SetDetailsPanelCollapsed(ColDetailsPanel.Width.Value > 0);
    }

    /// <summary>收起时整块隐藏；单击列表行会重新展开。</summary>
    private void SetDetailsPanelCollapsed(bool collapsed)
    {
        ColDetailsPanel.Width = new GridLength(collapsed ? 0 : 300);
        BorderRightPanel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        DetailsScroll.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        DetailsFooter.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        TxtDetailsTitle.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        BtnToggleDetails.Content = collapsed ? "◀" : L10n["Btn_Collapse"];
        BtnToggleDetails.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
    }

    private void EnsureDetailsPanelExpanded()
    {
        if (ColDetailsPanel.Width.Value <= 0 || DetailsScroll.Visibility != Visibility.Visible)
        {
            SetDetailsPanelCollapsed(false);
        }
    }

    /// <summary>切换左侧导航折叠状态：更新布局并持久化。</summary>
    private void BtnToggleLeftNav_Click(object sender, RoutedEventArgs e)
    {
        ApplyLeftNavState(!_leftNavCollapsed);
        PersistLeftNavCollapsed(_leftNavCollapsed);
    }

    /// <summary>收起态下鼠标进入导航栏临时展开（不改变持久化状态），离开后恢复。</summary>
    private void BorderLeftNav_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_isWorkspaceFullscreen || !_leftNavCollapsed) return;
        ApplyLeftNavVisual(false);
        ColLeftNav.Width = new GridLength(LeftNavExpandedWidth);
    }

    private void BorderLeftNav_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isWorkspaceFullscreen || !_leftNavCollapsed) return;
        ApplyLeftNavVisual(true);
        ColLeftNav.Width = new GridLength(LeftNavCollapsedWidth);
    }

    /// <summary>统一应用左侧导航状态：宽度、图标/文字可见性与折叠按钮外观。</summary>
    private void ApplyLeftNavState(bool collapsed)
    {
        _leftNavCollapsed = collapsed;
        ApplyLeftNavVisual(collapsed);
        ColLeftNav.Width = new GridLength(collapsed ? LeftNavCollapsedWidth : LeftNavExpandedWidth);
        BorderLeftNav.Visibility = Visibility.Visible;
    }

    /// <summary>仅切换导航栏内部元素的可见性与内外边距（不涉及列宽），供折叠/悬停临时展开复用。</summary>
    private void ApplyLeftNavVisual(bool collapsed)
    {
        var textVis = collapsed ? Visibility.Collapsed : Visibility.Visible;

        PanelLogoRow.Visibility = textVis;
        PanelLogoText.Visibility = textVis;
        PanelNavBottom.Visibility = textVis;

        TxtNavAssetsLabel.Visibility = textVis;
        TxtNavSessionsLabel.Visibility = textVis;
        TxtNavFavoritesLabel.Visibility = textVis;
        TxtNavRecentLabel.Visibility = textVis;
        TxtNavGroupsLabel.Visibility = textVis;
        TxtNavGroupsChevron.Visibility = textVis;

        if (collapsed) PanelNavGroups.Visibility = Visibility.Collapsed;

        var align = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        var padding = collapsed ? new Thickness(0, 14, 0, 14) : new Thickness(14, 14, 14, 14);
        foreach (var btn in new[] { NavBtnAssets, NavBtnSessions, NavBtnFavorites, NavBtnRecent })
        {
            btn.HorizontalContentAlignment = align;
            btn.Padding = padding;
        }
        // 分组按钮展开态需要 Stretch 以便箭头靠右对齐
        NavBtnGroups.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        NavBtnGroups.Padding = padding;

        TxtLeftNavGlyphCollapse.Visibility = textVis;
        TxtLeftNavGlyphExpand.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleLeftNav.HorizontalAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        BtnToggleLeftNav.ToolTip = L10n[collapsed ? "Tip_ExpandLeftNav" : "Tip_CollapseLeftNav"];
    }

    /// <summary>把折叠状态写入本地存储（保留其余字段）。</summary>
    private void PersistLeftNavCollapsed(bool collapsed)
    {
        var data = _assetStore.Load();
        _assetStore.Save(data with { IsLeftNavCollapsed = collapsed });
    }

    private void ShowAssetsArea(bool gridView)
    {
        _isGridView = gridView;
        SessionWorkspace.Visibility = Visibility.Collapsed;
        GridAssets.Visibility = gridView ? Visibility.Collapsed : Visibility.Visible;
        ScrollCardsArea.Visibility = gridView ? Visibility.Visible : Visibility.Collapsed;

        BtnViewTable.Background = (SolidColorBrush)Application.Current.Resources[gridView ? "SurfaceBackground" : "Primary"];
        BtnViewTable.Foreground = gridView ? (Brush)Application.Current.Resources["AppForeground"] : Brushes.White;
        BtnViewGrid.Background = (SolidColorBrush)Application.Current.Resources[gridView ? "Primary" : "SurfaceBackground"];
        BtnViewGrid.Foreground = gridView ? Brushes.White : (Brush)Application.Current.Resources["AppForeground"];
    }

    private void BtnViewTable_Click(object sender, RoutedEventArgs e) => ShowAssetsArea(false);

    private void BtnViewGrid_Click(object sender, RoutedEventArgs e) => ShowAssetsArea(true);

    private void BtnCardConnect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerAssetViewModel vm)
        {
            ConnectServer(vm);
        }
    }

    private void BtnCardEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerAssetViewModel vm)
        {
            var dlg = CreateServerEditDialog(vm.Model);
            if (dlg.ShowDialog() == true)
            {
                var updatedModel = ApplyRememberPassword(dlg.Asset, dlg.RememberPassword);

                vm.Model = updatedModel;
                vm.UpdateExpiryStatus();
                vm.NotifyFavoriteChanged();
                SaveAssetsToStore();
                ApplyFilter();
            }
        }
    }

    private void NavBtnAssets_Click(object sender, RoutedEventArgs e)
    {
        _navMode = NavMode.Assets;
        _activeGroupFilter = "";
        ApplyFilter();
        ShowAssetsArea(_isGridView);
        UpdateNavSelection();
    }

    private void NavBtnSessions_Click(object sender, RoutedEventArgs e)
    {
        _navMode = NavMode.Sessions;
        GridAssets.Visibility = Visibility.Collapsed;
        ScrollCardsArea.Visibility = Visibility.Collapsed;
        SessionWorkspace.Visibility = Visibility.Visible;
        UpdateNavSelection();
    }

    private void NavBtnFavorites_Click(object sender, RoutedEventArgs e)
    {
        _navMode = NavMode.Favorites;
        _activeGroupFilter = "";
        ApplyFilter();
        ShowAssetsArea(_isGridView);
        UpdateNavSelection();
    }

    private void NavBtnRecent_Click(object sender, RoutedEventArgs e)
    {
        _navMode = NavMode.Recent;
        _activeGroupFilter = "";
        ApplyFilter();
        ShowAssetsArea(_isGridView);
        UpdateNavSelection();
    }

    private void NavBtnGroups_Click(object sender, RoutedEventArgs e)
    {
        bool expand = PanelNavGroups.Visibility != Visibility.Visible;
        PanelNavGroups.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
        TxtNavGroupsChevron.Text = expand ? "▾" : "▸";

        if (!expand) return; // 收起时保持当前视图

        _navMode = NavMode.Groups;
        _activeGroupFilter = "";
        ApplyFilter();
        ShowAssetsArea(_isGridView);
        UpdateNavSelection();
    }

    /// <summary>点击左侧展开的分组项，直接按该分组过滤资产。</summary>
    private void NavGroupItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string grp)
        {
            _navMode = NavMode.Groups;
            _activeGroupFilter = grp;
            ApplyFilter();
            ShowAssetsArea(_isGridView);
        }
    }

    private void UpdateNavSelection()
    {
        var buttons = new (NavMode Mode, Button Btn)[]
        {
            (NavMode.Assets, NavBtnAssets),
            (NavMode.Sessions, NavBtnSessions),
            (NavMode.Favorites, NavBtnFavorites),
            (NavMode.Recent, NavBtnRecent),
            (NavMode.Groups, NavBtnGroups)
        };

        foreach (var (mode, btn) in buttons)
        {
            bool active = mode == _navMode;
            btn.Background = active
                ? (SolidColorBrush)Application.Current.Resources["SelectedBackground"]
                : Brushes.Transparent;
            btn.Foreground = active
                ? (Brush)Application.Current.Resources["Primary"]
                : (Brush)Application.Current.Resources["AppForeground"];
            btn.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            btn.BorderBrush = (Brush)Application.Current.Resources["Primary"];
            btn.BorderThickness = active ? new Thickness(3, 0, 0, 0) : new Thickness(0);
        }
    }

    private void LoadAssetsFromStore()
    {
        var data = _assetStore.Load();
        _allAssets.Clear();

        _rememberPassword = data.RememberPassword;
        _assetApiUrl = data.AssetApiUrl;
        _suppressRememberToggle = true;
        ChkRememberPassword.IsChecked = _rememberPassword;
        _suppressRememberToggle = false;

        foreach (var asset in data.Assets)
        {
            var vm = new ServerAssetViewModel(asset);
            if (asset.LastProbeReachable.HasValue)
            {
                vm.SetStatus(asset.LastProbeReachable.Value ? AssetStatusKind.Online : AssetStatusKind.Offline);
            }
            
            // 设置模板名称
            if (asset.TemplateId.HasValue)
            {
                var template = data.Templates.FirstOrDefault(t => t.Id == asset.TemplateId.Value);
                vm.TemplateName = template?.Name ?? "-";
            }
            
            RegisterAsset(vm);
        }

        ApplyFilter();

        // 恢复上次的左侧导航折叠状态
        ApplyLeftNavState(data.IsLeftNavCollapsed);
    }

    /// <summary>把资产加入列表并订阅其选中状态变化，用于实时刷新计数与表头三态。</summary>
    private void RegisterAsset(ServerAssetViewModel vm)
    {
        vm.PropertyChanged -= OnAssetSelectionChanged; // 防止重复订阅
        vm.PropertyChanged += OnAssetSelectionChanged;
        _allAssets.Add(vm);
    }

    private void OnAssetSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSelectAllSync) return;
        if (e.PropertyName == nameof(ServerAssetViewModel.IsSelected))
        {
            UpdateSelectionSummary();
        }
    }

    /// <summary>刷新「已选 N 台」文本并同步表头三态。</summary>
    private void UpdateSelectionSummary()
    {
        int selected = _viewAssets.Count(a => a.IsSelected);
        TxtSelectedCount.Text = L10n.Format("Status_SelectedCount", selected);

        var chk = GetSelectAllHeaderCheckBox();
        if (chk == null) return;

        int total = _viewAssets.Count;
        _suppressSelectAllSync = true;
        try
        {
            chk.IsChecked = (total == 0 || selected == 0) ? false
                : (selected == total ? true : null);
        }
        finally
        {
            _suppressSelectAllSync = false;
        }
    }

    /// <summary>从 DataGrid 列头取三态复选框（列头 x:Name 无法在代码后置直接访问）。</summary>
    private CheckBox? GetSelectAllHeaderCheckBox()
    {
        foreach (var col in GridAssets.Columns)
        {
            if (col.Header is CheckBox chk) return chk;
        }
        return null;
    }

    private void SaveAssetsToStore()
    {
        var storeData = _assetStore.Load();
        var models = _allAssets.Select(vm => vm.Model).ToList();
        _assetStore.Save(storeData with { Assets = models, AssetApiUrl = _assetApiUrl });
    }

    private void ApplyFilter()
    {
        _viewAssets.Clear();

        IEnumerable<ServerAssetViewModel> query = _allAssets;

        switch (_navMode)
        {
            case NavMode.Favorites:
                query = query.Where(a => a.Model.IsFavorite);
                break;
            case NavMode.Recent:
                query = query.Where(a => a.Model.LastAccessedAt.HasValue)
                             .OrderByDescending(a => a.Model.LastAccessedAt);
                break;
            case NavMode.Groups:
                query = query.OrderBy(a => a.Group).ThenBy(a => a.Name);
                break;
        }

        if (!string.IsNullOrWhiteSpace(_activeGroupFilter))
        {
            query = query.Where(a => string.Equals(a.Group, _activeGroupFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_searchKeyword))
        {
            query = query.Where(a => 
                a.Name.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ||
                a.Host.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ||
                a.Group.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ||
                a.Owner.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ||
                a.CustomNote1.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ||
                a.CustomNote2.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ||
                a.CustomNote3.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            _viewAssets.Add(item);
        }

        // 筛选切换后重置所有勾选，避免残留选中状态
        _suppressSelectAllSync = true;
        foreach (var a in _allAssets)
        {
            a.IsSelected = false;
        }
        _suppressSelectAllSync = false;

        RefreshGroupBadges();
        UpdateSelectionSummary();

        TxtStatusBarInfo.Text = _navMode switch
        {
            NavMode.Favorites => L10n.Format("Status_FavoritesView", _viewAssets.Count),
            NavMode.Recent => L10n.Format("Status_RecentView", _viewAssets.Count),
            NavMode.Groups => L10n.Format("Status_GroupsView", _allAssets.Select(a => a.Group).Distinct().Count(), _viewAssets.Count),
            _ => L10n.Format("Status_Total", _allAssets.Count, _viewAssets.Count)
        };
    }

    private void RefreshGroupBadges()
    {
        PanelGroupBadges.Children.Clear();

        // 检查临期或已过期资产数量
        var expiring = _allAssets.Where(a => a.IsExpiredOrNear).ToList();
        if (expiring.Count > 0)
        {
            BtnExpirySummaryBanner.Visibility = Visibility.Visible;
            BtnExpirySummaryBanner.Content = L10n.Format("Status_ExpiryBanner", expiring.Count);
            // 悬停列出「名称 (IP:Port) 到期日」
            BtnExpirySummaryBanner.ToolTip = string.Join("\n", expiring.Select(a =>
                L10n.Format("Tip_ExpiryBannerItem", a.Name, a.IpPortDisplay, a.ExpiresOnDisplay)));
        }
        else
        {
            BtnExpirySummaryBanner.Visibility = Visibility.Collapsed;
            BtnExpirySummaryBanner.ToolTip = null;
        }

        var primary = (SolidColorBrush)Application.Current.Resources["Primary"];
        var normalBg = (SolidColorBrush)Application.Current.Resources["SurfaceBackground"];
        var borderBrush = (Brush)Application.Current.Resources["BorderBrush"];
        var fg = (Brush)Application.Current.Resources["AppForeground"];

        // 选中的分组 Tab 使用主色底 + 白字 + 加粗 + 更大字号/内边距，突出当前分组
        Button CreateBadge(string content, string tag, bool active)
        {
            var btn = new Button
            {
                Content = content,
                Margin = active ? new Thickness(4, 0, 4, 0) : new Thickness(4, 2, 4, 2),
                Padding = active ? new Thickness(16, 7, 16, 7) : new Thickness(10, 4, 10, 4),
                Background = active ? primary : normalBg,
                Foreground = active ? Brushes.White : fg,
                BorderBrush = active ? primary : borderBrush,
                BorderThickness = new Thickness(1),
                FontWeight = active ? FontWeights.Bold : FontWeights.Normal,
                FontSize = active ? 14 : 12,
                Cursor = Cursors.Hand,
                Tag = tag
            };
            btn.Click += GroupBadge_Click;
            return btn;
        }

        PanelGroupBadges.Children.Add(CreateBadge(
            L10n.Format("Tag_Total", _allAssets.Count),
            "",
            string.IsNullOrEmpty(_activeGroupFilter)));

        var groups = _allAssets
            .GroupBy(a => a.Group)
            .OrderBy(g => g.Key);

        foreach (var grp in groups)
        {
            PanelGroupBadges.Children.Add(CreateBadge(
                $"{grp.Key}({grp.Count()})",
                grp.Key,
                string.Equals(_activeGroupFilter, grp.Key, StringComparison.OrdinalIgnoreCase)));
        }

        RefreshNavGroups();
    }

    /// <summary>同步左侧「服务器分组」展开区域的分组列表。</summary>
    private void RefreshNavGroups()
    {
        PanelNavGroups.Children.Clear();

        var fg = (Brush)Application.Current.Resources["AppForeground"];
        Button CreateItem(string content, string tag)
        {
            bool active = string.Equals(_activeGroupFilter, tag, StringComparison.OrdinalIgnoreCase);
            var btn = new Button
            {
                Content = content,
                Tag = tag,
                Margin = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(10, 6, 8, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = active ? (Brush)Application.Current.Resources["Primary"] : fg,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            btn.Click += NavGroupItem_Click;
            return btn;
        }

        PanelNavGroups.Children.Add(CreateItem(L10n.Format("Tag_Total", _allAssets.Count), ""));

        foreach (var grp in _allAssets.GroupBy(a => a.Group).OrderBy(g => g.Key))
        {
            PanelNavGroups.Children.Add(CreateItem($"{grp.Key} ({grp.Count()})", grp.Key));
        }
    }

    private void BtnExpirySummaryBanner_Click(object sender, RoutedEventArgs e)
    {
        // 一键过滤并展示临期/过期资产
        _activeGroupFilter = "";
        _searchKeyword = "";
        TxtSearchKeyword.Text = "";

        _viewAssets.Clear();
        foreach (var item in _allAssets.Where(a => a.IsExpiredOrNear))
        {
            _viewAssets.Add(item);
        }

        // 过滤后重置勾选，保持表头三态与计数一致
        _suppressSelectAllSync = true;
        foreach (var a in _allAssets)
        {
            a.IsSelected = false;
        }
        _suppressSelectAllSync = false;
        UpdateSelectionSummary();

        TxtStatusBarInfo.Text = L10n.Format("Status_ExpiryFilter", _viewAssets.Count);
    }

    private void BtnRetryConnection_Click(object sender, RoutedEventArgs e)
    {
        var target = _viewAssets.FirstOrDefault(a => a.IsSelected) ?? (GridAssets.SelectedItem as ServerAssetViewModel);
        if (target != null)
        {
            TxtStatusBarInfo.Text = L10n.Format("Status_Retrying", target.Name, target.IpPortDisplay);
            ConnectServer(target);
        }
        else
        {
            MessageBox.Show(L10n["Msg_SelectAssetForRetry"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void GroupBadge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string grp)
        {
            _activeGroupFilter = grp;
            ApplyFilter();
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        _searchKeyword = TxtSearchKeyword.Text.Trim();
        ApplyFilter();
    }

    private void TxtSearchKeyword_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnSearch_Click(sender, e);
        }
    }

    private void BtnShowAll_Click(object sender, RoutedEventArgs e)
    {
        _activeGroupFilter = "";
        _searchKeyword = "";
        TxtSearchKeyword.Text = "";
        ApplyFilter();
    }

    /// <summary>表头三态复选框：全选/取消全选当前可见行。</summary>
    private void ChkSelectAllHeader_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectAllSync) return;
        bool allSelected = _viewAssets.Count > 0 && _viewAssets.All(a => a.IsSelected);
        foreach (var vm in _viewAssets)
        {
            vm.IsSelected = !allSelected;
        }
        UpdateSelectionSummary();
    }

    /// <summary>反选当前可见行。</summary>
    private void BtnInvertSelect_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in _viewAssets)
        {
            vm.IsSelected = !vm.IsSelected;
        }
        UpdateSelectionSummary();
    }

    /// <summary>单击行即选中：命中测试交互控件/双击时跳过，普通点击则单选当前行。</summary>
    private void GridAssets_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        if (IsInteractiveElement(src)) return; // 点击行内交互控件，交给控件自身处理
        if (e.ClickCount == 2) return;         // 双击交给连接逻辑

        var row = FindAncestorRow(src);
        if (row?.DataContext is not ServerAssetViewModel vm) return;

        bool additive = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
        if (!additive)
        {
            foreach (var other in _viewAssets)
            {
                if (!ReferenceEquals(other, vm))
                {
                    other.IsSelected = false;
                }
            }
            GridAssets.SelectedItem = vm;
        }
        vm.IsSelected = true;
        UpdateSelectionSummary();
    }

    /// <summary>命中测试：e.OriginalSource 是否落在行内的交互控件（Button/CheckBox/ComboBox/TextBox/超链接）上。</summary>
    private static bool IsInteractiveElement(DependencyObject source)
    {
        var cursor = source;
        while (cursor != null && cursor is not DataGridRow)
        {
            if (cursor is ButtonBase || cursor is TextBox || cursor is ComboBox
                || cursor is System.Windows.Documents.Hyperlink)
            {
                return true;
            }
            cursor = VisualTreeHelper.GetParent(cursor);
        }
        return false;
    }

    /// <summary>从命中元素向上查找所属 DataGridRow。</summary>
    private static DataGridRow? FindAncestorRow(DependencyObject source)
    {
        var cursor = source;
        while (cursor != null && cursor is not DataGridRow)
        {
            cursor = VisualTreeHelper.GetParent(cursor);
        }
        return cursor as DataGridRow;
    }

    private void BtnAddServer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ServerEditDialog(null, CollectGroups(), _rememberPassword, CollectAssetModels(), CollectTemplates()) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var newModel = ApplyRememberPassword(dlg.Asset, dlg.RememberPassword);

            var vm = new ServerAssetViewModel(newModel);
            RegisterAsset(vm);
            SaveAssetsToStore();
            ApplyFilter();
        }
    }

    /// <summary>新增/编辑对话框的分组下拉数据：现有资产里出现过的分组。</summary>
    private List<string> CollectGroups() =>
        _allAssets.Select(a => a.Group).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().ToList();

    /// <summary>跳板机下拉数据：只有 SSH 类型的资产才能作为中转。</summary>
    private List<ServerAsset> CollectAssetModels() =>
        _allAssets.Select(a => a.Model)
            .Where(a => string.Equals(a.Protocol, "ssh", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private ServerEditDialog CreateServerEditDialog(ServerAsset existing) =>
        new(existing, CollectGroups(), _rememberPassword || !string.IsNullOrEmpty(existing.EncryptedPassword), CollectAssetModels(), CollectTemplates()) { Owner = this };

    /// <summary>连接模板下拉数据。</summary>
    private List<ConnectionTemplate> CollectTemplates() =>
        _assetStore.Load().Templates.ToList();

    /// <summary>按对话框里的“记住密码”决定是否把明文密码加密落盘。</summary>
    private ServerAsset ApplyRememberPassword(ServerAsset model, bool remember)
    {
        if (!remember)
        {
            return model.EncryptedPassword is null ? model : model with { EncryptedPassword = null };
        }

        // 勾了记住密码但全局开关是关的，顺手把它打开，避免出现“存了却从不使用”的状态
        if (!_rememberPassword)
        {
            ChkRememberPassword.IsChecked = true;
        }

        return string.IsNullOrEmpty(model.Password)
            ? model
            : model with { EncryptedPassword = CredentialProtector.Protect(model.Password) };
    }

    private void BtnFavoriteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerAssetViewModel vm)
        {
            vm.Model = vm.Model with { IsFavorite = !vm.Model.IsFavorite };
            vm.NotifyFavoriteChanged();
            SaveAssetsToStore();
            if (_navMode == NavMode.Favorites) ApplyFilter();
            TxtStatusBarInfo.Text = vm.Model.IsFavorite
                ? L10n.Format("Status_Favorited", vm.Name)
                : L10n.Format("Status_Unfavorited", vm.Name);
        }
    }

    private void BtnEditRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerAssetViewModel vm)
        {
            var dlg = CreateServerEditDialog(vm.Model);
            if (dlg.ShowDialog() == true)
            {
                var updatedModel = ApplyRememberPassword(dlg.Asset, dlg.RememberPassword);

                vm.Model = updatedModel;
                vm.UpdateExpiryStatus();
                SaveAssetsToStore();
                ApplyFilter();
            }
        }
    }

    private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ServerAssetViewModel vm)
        {
            var result = MessageBox.Show(L10n.Format("Msg_ConfirmDelete", vm.Name, vm.IpPortDisplay), L10n["Msg_DeleteTitle"], MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _allAssets.Remove(vm);
                SaveAssetsToStore();
                ApplyFilter();
            }
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewAssets.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(L10n["Msg_SelectToDelete"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 附带待删清单摘要（名称 + IP，最多列出前 10 条，超出用省略号）
        const int maxList = 10;
        var lines = selected.Take(maxList)
            .Select(a => $"• {a.Name} ({a.IpPortDisplay})")
            .ToList();
        if (selected.Count > maxList)
        {
            lines.Add(L10n.Format("Msg_DeleteListMore", selected.Count - maxList));
        }
        string listText = string.Join("\n", lines);

        var result = MessageBox.Show(L10n.Format("Msg_ConfirmBatchDelete", selected.Count, listText), L10n["Msg_BatchDeleteTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            foreach (var item in selected)
            {
                _allAssets.Remove(item);
            }
            SaveAssetsToStore();
            ApplyFilter();
        }
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = L10n["Dlg_ImportTitle"],
            Filter = L10n["Dlg_ImportFilter"]
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            // 按扩展名区分来源：.xls/.xlsx 为 ISS7 导出，.csv 为标准格式
            CsvImportResult importResult;
            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            if (ext is ".xls" or ".xlsx")
            {
                using var stream = File.OpenRead(dlg.FileName);
                importResult = AssetExcelImportService.Import(stream);
            }
            else
            {
                importResult = AssetCsvService.ImportCsv(File.ReadAllText(dlg.FileName));
            }

            int added = 0;
            foreach (var asset in importResult.Assets)
            {
                var model = asset;
                // 开启“本机记住密码”时，把导入的密码加密落盘
                if (_rememberPassword && !string.IsNullOrEmpty(asset.Password))
                {
                    model = asset with { EncryptedPassword = CredentialProtector.Protect(asset.Password) };
                }
                RegisterAsset(new ServerAssetViewModel(model));
                added++;
            }

            SaveAssetsToStore();
            ApplyFilter();

            string msg = L10n.Format("Msg_ImportDone", added);
            if (importResult.Errors.Count > 0)
            {
                msg += "\n" + L10n.Format("Msg_ImportSkipped", importResult.Errors.Count);
            }
            if (added == 0)
            {
                msg += "\n\n" + L10n["Msg_ImportFormatHint"];
            }
            MessageBox.Show(msg, L10n["Msg_ImportDoneTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Format("Msg_ImportFailed", ex.Message), L10n["Msg_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewAssets.Where(a => a.IsSelected).Select(a => a.Model).ToList();
        if (selected.Count == 0)
        {
            selected = _viewAssets.Select(a => a.Model).ToList();
        }

        if (selected.Count == 0)
        {
            MessageBox.Show(L10n["Msg_ExportedEmpty"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 弹出字段选择对话框；用户取消则不导出
        var fieldsDlg = new ExportFieldsDialog { Owner = this };
        if (fieldsDlg.ShowDialog() != true)
        {
            return;
        }
        var fields = fieldsDlg.SelectedFields;

        // 勾选密码列时，先做明文泄露风险确认
        if (fields.Password)
        {
            var risk = MessageBox.Show(L10n["ExpFld_RiskBody"], L10n["ExpFld_RiskTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (risk != MessageBoxResult.Yes)
            {
                return;
            }
        }

        // 勾选密钥路径列时，同样做敏感信息确认
        if (fields.SshKeyPath)
        {
            var risk = MessageBox.Show(L10n["ExpFld_KeyPathRiskBody"], L10n["ExpFld_KeyPathRiskTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (risk != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var dlg = new SaveFileDialog
        {
            Title = L10n["Dlg_ExportTitle"],
            Filter = L10n["Dlg_ExportFilter"],
            FileName = $"Servers_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var csv = AssetCsvService.ExportCsv(selected, fields);
                File.WriteAllText(dlg.FileName, csv, System.Text.Encoding.UTF8);
                var msg = L10n.Format("Msg_ExportDone", selected.Count, dlg.FileName);
                if (!fields.Password)
                {
                    msg += "\n\n" + L10n["Msg_ExportDoneNoPwd"];
                }
                MessageBox.Show(msg, L10n["Msg_ExportDoneTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(L10n.Format("Msg_ExportFailed", ex.Message), L10n["Msg_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnResetResolution_Click(object sender, RoutedEventArgs e)
    {
        TxtWidth.Text = "1920";
        TxtHeight.Text = "1080";
    }

    private void BtnScreen14_Click(object sender, RoutedEventArgs e)
    {
        var primaryWidth = (int)SystemParameters.PrimaryScreenWidth;
        var primaryHeight = (int)SystemParameters.PrimaryScreenHeight;
        TxtWidth.Text = (primaryWidth / 2).ToString();
        TxtHeight.Text = (primaryHeight / 2).ToString();
    }

    private void BtnScreen16_Click(object sender, RoutedEventArgs e)
    {
        var primaryWidth = (int)SystemParameters.PrimaryScreenWidth;
        var primaryHeight = (int)SystemParameters.PrimaryScreenHeight;
        TxtWidth.Text = (primaryWidth / 3).ToString();
        TxtHeight.Text = (primaryHeight / 2).ToString();
    }

    private void BtnScreen19_Click(object sender, RoutedEventArgs e)
    {
        var primaryWidth = (int)SystemParameters.PrimaryScreenWidth;
        var primaryHeight = (int)SystemParameters.PrimaryScreenHeight;
        TxtWidth.Text = (primaryWidth / 3).ToString();
        TxtHeight.Text = (primaryHeight / 3).ToString();
    }

    private void BtnSplitH_Click(object sender, RoutedEventArgs e)
    {
        var primaryWidth = (int)SystemParameters.PrimaryScreenWidth;
        var primaryHeight = (int)SystemParameters.PrimaryScreenHeight;
        TxtWidth.Text = (primaryWidth / 2).ToString();
        TxtHeight.Text = primaryHeight.ToString();
    }

    private void BtnSplitV_Click(object sender, RoutedEventArgs e)
    {
        var primaryWidth = (int)SystemParameters.PrimaryScreenWidth;
        var primaryHeight = (int)SystemParameters.PrimaryScreenHeight;
        TxtWidth.Text = primaryWidth.ToString();
        TxtHeight.Text = (primaryHeight / 2).ToString();
    }

    private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        // 将详情面板中的分辨率快速配置写回当前选中的 RDP 资产
        if (GridAssets.SelectedItem is ServerAssetViewModel vm
            && !_inspectorProtocol.Equals("ssh", StringComparison.OrdinalIgnoreCase)
            && PanelRdpResolution.Visibility == Visibility.Visible)
        {
            int? rdpWidth = int.TryParse(TxtWidth.Text.Trim(), out var parsedW) && parsedW > 0 ? parsedW : null;
            int? rdpHeight = int.TryParse(TxtHeight.Text.Trim(), out var parsedH) && parsedH > 0 ? parsedH : null;
            vm.Model = vm.Model with { RdpWidth = rdpWidth, RdpHeight = rdpHeight };
        }

        SaveAssetsToStore();
        MessageBox.Show(L10n["Msg_SettingsSaved"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>留下宝贵意见：优先拉起本机邮件客户端；没有可用客户端时把邮箱直接展示出来。</summary>
    private void BtnFeedback_Click(object sender, RoutedEventArgs e)
    {
        const string email = "remotemate@protonmail.com";

        if (HasMailClient())
        {
            try
            {
                OpenBrowserUrl($"mailto:{email}?subject=RemotePilot%20Feedback");
                return;
            }
            catch
            {
                // 打不开就走下面的兜底提示
            }
        }

        MessageBox.Show(
            L10n["Msg_FeedbackFallback"],
            L10n["Msg_FeedbackTitle"],
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>本机是否配置了 mailto 默认程序，避免弹系统的"无法打开此 mailto 链接"。</summary>
    private static bool HasMailClient()
    {
        using var userChoice = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\mailto\UserChoice");
        var progId = userChoice?.GetValue("ProgId") as string;
        if (string.IsNullOrWhiteSpace(progId))
        {
            return false;
        }

        using var command = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
        return command?.GetValue(null) is string cmd && !string.IsNullOrWhiteSpace(cmd);
    }

    private void BtnSyncCloud_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new FastRdpSyncDialog(_assetApiUrl) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SyncedAssets.Count > 0)
        {
            int count = 0;
            foreach (var asset in dlg.SyncedAssets)
            {
                RegisterAsset(new ServerAssetViewModel(asset));
                count++;
            }
            // 记住接口地址，下次打开直接带出
            _assetApiUrl = dlg.ApiUrl;
            SaveAssetsToStore();
            ApplyFilter();
            MessageBox.Show(L10n.Format("Msg_SyncDone", count), L10n["Msg_SyncDoneTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void BtnProbe_Click(object sender, RoutedEventArgs e)
    {
        await RunBatchProbeAsync();
    }

    private async void BtnBatchProbe_Click(object sender, RoutedEventArgs e)
    {
        await RunBatchProbeAsync();
    }

    /// <summary>批量探测当前可见资产的网络可达性与延迟（顶栏「检测延迟」与分组行「批量检测」共用）。</summary>
    private async Task RunBatchProbeAsync()
    {
        BtnProbe.IsEnabled = false;
        BtnBatchProbe.IsEnabled = false;
        TxtStatusBarInfo.Text = L10n["Status_ProbingAll"];

        foreach (var vm in _viewAssets)
        {
            vm.SetStatus(AssetStatusKind.Probing);
        }

        var assets = _viewAssets.Select(v => v.Model).ToList();
        var latencies = await _probe.ProbeBatchAsync(assets, 3000);

        foreach (var vm in _viewAssets)
        {
            if (latencies.TryGetValue(vm.Id, out var latency))
            {
                if (latency.HasValue)
                {
                    vm.SetStatus(AssetStatusKind.Latency, latency.Value);
                    vm.Model = vm.Model with { LastProbeReachable = true, LastProbeAt = DateTimeOffset.UtcNow };
                }
                else
                {
                    vm.SetStatus(AssetStatusKind.Offline);
                    vm.Model = vm.Model with { LastProbeReachable = false, LastProbeAt = DateTimeOffset.UtcNow };
                }
            }
        }

        SaveAssetsToStore();
        BtnProbe.IsEnabled = true;
        BtnBatchProbe.IsEnabled = true;
        TxtStatusBarInfo.Text = L10n["Status_ProbeDone"];
    }

    private void TxtProbeInterval_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_probe == null) return;

        if (int.TryParse(TxtProbeInterval.Text.Trim(), out var minutes) && minutes >= 1)
        {
            _probe.StartPeriodicProbe(minutes, _allAssets.Select(a => a.Model), _assetStore);
        }
        else
        {
            _probe.StopPeriodicProbe();
        }
    }

    private void ChkRememberPassword_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressRememberToggle) return;

        _rememberPassword = ChkRememberPassword.IsChecked == true;

        // 关闭“本机记住密码”时，立即清除已保存的密码密文
        if (!_rememberPassword)
        {
            foreach (var vm in _allAssets)
            {
                if (!string.IsNullOrEmpty(vm.Model.EncryptedPassword))
                {
                    vm.Model = vm.Model with { EncryptedPassword = null };
                }
            }
        }

        _assetStore.Save(_assetStore.Load() with
        {
            Assets = _allAssets.Select(vm => vm.Model).ToList(),
            RememberPassword = _rememberPassword
        });

        TxtStatusBarInfo.Text = _rememberPassword
            ? L10n["Status_RememberPasswordOn"]
            : L10n["Status_RememberPasswordOff"];
    }

    private void OnProbeCompleted(object? sender, List<ServerAsset> updatedList)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var dict = updatedList.ToDictionary(a => a.Id);
            foreach (var vm in _allAssets)
            {
                if (dict.TryGetValue(vm.Id, out var updated))
                {
                    vm.Model = updated;
                    vm.SetStatus(updated.LastProbeReachable == true ? AssetStatusKind.Online : AssetStatusKind.Offline);
                }
            }
            TxtStatusBarInfo.Text = L10n.Format("Status_ProbeScheduled", DateTime.Now.ToString("HH:mm:ss"));
        });
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var target = _viewAssets.FirstOrDefault(a => a.IsSelected) ?? (GridAssets.SelectedItem as ServerAssetViewModel);
        if (target == null)
        {
            MessageBox.Show(L10n["Msg_SelectToConnect"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ConnectServer(target);
    }

    private void ConnectServer(ServerAssetViewModel vm, bool switchToWorkspace = true)
    {
        int.TryParse(TxtWidth.Text.Trim(), out var w);
        int.TryParse(TxtHeight.Text.Trim(), out var h);

        // 颜色深度用 Tag 取值，避免界面语言切换后解析文案失败
        int colorDepth = 32;
        if (CmbColorDepth.SelectedItem is ComboBoxItem cbi && int.TryParse(cbi.Tag?.ToString(), out var cd))
        {
            colorDepth = cd;
        }

        // 只记录最近使用时间：分辨率/重定向不再把顶栏值写回资产，避免覆盖“连接方案/模板”里配置的参数
        var assetToLaunch = vm.Model with { LastAccessedAt = DateTimeOffset.UtcNow };

        // 记录最近使用时间，供“最近使用”视图排序
        vm.Model = assetToLaunch;
        SaveAssetsToStore();
        if (_navMode == NavMode.Recent) ApplyFilter();

        if (string.Equals(assetToLaunch.Protocol, "ssh", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var password = assetToLaunch.Password;
                var keyPath = assetToLaunch.SshKeyPath;
                var username = assetToLaunch.Username;

                // 只要存在已保存密文就尝试 DPAPI 解密免输入；解密失败（null/空）则走下方补录流程
                if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(assetToLaunch.EncryptedPassword))
                {
                    password = CredentialProtector.Unprotect(assetToLaunch.EncryptedPassword);
                }

                // 缺少可用凭据时当场补录
                if (string.IsNullOrEmpty(password) && string.IsNullOrWhiteSpace(keyPath))
                {
                    var credentialDialog = new SshCredentialDialog(username, _rememberPassword) { Owner = this };
                    if (credentialDialog.ShowDialog() != true) return;
                    username = credentialDialog.Username;
                    password = credentialDialog.Password;
                    keyPath = credentialDialog.PrivateKeyPath ?? "";

                    assetToLaunch = credentialDialog.RememberPassword
                        ? assetToLaunch with { EncryptedPassword = CredentialProtector.Protect(password) }
                        : assetToLaunch with { EncryptedPassword = null };
                }

                assetToLaunch = assetToLaunch with { Username = username, SshKeyPath = keyPath ?? "" };
                vm.Model = assetToLaunch;
                SaveAssetsToStore();

                // 跳板机：解析被引用的资产及其凭据（密码同样走 DPAPI 解密）
                ServerAsset? jumpAsset = null;
                string? jumpPassword = null;
                if (assetToLaunch.JumpAssetId is Guid jumpId)
                {
                    jumpAsset = _allAssets.FirstOrDefault(a => a.Model.Id == jumpId)?.Model;
                    if (jumpAsset != null && string.IsNullOrEmpty(jumpAsset.Password)
                        && !string.IsNullOrEmpty(jumpAsset.EncryptedPassword))
                    {
                        jumpPassword = CredentialProtector.Unprotect(jumpAsset.EncryptedPassword);
                    }
                }

                // 加载到会话工作台；switchToWorkspace=false（资产列表双击连接）时停留当前视图，由用户自行切换
                SessionWorkspace.AddSession(assetToLaunch, password, keyPath, null, jumpAsset, jumpPassword);
                if (switchToWorkspace)
                {
                    GridAssets.Visibility = Visibility.Collapsed;
                    SessionWorkspace.Visibility = Visibility.Visible;
                    _navMode = NavMode.Sessions;
                    UpdateNavSelection();
                }
                TxtStatusBarInfo.Text = switchToWorkspace
                    ? L10n.Format("Status_SshOpened", assetToLaunch.Name)
                    : L10n.Format("Status_ConnectBackground", assetToLaunch.Name);
                BorderConnectionErrorHint.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                TxtErrorReason.Text = L10n.Format("Msg_SshConnectFailed", ex.Message);
                BorderConnectionErrorHint.Visibility = Visibility.Visible;
                TxtStatusBarInfo.Text = L10n.Format("Status_ConnectFailed", ex.Message);
            }
        }
        else
        {
            try
            {
                // 只要存在已保存密文就尝试 DPAPI 解密并写入 .rdp 免输入；解密失败则由 .rdp 的凭据提示兜底
                var rdpPassword = assetToLaunch.Password;
                if (string.IsNullOrEmpty(rdpPassword) && !string.IsNullOrEmpty(assetToLaunch.EncryptedPassword))
                {
                    rdpPassword = CredentialProtector.Unprotect(assetToLaunch.EncryptedPassword);
                }

                // 按资产绑定的模板取连接参数（网关、分辨率等），找不到则按资产自身配置
                ConnectionTemplate? rdpTemplate = null;
                if (assetToLaunch.TemplateId is Guid templateId)
                {
                    rdpTemplate = _assetStore.Load().Templates.FirstOrDefault(t => t.Id == templateId);
                }

                // 参数优先级：资产（“连接方案”里配置的）→ 绑定模板 → 顶栏兜底
                // 顶栏「设备重定向」被用户改动后作为本次连接的显式覆盖；未改动时按 资产 → 模板 → 顶栏默认 取值
                bool? drivesOverride = _redirectToolbarTouched ? (ChkDriveMap.IsChecked ?? false) : null;
                bool? audioOverride = _redirectToolbarTouched ? (ChkAudio.IsChecked ?? true) : null;
                bool? clipboardOverride = _redirectToolbarTouched ? (ChkClipboard.IsChecked ?? false) : null;

                var rdpAsset = assetToLaunch with
                {
                    RdpWidth = assetToLaunch.RdpWidth ?? rdpTemplate?.RdpWidth ?? (w > 0 ? w : null),
                    RdpHeight = assetToLaunch.RdpHeight ?? rdpTemplate?.RdpHeight ?? (h > 0 ? h : null),
                    RdpColorDepth = assetToLaunch.RdpColorDepth ?? rdpTemplate?.RdpColorDepth ?? colorDepth,
                    RedirectDrives = drivesOverride ?? assetToLaunch.RedirectDrives ?? rdpTemplate?.RedirectDrives ?? false,
                    RedirectAudio = audioOverride ?? assetToLaunch.RedirectAudio ?? rdpTemplate?.RedirectAudio ?? true,
                    RedirectClipboard = clipboardOverride ?? assetToLaunch.RedirectClipboard ?? rdpTemplate?.RedirectClipboard ?? false
                };

                // 加载到会话工作台；switchToWorkspace=false（资产列表双击连接）时停留当前视图，由用户自行切换
                SessionWorkspace.AddSession(rdpAsset, rdpPassword, template: rdpTemplate);
                if (switchToWorkspace)
                {
                    GridAssets.Visibility = Visibility.Collapsed;
                    SessionWorkspace.Visibility = Visibility.Visible;
                    _navMode = NavMode.Sessions;
                    UpdateNavSelection();
                }
                TxtStatusBarInfo.Text = switchToWorkspace
                    ? L10n.Format("Status_RdpOpened", assetToLaunch.Name)
                    : L10n.Format("Status_ConnectBackground", assetToLaunch.Name);
                BorderConnectionErrorHint.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                TxtErrorReason.Text = L10n.Format("Msg_RdpConnectFailed", ex.Message);
                BorderConnectionErrorHint.Visibility = Visibility.Visible;
                TxtStatusBarInfo.Text = L10n.Format("Status_ConnectFailed", ex.Message);
            }
        }
    }

    /// <summary>
    /// SSH 连接失败回调：当资产已保存凭据（DPAPI 密文）且失败疑似认证失败时，
    /// 弹窗让用户重新输入凭据并重连；同一资产仅提示一次，避免凭据仍错误时无限弹窗。
    /// </summary>
    private void OnSessionConnectFailed(SessionItemViewModel sessionVm, string? error)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnSessionConnectFailed(sessionVm, error));
            return;
        }

        // 仅处理 SSH 协议（RDP 由外部 mstsc.exe 启动，应用内无法探测认证失败）
        var asset = sessionVm.Asset;
        if (!string.Equals(asset.Protocol, "ssh", StringComparison.OrdinalIgnoreCase)) return;

        // 仅当失败疑似认证失败才回退；网络不可达等错误不弹窗
        if (string.IsNullOrEmpty(error)) return;
        bool looksAuthFailure = error.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || error.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || error.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || error.Contains("credential", StringComparison.OrdinalIgnoreCase);
        if (!looksAuthFailure) return;

        var assetVm = _allAssets.FirstOrDefault(a => a.Id == asset.Id);
        if (assetVm == null) return;

        // 仅当资产已保存凭据（DPAPI 密文）时才需要回退补录；当场输入的口令无需回退
        if (string.IsNullOrEmpty(assetVm.Model.EncryptedPassword)) return;

        // 防循环：同一资产只提示一次
        if (!_authRetryPrompted.Add(assetVm.Id)) return;

        var dlg = new SshCredentialDialog(assetVm.Model.Username, _rememberPassword)
        {
            Owner = this,
            Title = L10n["Cred_ReenterTitle"]
        };
        if (dlg.ShowDialog() != true) return;

        assetVm.Model = assetVm.Model with
        {
            Username = dlg.Username,
            SshKeyPath = dlg.PrivateKeyPath ?? assetVm.Model.SshKeyPath,
            Password = dlg.Password,
            EncryptedPassword = dlg.RememberPassword ? CredentialProtector.Protect(dlg.Password) : null
        };
        SaveAssetsToStore();

        SessionWorkspace.CloseSession(sessionVm);
        TxtStatusBarInfo.Text = L10n["Status_CredReenter"];
        ConnectServer(assetVm);
    }

    private static void OpenBrowserUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Format("Msg_BrowserFailed", ex.Message));
        }
    }

    private void RedirectToolbar_Changed(object sender, RoutedEventArgs e) => _redirectToolbarTouched = true;

    private void GridAssets_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 双击行内交互控件（收藏/模板名/编辑/删除等）时不发起连接
        if (e.OriginalSource is DependencyObject src && IsInteractiveElement(src)) return;

        // 双击直接连接，不展开右侧配置面板
        if (GridAssets.SelectedItem is ServerAssetViewModel vm)
        {
            ConnectServer(vm, switchToWorkspace: false);
        }
    }

    private void GridAssets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 行选中变化后刷新「已选 N 台」与表头三态
        UpdateSelectionSummary();

        if (GridAssets.SelectedItem is ServerAssetViewModel vm)
        {
            EnsureDetailsPanelExpanded();
            PanelInspectorForm.Visibility = Visibility.Visible;
            InspectorPlaceholder.Visibility = Visibility.Collapsed;

            InspectorTxtName.Text = vm.Model.Name;
            _inspectorProtocol = string.IsNullOrWhiteSpace(vm.Model.Protocol) ? "rdp" : vm.Model.Protocol;
            InspectorTxtTarget.Text = $"{_inspectorProtocol} · {vm.Model.Host}:{vm.Model.Port}";

            // 按协议仅显示对应参数（SSH 资产隐藏 RDP 专属的分辨率设置）
            ApplyInspectorProtocolVisibility();
            TxtWidth.Text = (vm.Model.RdpWidth ?? 1920).ToString();
            TxtHeight.Text = (vm.Model.RdpHeight ?? 1080).ToString();

            // 到期预警状态
            if (vm.Model.ExpiresOn.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var daysRemaining = vm.Model.ExpiresOn.Value.DayNumber - today.DayNumber;
                if (daysRemaining < 0)
                {
                    InspectorTxtExpiryWarning.Text = L10n.Format("Insp_Expired", -daysRemaining);
                    InspectorTxtExpiryWarning.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CF222E"));
                }
                else if (daysRemaining <= 7)
                {
                    InspectorTxtExpiryWarning.Text = L10n.Format("Insp_DaysLeft", daysRemaining);
                    InspectorTxtExpiryWarning.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A6700"));
                }
                else
                {
                    InspectorTxtExpiryWarning.Text = L10n.Format("Insp_Normal", daysRemaining);
                    InspectorTxtExpiryWarning.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A7F37"));
                }
            }
            else
            {
                InspectorTxtExpiryWarning.Text = L10n["Insp_NotSet"];
                InspectorTxtExpiryWarning.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A7F37"));
            }

            // 最后检测耗时
            if (vm.Model.LastProbeAt.HasValue)
            {
                var durationText = vm.StatusKind == AssetStatusKind.Latency ? vm.StatusDisplay : (vm.Model.LastProbeReachable == true ? L10n["Insp_Online"] : L10n["Insp_Offline"]);
                InspectorTxtProbeDuration.Text = L10n.Format("Insp_ProbeAt", durationText, vm.Model.LastProbeAt.Value.ToLocalTime().ToString("HH:mm:ss"));
            }
            else
            {
                InspectorTxtProbeDuration.Text = L10n["Insp_NotProbed"];
            }
        }
        else
        {
            PanelInspectorForm.Visibility = Visibility.Collapsed;
            InspectorPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void ApplyInspectorProtocolVisibility()
    {
        PanelRdpResolution.Visibility = _inspectorProtocol.Equals("ssh", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
