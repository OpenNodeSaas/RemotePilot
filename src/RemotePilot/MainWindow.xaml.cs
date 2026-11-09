using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RemotePilot.Models;
using RemotePilot.Services;
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
    private NavMode _navMode = NavMode.Assets;
    private bool _isGridView;
    private bool _rememberPassword;
    private bool _suppressRememberToggle;
    private string _assetApiUrl = "";
    private bool _isWorkspaceFullscreen;
    private bool _detailsWasVisibleBeforeFullscreen;
    private WindowState _windowStateBeforeFullscreen = WindowState.Normal;

    private enum NavMode { Assets, Sessions, Favorites, Recent, Groups }

    // Localization Keys
    public const string ResNavAssets = "Nav_Assets";
    public const string ResNavSessions = "Nav_Sessions";
    public const string ResNavFavorites = "Nav_Favorites";
    public const string ResNavRecent = "Nav_Recent";
    public const string ResNavGroups = "Nav_Groups";
    public const string ResNavPortal = "Nav_FastRdpPortal";
    public const string ResNavFeedback = "Nav_Feedback";
    public const string ResBtnSearch = "Btn_Search";
    public const string ResBtnAddServer = "Btn_AddServer";
    public const string ResBtnImport = "Btn_Import";
    public const string ResBtnImportTemplate = "Btn_ImportTemplate";
    public const string ResBtnExportSelected = "Btn_ExportSelected";
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
    public const string ResInspName = "Insp_Name";
    public const string ResInspHost = "Insp_Host";
    public const string ResInspPort = "Insp_Port";
    public const string ResInspGroup = "Insp_Group";
    public const string ResInspOwner = "Insp_Owner";
    public const string ResInspProtocol = "Insp_Protocol";
    public const string ResInspUsername = "Insp_Username";
    public const string ResInspExpiresOn = "Insp_ExpiresOn";
    public const string ResInspNote1 = "Insp_Note1";
    public const string ResInspNote2 = "Insp_Note2";
    public const string ResInspNote3 = "Insp_Note3";
    public const string ResInspExpiryWarn = "Insp_ExpiryWarn";
    public const string ResInspLastProbe = "Insp_LastProbe";
    public const string ResInspSave = "Insp_Save";
    public const string ResInspPlaceholder = "Insp_Placeholder";
    public const string ResDiagTitle = "Diag_Title";
    public const string ResDiagReason = "Diag_Reason";
    public const string ResDiagAdvice = "Diag_Advice";
    public const string ResDiagAdviceDetail = "Diag_AdviceDetail";
    public const string ResDiagRetry = "Diag_Retry";
    public const string ResDiagRelay = "Diag_Relay";
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
    public const string ResInspFavorite = "Insp_Favorite";
    public const string ResStatusReady = "Status_Ready";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    public MainWindow()
    {
        InitializeComponent();

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
            ColLeftNav.Width = new GridLength(220);
            BorderLeftNav.Visibility = Visibility.Visible;
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
            
            _allAssets.Add(vm);
        }

        ApplyFilter();
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

        RefreshGroupBadges();

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
        int expiryCount = _allAssets.Count(a => a.IsExpiredOrNear);
        if (expiryCount > 0)
        {
            BtnExpirySummaryBanner.Visibility = Visibility.Visible;
            BtnExpirySummaryBanner.Content = L10n.Format("Status_ExpiryBanner", expiryCount);
        }
        else
        {
            BtnExpirySummaryBanner.Visibility = Visibility.Collapsed;
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

    private void BtnShowRelayPrompt_Click(object sender, RoutedEventArgs e)
    {
        var target = _viewAssets.FirstOrDefault(a => a.IsSelected) ?? (GridAssets.SelectedItem as ServerAssetViewModel);
        if (target != null)
        {
            ShowRelayRecommendationPrompt(target.Host, target.Port);
        }
        else
        {
            MessageBox.Show(L10n["Msg_SelectAsset"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void BtnSelectAllToggle_Click(object sender, RoutedEventArgs e)
    {
        bool targetState = !_viewAssets.All(a => a.IsSelected);
        foreach (var vm in _viewAssets)
        {
            vm.IsSelected = targetState;
        }
    }

    private void BtnAddServer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ServerEditDialog(null, CollectGroups(), _rememberPassword, CollectAssetModels(), CollectTemplates()) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var newModel = ApplyRememberPassword(dlg.Asset, dlg.RememberPassword);

            var vm = new ServerAssetViewModel(newModel);
            _allAssets.Add(vm);
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
            var result = MessageBox.Show(L10n.Format("Msg_ConfirmDelete", vm.Name), L10n["Msg_DeleteTitle"], MessageBoxButton.YesNo, MessageBoxImage.Question);
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

        var result = MessageBox.Show(L10n.Format("Msg_ConfirmBatchDelete", selected.Count), L10n["Msg_BatchDeleteTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
                _allAssets.Add(new ServerAssetViewModel(model));
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

    private void BtnImportTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = L10n["Dlg_TemplateTitle"],
            Filter = L10n["Dlg_ExportFilter"],
            FileName = "服务器导入模板.csv"
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dlg.FileName, AssetCsvService.BuildStandardTemplateCsv(), System.Text.Encoding.UTF8);
            MessageBox.Show(L10n.Format("Msg_TemplateSaved", dlg.FileName), L10n["Msg_ImportDoneTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Format("Msg_ExportFailed", ex.Message), L10n["Msg_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
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
                var csv = AssetCsvService.ExportCsv(selected);
                File.WriteAllText(dlg.FileName, csv, System.Text.Encoding.UTF8);
                MessageBox.Show(L10n.Format("Msg_ExportDone", selected.Count, dlg.FileName), L10n["Msg_ExportDoneTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
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
        SaveAssetsToStore();
        MessageBox.Show(L10n["Msg_SettingsSaved"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnFastRdpPortal_Click(object sender, RoutedEventArgs e)
    {
        OpenBrowserUrl("https://www.idazi.xyz");
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
                _allAssets.Add(new ServerAssetViewModel(asset));
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
        BtnProbe.IsEnabled = false;
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
        TxtStatusBarInfo.Text = L10n["Status_ProbeDone"];

        var offlineAsset = _viewAssets.FirstOrDefault(a => a.StatusKind == AssetStatusKind.Offline);
        if (offlineAsset != null)
        {
            ShowRelayRecommendationPrompt(offlineAsset.Model.Host, offlineAsset.Model.Port);
        }
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

    private void ConnectServer(ServerAssetViewModel vm)
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

                // 开启“本机记住密码”时，用 DPAPI 密文免输入直接登录
                if (string.IsNullOrEmpty(password) && _rememberPassword && !string.IsNullOrEmpty(assetToLaunch.EncryptedPassword))
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
                    if (jumpAsset != null && string.IsNullOrEmpty(jumpAsset.Password) && _rememberPassword
                        && !string.IsNullOrEmpty(jumpAsset.EncryptedPassword))
                    {
                        jumpPassword = CredentialProtector.Unprotect(jumpAsset.EncryptedPassword);
                    }
                }

                // 无缝切换并加载到会话工作台
                GridAssets.Visibility = Visibility.Collapsed;
                SessionWorkspace.Visibility = Visibility.Visible;
                SessionWorkspace.AddSession(assetToLaunch, password, keyPath, null, jumpAsset, jumpPassword);
                _navMode = NavMode.Sessions;
                UpdateNavSelection();
                TxtStatusBarInfo.Text = L10n.Format("Status_SshOpened", assetToLaunch.Name);
                BorderConnectionErrorHint.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                TxtErrorReason.Text = L10n.Format("Msg_SshConnectFailed", ex.Message);
                BorderConnectionErrorHint.Visibility = Visibility.Visible;
                TxtStatusBarInfo.Text = L10n.Format("Status_ConnectFailed", ex.Message);
                bool isAuthError = ex.Message.Contains("auth", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("credential", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase);
                if (!isAuthError)
                {
                    ShowRelayRecommendationPrompt(assetToLaunch.Host, assetToLaunch.Port);
                }
            }
        }
        else
        {
            try
            {
                // 开启“本机记住密码”时，用 DPAPI 密文写入 .rdp，实现免输入登录
                var rdpPassword = assetToLaunch.Password;
                if (string.IsNullOrEmpty(rdpPassword) && _rememberPassword && !string.IsNullOrEmpty(assetToLaunch.EncryptedPassword))
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
                var rdpAsset = assetToLaunch with
                {
                    RdpWidth = assetToLaunch.RdpWidth ?? rdpTemplate?.RdpWidth ?? (w > 0 ? w : null),
                    RdpHeight = assetToLaunch.RdpHeight ?? rdpTemplate?.RdpHeight ?? (h > 0 ? h : null),
                    RdpColorDepth = assetToLaunch.RdpColorDepth ?? rdpTemplate?.RdpColorDepth ?? colorDepth,
                    RedirectDrives = assetToLaunch.RedirectDrives ?? rdpTemplate?.RedirectDrives ?? (ChkDriveMap.IsChecked ?? false),
                    RedirectAudio = assetToLaunch.RedirectAudio ?? rdpTemplate?.RedirectAudio ?? (ChkAudio.IsChecked ?? true),
                    RedirectClipboard = assetToLaunch.RedirectClipboard ?? rdpTemplate?.RedirectClipboard ?? (ChkClipboard.IsChecked ?? false)
                };

                // RDP 同样支持在会话工作台的嵌入/占位容器中打开
                GridAssets.Visibility = Visibility.Collapsed;
                SessionWorkspace.Visibility = Visibility.Visible;
                SessionWorkspace.AddSession(rdpAsset, rdpPassword, template: rdpTemplate);
                _navMode = NavMode.Sessions;
                UpdateNavSelection();
                TxtStatusBarInfo.Text = L10n.Format("Status_RdpOpened", assetToLaunch.Name);
                BorderConnectionErrorHint.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                TxtErrorReason.Text = L10n.Format("Msg_RdpConnectFailed", ex.Message);
                BorderConnectionErrorHint.Visibility = Visibility.Visible;
                TxtStatusBarInfo.Text = L10n.Format("Status_ConnectFailed", ex.Message);
                bool isAuthError = ex.Message.Contains("auth", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("credential", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase);
                if (!isAuthError)
                {
                    ShowRelayRecommendationPrompt(assetToLaunch.Host, assetToLaunch.Port);
                }
            }
        }
    }

    private void ShowRelayRecommendationPrompt(string host, int port)
    {
        var result = MessageBox.Show(
            L10n.Format("Dlg_RelayPrompt", host, port),
            L10n["Dlg_RelayTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            using var client = new FastRdpApiClient();
            var url = client.GetRelayRecommendationUrl(host, port);
            OpenBrowserUrl(url);
        }
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

    private void GridAssets_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 双击直接连接，不展开右侧配置面板
        if (GridAssets.SelectedItem is ServerAssetViewModel vm)
        {
            ConnectServer(vm);
        }
    }

    private void GridAssets_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridAssets.SelectedItem is ServerAssetViewModel vm)
        {
            EnsureDetailsPanelExpanded();
            PanelInspectorForm.Visibility = Visibility.Visible;
            InspectorPlaceholder.Visibility = Visibility.Collapsed;

            ChkInspectorFavorite.IsChecked = vm.Model.IsFavorite;
            InspectorTxtName.Text = vm.Model.Name;
            InspectorTxtHost.Text = vm.Model.Host;
            InspectorTxtPort.Text = vm.Model.Port.ToString();
            InspectorTxtGroup.Text = vm.Model.Group;
            InspectorTxtOwner.Text = vm.Model.Owner;
            InspectorTxtUsername.Text = vm.Model.Username;

            // 按协议仅显示对应参数（SSH 资产隐藏 RDP 专属的分辨率设置）
            SetInspectorProtocol(vm.Model.Protocol);
            InspectorTxtExpiresOn.Text = vm.Model.ExpiresOn.HasValue ? vm.Model.ExpiresOn.Value.ToString("yyyy-MM-dd") : "";
            InspectorTxtNote1.Text = vm.CustomNote1;
            InspectorTxtNote2.Text = vm.CustomNote2;
            InspectorTxtNote3.Text = vm.CustomNote3;

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

    private void SetInspectorProtocol(string protocol)
    {
        InspectorCmbProtocol.SelectedIndex = protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ApplyInspectorProtocolVisibility();
    }

    private void ApplyInspectorProtocolVisibility()
    {
        var proto = (InspectorCmbProtocol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "rdp";
        PanelRdpResolution.Visibility = proto.Equals("ssh", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void InspectorCmbProtocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelRdpResolution == null) return; // 初始化期间控件尚未创建
        ApplyInspectorProtocolVisibility();

        // 端口仍为默认值时跟随协议切换，避免覆盖用户自定义端口
        var port = InspectorTxtPort.Text.Trim();
        if (port.Length == 0 || port == "3389" || port == "22")
        {
            var proto = (InspectorCmbProtocol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "rdp";
            InspectorTxtPort.Text = proto.Equals("ssh", StringComparison.OrdinalIgnoreCase) ? "22" : "3389";
        }
    }

    private void InspectorBtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (GridAssets.SelectedItem is ServerAssetViewModel vm)
        {
            int.TryParse(InspectorTxtPort.Text.Trim(), out var port);
            var protocol = (InspectorCmbProtocol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "rdp";
            bool isSshProtocol = protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase);
            DateOnly? expiresOn = null;
            if (DateOnly.TryParse(InspectorTxtExpiresOn.Text.Trim(), out var parsedDate))
            {
                expiresOn = parsedDate;
            }

            var updatedModel = vm.Model with
            {
                Name = InspectorTxtName.Text.Trim(),
                Host = InspectorTxtHost.Text.Trim(),
                Port = port > 0 ? port : (isSshProtocol ? 22 : 3389),
                Group = InspectorTxtGroup.Text.Trim(),
                Owner = InspectorTxtOwner.Text.Trim(),
                Protocol = protocol,
                Username = InspectorTxtUsername.Text.Trim(),
                IsFavorite = ChkInspectorFavorite.IsChecked == true,
                ExpiresOn = expiresOn,
                CustomNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CustomNote1"] = InspectorTxtNote1.Text,
                    ["CustomNote2"] = InspectorTxtNote2.Text,
                    ["CustomNote3"] = InspectorTxtNote3.Text
                }
            };

            vm.Model = updatedModel;
            vm.UpdateExpiryStatus();
            vm.NotifyFavoriteChanged();
            SaveAssetsToStore();
            ApplyFilter();

            // Re-select in grid view
            GridAssets.SelectedItem = vm;
            MessageBox.Show(L10n["Msg_Saved"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
