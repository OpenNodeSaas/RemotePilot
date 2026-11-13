using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RemotePilot.Models;
using RemotePilot.Services;
using Microsoft.Win32;

namespace RemotePilot;

public partial class ConnectionSettingsDrawer : UserControl
{
    public const string ResDrawerTitle = "Drawer_Title";
    public const string ResSec1 = "Drawer_Sec1";
    public const string ResAssetName = "Drawer_AssetName";
    public const string ResTarget = "Drawer_Target";
    public const string ResProtocolType = "Drawer_ProtocolType";
    public const string ResApplyTemplate = "Drawer_ApplyTemplate";
    public const string ResParamSource = "Drawer_ParamSource";
    public const string ResSourceCustom = "Drawer_SourceCustom";
    public const string ResSec2 = "Drawer_Sec2";
    public const string ResSshWindowSize = "Setting_SshWindowSize";
    public const string ResScreen14 = "Btn_Screen14";
    public const string ResScreen16 = "Btn_Screen16";
    public const string ResScreen19 = "Btn_Screen19";
    public const string ResSplitH = "Btn_SplitH";
    public const string ResSplitV = "Btn_SplitV";
    public const string ResResolutionMode = "Drawer_ResolutionMode";
    public const string ResResFit = "Drawer_ResFit";
    public const string ResRes1080p = "Drawer_Res1080p";
    public const string ResRes2k = "Drawer_Res2k";
    public const string ResRes4k = "Drawer_Res4k";
    public const string ResResCustom = "Drawer_ResCustom";
    public const string ResWidth = "Drawer_Width";
    public const string ResHeight = "Drawer_Height";
    public const string ResColorDepth = "Drawer_ColorDepth";
    public const string ResDepth8 = "Drawer_Depth8";
    public const string ResDepth15 = "Drawer_Depth15";
    public const string ResDepth16 = "Drawer_Depth16";
    public const string ResDepth24 = "Drawer_Depth24";
    public const string ResDepth32 = "Drawer_Depth32";
    public const string ResNegotiationWarn = "Drawer_NegotiationWarn";
    public const string ResSec3 = "Drawer_Sec3";
    public const string ResChkDrive = "Drawer_ChkDrive";
    public const string ResChkAudio = "Drawer_ChkAudio";
    public const string ResChkClipboard = "Drawer_ChkClipboard";
    public const string ResComplianceHint = "Drawer_ComplianceHint";
    public const string ResSec4 = "Drawer_Sec4";
    public const string ResGateway = "Drawer_Gateway";
    public const string ResGatewayHint = "Drawer_GatewayHint";
    public const string ResSshPort = "Drawer_SshPort";
    public const string ResSshUser = "Drawer_SshUser";
    public const string ResKeyPath = "Drawer_KeyPath";
    public const string ResBtnBrowse = "Btn_Browse";
    public const string ResSaveAsTemplate = "Drawer_SaveAsTemplate";
    public const string ResSaveApply = "Drawer_SaveApply";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    private enum ParamSource { Asset, Template, Fallback }

    private enum ParamSourceKind { Custom, FullCustom, TemplateOnly, Inherited }

    private ServerAsset? _currentAsset;
    private ConnectionTemplate? _currentTemplate;
    private ConnectionTemplate? _boundTemplate;
    private ParamSourceKind _sourceKind = ParamSourceKind.Custom;
    private string _sourceTemplateName = string.Empty;
    private ObservableCollection<ConnectionTemplate> _templates = [];

    public event Action<ServerAsset>? Applied;
    public event Action<ConnectionTemplate>? TemplateSaved;
    public event Action? Closed;

    public ConnectionSettingsDrawer()
    {
        InitializeComponent();
        L10n.LanguageChanged += (_, __) => RefreshLocalizedText();
    }

    public void LoadTemplates(IEnumerable<ConnectionTemplate> templates)
    {
        _templates = new ObservableCollection<ConnectionTemplate>(templates);
        
        // 构建显示列表：首项为"（无模板）"，后续为模板摘要
        var displayItems = new List<string> { "（无模板）" };
        displayItems.AddRange(_templates.Select(t => t.GetDisplaySummary()));
        
        CmbTemplates.ItemsSource = displayItems;
    }

    private void SetSource(ParamSourceKind kind, string templateName = "")
    {
        _sourceKind = kind;
        _sourceTemplateName = templateName;
        UpdateParamSourceText();
    }

    private void UpdateParamSourceText()
    {
        TxtParamSource.Text = _sourceKind switch
        {
            ParamSourceKind.Inherited => L10n.Format("Drawer_SourceInherit", _sourceTemplateName),
            ParamSourceKind.FullCustom => L10n["Drawer_SourceFullCustom"],
            ParamSourceKind.TemplateOnly => L10n["Drawer_SourceTemplateOnly"],
            _ => L10n["Drawer_SourceCustom"]
        };
    }

    private void RefreshLocalizedText()
    {
        if (_boundTemplate != null)
        {
            TxtAssetName.Text = L10n.Format("Drawer_TemplatePrefix", _boundTemplate.Name);
            TxtTargetAddress.Text = L10n.Format("Drawer_PortLabel", _boundTemplate.Port);
        }
        else if (_currentAsset != null)
        {
            TxtAssetName.Text = _currentAsset.Name;
            TxtTargetAddress.Text = $"{_currentAsset.Host}:{_currentAsset.Port}";
        }
        UpdateParamSourceText();
    }

    public void BindAsset(ServerAsset asset, IEnumerable<ConnectionTemplate> templates)
    {
        _currentAsset = asset;
        _currentTemplate = null;
        _boundTemplate = null;
        LoadTemplates(templates);

        // Populate UI from Asset
        TxtAssetName.Text = asset.Name;
        TxtTargetAddress.Text = $"{asset.Host}:{asset.Port}";
        TxtProtocolTag.Text = asset.Protocol.ToUpperInvariant();

        // Template selection
        if (asset.TemplateId.HasValue)
        {
            var matchIndex = _templates.ToList().FindIndex(t => t.Id == asset.TemplateId.Value);
            if (matchIndex >= 0)
            {
                CmbTemplates.SelectedIndex = matchIndex + 1; // +1 因为首项是"（无模板）"
                _currentTemplate = _templates[matchIndex];
                SetSource(ParamSourceKind.Inherited, _currentTemplate.Name);
            }
            else
            {
                CmbTemplates.SelectedIndex = 0;
                SetSource(ParamSourceKind.Custom);
            }
        }
        else
        {
            CmbTemplates.SelectedIndex = 0;
            SetSource(ParamSourceKind.FullCustom);
        }

        // Resolution & Color Depth
        if (asset.RdpWidth.HasValue && asset.RdpHeight.HasValue)
        {
            TxtWidth.Text = asset.RdpWidth.Value.ToString();
            TxtHeight.Text = asset.RdpHeight.Value.ToString();
            CmbResolutionMode.SelectedIndex = 3; // Custom
        }
        else
        {
            CmbResolutionMode.SelectedIndex = 0; // Container fit
        }

        int colorDepth = asset.RdpColorDepth ?? 32;
        CmbColorDepth.SelectedItem = CmbColorDepth.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == colorDepth.ToString()) ?? CmbColorDepth.Items[4];

        // Redirects
        ChkDriveMap.IsChecked = asset.RedirectDrives ?? false;
        ChkAudio.IsChecked = asset.RedirectAudio ?? true;
        ChkClipboard.IsChecked = asset.RedirectClipboard ?? true;

        // Protocol Specific
        if (asset.Protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase))
        {
            PanelRdpOnly.Visibility = Visibility.Collapsed;
            PanelSshWindowOnly.Visibility = Visibility.Visible;
            PanelRdpSpecific.Visibility = Visibility.Collapsed;
            PanelSshSpecific.Visibility = Visibility.Visible;
            TxtSshPort.Text = asset.Port.ToString();
            TxtSshUsername.Text = asset.Username;
            TxtSshKeyPath.Text = asset.SshKeyPath ?? string.Empty;
            TxtSshWidth.Text = (asset.SshWindowWidth ?? _currentTemplate?.SshWindowWidth ?? SshWindow.DefaultWidth).ToString();
            TxtSshHeight.Text = (asset.SshWindowHeight ?? _currentTemplate?.SshWindowHeight ?? SshWindow.DefaultHeight).ToString();
        }
        else if (asset.Protocol.Equals("vnc", StringComparison.OrdinalIgnoreCase))
        {
            // VNC 连接参数由外装客户端决定，这里不提供 RDP/SSH 专属项
            PanelRdpOnly.Visibility = Visibility.Collapsed;
            PanelSshWindowOnly.Visibility = Visibility.Collapsed;
            PanelRdpSpecific.Visibility = Visibility.Collapsed;
            PanelSshSpecific.Visibility = Visibility.Collapsed;
        }
        else
        {
            PanelRdpOnly.Visibility = Visibility.Visible;
            PanelSshWindowOnly.Visibility = Visibility.Collapsed;
            PanelRdpSpecific.Visibility = Visibility.Visible;
            PanelSshSpecific.Visibility = Visibility.Collapsed;
            TxtRdpGateway.Text = _currentTemplate?.RdpGateway ?? string.Empty;
        }

        Visibility = Visibility.Visible;
        UpdateEffectiveParamsPreview();
    }

    public void BindTemplate(ConnectionTemplate template)
    {
        _currentTemplate = template;
        _currentAsset = null;
        _boundTemplate = template;

        TxtAssetName.Text = L10n.Format("Drawer_TemplatePrefix", template.Name);
        TxtTargetAddress.Text = L10n.Format("Drawer_PortLabel", template.Port);
        TxtProtocolTag.Text = template.Protocol.ToUpperInvariant();
        SetSource(ParamSourceKind.TemplateOnly);
        CmbTemplates.Visibility = Visibility.Collapsed;

        TxtWidth.Text = template.RdpWidth.ToString();
        TxtHeight.Text = template.RdpHeight.ToString();
        CmbResolutionMode.SelectedIndex = 3;

        int colorDepth = template.RdpColorDepth;
        CmbColorDepth.SelectedItem = CmbColorDepth.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == colorDepth.ToString()) ?? CmbColorDepth.Items[4];

        ChkDriveMap.IsChecked = template.RedirectDrives;
        ChkAudio.IsChecked = template.RedirectAudio;
        ChkClipboard.IsChecked = template.RedirectClipboard;

        if (template.Protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase))
        {
            PanelRdpOnly.Visibility = Visibility.Collapsed;
            PanelSshWindowOnly.Visibility = Visibility.Visible;
            PanelRdpSpecific.Visibility = Visibility.Collapsed;
            PanelSshSpecific.Visibility = Visibility.Visible;
            TxtSshPort.Text = template.Port.ToString();
            TxtSshUsername.Text = template.Username;
            TxtSshKeyPath.Text = template.SshKeyPath ?? string.Empty;
            TxtSshWidth.Text = template.SshWindowWidth.ToString();
            TxtSshHeight.Text = template.SshWindowHeight.ToString();
        }
        else if (template.Protocol.Equals("vnc", StringComparison.OrdinalIgnoreCase))
        {
            PanelRdpOnly.Visibility = Visibility.Collapsed;
            PanelSshWindowOnly.Visibility = Visibility.Collapsed;
            PanelRdpSpecific.Visibility = Visibility.Collapsed;
            PanelSshSpecific.Visibility = Visibility.Collapsed;
        }
        else
        {
            PanelRdpOnly.Visibility = Visibility.Visible;
            PanelSshWindowOnly.Visibility = Visibility.Collapsed;
            PanelRdpSpecific.Visibility = Visibility.Visible;
            PanelSshSpecific.Visibility = Visibility.Collapsed;
            TxtRdpGateway.Text = template.RdpGateway;
        }

        Visibility = Visibility.Visible;
        UpdateEffectiveParamsPreview();
    }

    private void UpdateEffectiveParamsPreview()
    {
        if (_currentAsset == null && _boundTemplate == null) return;

        bool isRdp = _currentAsset?.Protocol.Equals("rdp", StringComparison.OrdinalIgnoreCase) ?? 
                     _boundTemplate?.Protocol.Equals("rdp", StringComparison.OrdinalIgnoreCase) ?? true;

        if (!isRdp)
        {
            PanelEffectiveParams.Visibility = Visibility.Collapsed;
            return;
        }
        PanelEffectiveParams.Visibility = Visibility.Visible;

        // Resolution
        var (resWidth, resHeight, resSource) = GetEffectiveResolution();
        TxtEffectiveResolution.Text = $"{resWidth} × {resHeight}";
        TxtEffectiveResolutionSource.Text = GetSourceIcon(resSource);
        TxtEffectiveResolutionSource.Foreground = GetSourceBrush(resSource);

        // Color Depth
        var (colorDepth, depthSource) = GetEffectiveColorDepth();
        TxtEffectiveColorDepth.Text = $"{colorDepth} 位";
        TxtEffectiveColorDepthSource.Text = GetSourceIcon(depthSource);
        TxtEffectiveColorDepthSource.Foreground = GetSourceBrush(depthSource);

        // Gateway
        var (gateway, gwSource) = GetEffectiveGateway();
        TxtEffectiveGateway.Text = string.IsNullOrWhiteSpace(gateway) ? "未设置" : gateway;
        TxtEffectiveGatewaySource.Text = GetSourceIcon(gwSource);
        TxtEffectiveGatewaySource.Foreground = GetSourceBrush(gwSource);

        // Drive Redirect
        var (drive, driveSource) = GetEffectiveRedirectDrive();
        TxtEffectiveDrive.Text = drive ? "启用" : "禁用";
        TxtEffectiveDriveSource.Text = GetSourceIcon(driveSource);
        TxtEffectiveDriveSource.Foreground = GetSourceBrush(driveSource);

        // Audio Redirect
        var (audio, audioSource) = GetEffectiveRedirectAudio();
        TxtEffectiveAudio.Text = audio ? "启用" : "禁用";
        TxtEffectiveAudioSource.Text = GetSourceIcon(audioSource);
        TxtEffectiveAudioSource.Foreground = GetSourceBrush(audioSource);

        // Clipboard Redirect
        var (clipboard, clipSource) = GetEffectiveRedirectClipboard();
        TxtEffectiveClipboard.Text = clipboard ? "启用" : "禁用";
        TxtEffectiveClipboardSource.Text = GetSourceIcon(clipSource);
        TxtEffectiveClipboardSource.Foreground = GetSourceBrush(clipSource);
    }

    private (int width, int height, ParamSource source) GetEffectiveResolution()
    {
        if (int.TryParse(TxtWidth.Text, out var w) && int.TryParse(TxtHeight.Text, out var h))
        {
            if (_currentAsset?.RdpWidth.HasValue == true && _currentAsset.RdpHeight.HasValue == true)
            {
                return (w, h, ParamSource.Asset);
            }
            if (_currentTemplate != null)
            {
                return (w, h, ParamSource.Template);
            }
        }
        return (1920, 1080, ParamSource.Fallback);
    }

    private (int depth, ParamSource source) GetEffectiveColorDepth()
    {
        if (CmbColorDepth.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var depth))
        {
            if (_currentAsset?.RdpColorDepth.HasValue == true)
            {
                return (depth, ParamSource.Asset);
            }
            if (_currentTemplate != null)
            {
                return (depth, ParamSource.Template);
            }
        }
        return (32, ParamSource.Fallback);
    }

    private (string gateway, ParamSource source) GetEffectiveGateway()
    {
        var gw = TxtRdpGateway.Text;
        if (!string.IsNullOrWhiteSpace(gw))
        {
            if (_currentAsset != null && !string.IsNullOrWhiteSpace(_currentTemplate?.RdpGateway))
            {
                return (gw, ParamSource.Template);
            }
            return (gw, ParamSource.Asset);
        }
        return (string.Empty, ParamSource.Fallback);
    }

    private (bool enabled, ParamSource source) GetEffectiveRedirectDrive()
    {
        bool value = ChkDriveMap.IsChecked ?? false;
        if (_currentAsset?.RedirectDrives.HasValue == true)
        {
            return (value, ParamSource.Asset);
        }
        if (_currentTemplate != null)
        {
            return (value, ParamSource.Template);
        }
        return (value, ParamSource.Fallback);
    }

    private (bool enabled, ParamSource source) GetEffectiveRedirectAudio()
    {
        bool value = ChkAudio.IsChecked ?? true;
        if (_currentAsset?.RedirectAudio.HasValue == true)
        {
            return (value, ParamSource.Asset);
        }
        if (_currentTemplate != null)
        {
            return (value, ParamSource.Template);
        }
        return (value, ParamSource.Fallback);
    }

    private (bool enabled, ParamSource source) GetEffectiveRedirectClipboard()
    {
        bool value = ChkClipboard.IsChecked ?? true;
        if (_currentAsset?.RedirectClipboard.HasValue == true)
        {
            return (value, ParamSource.Asset);
        }
        if (_currentTemplate != null)
        {
            return (value, ParamSource.Template);
        }
        return (value, ParamSource.Fallback);
    }

    private string GetSourceIcon(ParamSource source) => source switch
    {
        ParamSource.Asset => "🔵 资产配置",
        ParamSource.Template => "🟢 绑定模板",
        ParamSource.Fallback => "⚪ 顶栏兜底",
        _ => ""
    };

    private System.Windows.Media.Brush GetSourceBrush(ParamSource source)
    {
        return source switch
        {
            ParamSource.Asset => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x42, 0x85, 0xF4)),
            ParamSource.Template => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x34, 0xA8, 0x53)),
            ParamSource.Fallback => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E)),
            _ => System.Windows.Media.Brushes.Gray
        };
    }

    private void CmbTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_currentAsset == null) return;

        var selectedIndex = CmbTemplates.SelectedIndex;
        
        // 索引 0 是"（无模板）"
        if (selectedIndex == 0)
        {
            _currentTemplate = null;
            SetSource(ParamSourceKind.FullCustom);
            return;
        }

        // 索引 >= 1 对应 _templates[index - 1]
        if (selectedIndex > 0 && selectedIndex <= _templates.Count)
        {
            var tmpl = _templates[selectedIndex - 1];
            _currentTemplate = tmpl;
            SetSource(ParamSourceKind.Inherited, tmpl.Name);

            // Populate values from template if asset didn't override explicitly
            TxtWidth.Text = tmpl.RdpWidth.ToString();
            TxtHeight.Text = tmpl.RdpHeight.ToString();
            ChkDriveMap.IsChecked = tmpl.RedirectDrives;
            ChkAudio.IsChecked = tmpl.RedirectAudio;
            ChkClipboard.IsChecked = tmpl.RedirectClipboard;
            if (tmpl.Protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase))
            {
                TxtSshPort.Text = tmpl.Port.ToString();
                TxtSshUsername.Text = tmpl.Username;
                TxtSshKeyPath.Text = tmpl.SshKeyPath;
                TxtSshWidth.Text = tmpl.SshWindowWidth.ToString();
                TxtSshHeight.Text = tmpl.SshWindowHeight.ToString();
            }
            else
            {
                TxtRdpGateway.Text = tmpl.RdpGateway;
            }
            UpdateEffectiveParamsPreview();
        }
    }

    private void CmbResolutionMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbResolutionMode.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            if (tag == "1080p")
            {
                TxtWidth.Text = "1920";
                TxtHeight.Text = "1080";
            }
            else if (tag == "2k")
            {
                TxtWidth.Text = "2560";
                TxtHeight.Text = "1440";
            }
            else if (tag == "4k")
            {
                TxtWidth.Text = "3840";
                TxtHeight.Text = "2160";
            }
            UpdateEffectiveParamsPreview();
        }
    }

    private void BtnBrowseSshKey_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = L10n["Dlg_BrowseKey"],
            Filter = L10n["Drawer_KeyFilter"]
        };
        if (dlg.ShowDialog() == true)
        {
            TxtSshKeyPath.Text = dlg.FileName;
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAsset != null)
        {
            int width = int.TryParse(TxtWidth.Text, out var w) ? w : 1920;
            int height = int.TryParse(TxtHeight.Text, out var h) ? h : 1080;
            int colorDepth = 32;
            if (CmbColorDepth.SelectedItem is ComboBoxItem cbi && int.TryParse(cbi.Tag?.ToString(), out var cd))
            {
                colorDepth = cd;
            }

            var isSsh = _currentAsset.Protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase);

            // Update asset properties
            var updatedModel = _currentAsset with
            {
                TemplateId = _currentTemplate?.Id,
                RdpWidth = width,
                RdpHeight = height,
                RdpColorDepth = colorDepth,
                // SSH 资产的窗口尺寸单独存储，独立窗口打开时按此尺寸
                SshWindowWidth = isSsh ? ParsePositiveInt(TxtSshWidth.Text) : _currentAsset.SshWindowWidth,
                SshWindowHeight = isSsh ? ParsePositiveInt(TxtSshHeight.Text) : _currentAsset.SshWindowHeight,
                RedirectDrives = ChkDriveMap.IsChecked ?? false,
                RedirectAudio = ChkAudio.IsChecked ?? true,
                RedirectClipboard = ChkClipboard.IsChecked ?? true,
                SshKeyPath = TxtSshKeyPath.Text,
                Port = isSsh && int.TryParse(TxtSshPort.Text, out var sp) ? sp : _currentAsset.Port,
                Username = isSsh ? TxtSshUsername.Text : _currentAsset.Username
            };

            _currentAsset = updatedModel;
            Applied?.Invoke(updatedModel);
        }

        Visibility = Visibility.Collapsed;
    }

    private static int? ParsePositiveInt(string text) =>
        int.TryParse(text.Trim(), out var value) && value > 0 ? value : null;

    private void BtnSshWindowPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string preset }) return;

        var primaryWidth = (int)SystemParameters.PrimaryScreenWidth;
        var primaryHeight = (int)SystemParameters.PrimaryScreenHeight;

        var (width, height) = preset switch
        {
            "quarter" => (primaryWidth / 2, primaryHeight / 2),
            "sixth" => (primaryWidth / 3, primaryHeight / 2),
            "ninth" => (primaryWidth / 3, primaryHeight / 3),
            "splith" => (primaryWidth / 2, primaryHeight),
            _ => (primaryWidth, primaryHeight / 2)
        };

        TxtSshWidth.Text = width.ToString();
        TxtSshHeight.Text = height.ToString();
    }

    private void BtnSaveAsTemplate_Click(object sender, RoutedEventArgs e)
    {
        int width = int.TryParse(TxtWidth.Text, out var w) ? w : 1920;
        int height = int.TryParse(TxtHeight.Text, out var h) ? h : 1080;
        int colorDepth = 32;
        if (CmbColorDepth.SelectedItem is ComboBoxItem cbi && int.TryParse(cbi.Tag?.ToString(), out var cd))
        {
            colorDepth = cd;
        }

        var newTemplate = new ConnectionTemplate
        {
            Id = Guid.NewGuid(),
            Name = _currentAsset != null ? L10n.Format("Drawer_TemplateNameSuffix", _currentAsset.Name) : L10n["Drawer_NewTemplate"],
            Protocol = _currentAsset?.Protocol ?? "rdp",
            Port = _currentAsset?.Port ?? 3389,
            Username = _currentAsset?.Username ?? string.Empty,
            RdpWidth = width,
            RdpHeight = height,
            RdpColorDepth = colorDepth,
            RdpGateway = TxtRdpGateway.Text,
            SshWindowWidth = ParsePositiveInt(TxtSshWidth.Text) ?? SshWindow.DefaultWidth,
            SshWindowHeight = ParsePositiveInt(TxtSshHeight.Text) ?? SshWindow.DefaultHeight,
            RedirectDrives = ChkDriveMap.IsChecked ?? false,
            RedirectAudio = ChkAudio.IsChecked ?? true,
            RedirectClipboard = ChkClipboard.IsChecked ?? true,
            SshKeyPath = TxtSshKeyPath.Text
        };

        TemplateSaved?.Invoke(newTemplate);
        MessageBox.Show(L10n["Drawer_TemplateSaved"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Visibility = Visibility.Collapsed;
        Closed?.Invoke();
    }

    private void TxtResolution_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateEffectiveParamsPreview();
    }

    private void CmbColorDepth_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateEffectiveParamsPreview();
    }

    private void ChkRedirect_Changed(object sender, RoutedEventArgs e)
    {
        UpdateEffectiveParamsPreview();
    }

    private void TxtRdpGateway_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateEffectiveParamsPreview();
    }
}
