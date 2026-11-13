using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RemotePilot.Services;

namespace RemotePilot.Controls;

/// <summary>
/// 设置页：集中配置外装 VNC 客户端（程序路径 + 启动参数模板）与 SSH 终端外观（字号、配色方案）。
/// </summary>
public partial class SettingsView : UserControl
{
    // Localization Keys
    public const string ResTitle = "Settings_Title";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    /// <summary>用户在设置页点击保存时触发（VNC 与 SSH 外观一起保存）。</summary>
    public event Action? Saved;

    public string VncViewerPath => TxtViewerPath.Text.Trim();
    public string VncArgumentTemplate => TxtArgs.Text.Trim();
    public double SshFontSize => SliderFontSize.Value;
    public int SshSchemeIndex => CmbScheme.SelectedIndex;
    public bool SshStandaloneWindow => ChkSshStandalone.IsChecked == true;

    public SettingsView()
    {
        InitializeComponent();

        LocalizationManager.Instance.LanguageChanged += (s, e) =>
        {
            ApplyStaticTexts();
            RefreshViewerStatus();
        };

        ApplyStaticTexts();
        RefreshViewerStatus();
    }

    /// <summary>由主窗口在加载设置时调用，回填当前值。</summary>
    public void LoadSettings(string viewerPath, string argumentTemplate, double sshFontSize, int sshScheme, bool sshStandaloneWindow)
    {
        TxtViewerPath.Text = viewerPath ?? "";
        TxtArgs.Text = argumentTemplate ?? "";
        SliderFontSize.Value = Math.Clamp(sshFontSize, SshTerminalView.MinFontSize, SshTerminalView.MaxFontSize);
        CmbScheme.SelectedIndex = Math.Clamp(sshScheme, 0, CmbScheme.Items.Count - 1);
        ChkSshStandalone.IsChecked = sshStandaloneWindow;
        RefreshViewerStatus();
        UpdatePreview();
    }

    private void ApplyStaticTexts()
    {
        TxtSubtitle.Text = L10n["Settings_Subtitle"];
        TxtVncSection.Text = L10n["Settings_VncSection"];
        TxtSshSection.Text = L10n["Settings_SshSection"];
        TxtViewerLabel.Text = L10n["Help_ViewerLabel"];
        TxtArgsLabel.Text = L10n["Help_ArgsLabel"];
        TxtArgsHint.Text = L10n["Help_ArgsHint"];
        BtnBrowse.Content = L10n["Help_BtnBrowse"];
        BtnDetect.Content = L10n["Help_BtnDetect"];
        BtnResetArgs.Content = L10n["Help_BtnResetArgs"];
        TxtSshFontSize.Text = L10n["Settings_SshFontSize"];
        TxtSshScheme.Text = L10n["Settings_SshScheme"];
        ChkSshStandalone.Content = L10n["Settings_SshStandalone"];
        BtnSaveSettings.Content = L10n["Settings_BtnSave"];
        RebuildSchemeItems();
        UpdateFontSizeValue();
    }

    /// <summary>按当前语言重建配色方案下拉项，并尽量保留原选中档位。</summary>
    private void RebuildSchemeItems()
    {
        int index = CmbScheme.SelectedIndex;
        CmbScheme.Items.Clear();
        CmbScheme.Items.Add(L10n["Term_SchemeDark"]);
        CmbScheme.Items.Add(L10n["Term_SchemeGreen"]);
        CmbScheme.Items.Add(L10n["Term_SchemeLight"]);
        CmbScheme.SelectedIndex = index >= 0 && index < CmbScheme.Items.Count ? index : 0;
    }

    private void UpdateFontSizeValue()
    {
        if (TxtFontSizeValue is not null)
        {
            TxtFontSizeValue.Text = ((int)SliderFontSize.Value).ToString();
        }
    }

    private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateFontSizeValue();

    private void CmbScheme_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    /// <summary>用当前配色档位的终端背景色实时预览（与 SshTerminalView 的取值保持一致）。</summary>
    private void UpdatePreview()
    {
        if (PreviewSwatch is null) return;
        string hex = CmbScheme.SelectedIndex switch
        {
            1 => "#0B1F0B",
            2 => "#F7F7F7",
            _ => "#1E1E1E"
        };
        PreviewSwatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    private void RefreshViewerStatus()
    {
        var configured = TxtViewerPath.Text.Trim();

        if (configured.Length > 0 && !File.Exists(configured))
        {
            SetStatus(L10n["Help_ViewerNotExists"], "Danger");
            return;
        }

        var resolved = VncLauncher.ResolveViewer(configured);
        if (resolved == null)
        {
            SetStatus(L10n["Help_ViewerNotDetected"], "MutedForeground");
            return;
        }

        SetStatus(L10n.Format("Help_ViewerDetected", resolved.ExecutablePath, VncLauncher.DescribeFamily(resolved.Family)), "Success");
    }

    private void SetStatus(string text, string brushKey)
    {
        TxtViewerStatus.Text = text;
        TxtViewerStatus.Foreground = (Brush)FindResource(brushKey);
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = L10n["Help_BtnBrowse"],
            Filter = L10n["Help_ViewerFilter"]
        };

        if (dlg.ShowDialog() == true)
        {
            TxtViewerPath.Text = dlg.FileName;
            RefreshViewerStatus();
        }
    }

    private void BtnDetect_Click(object sender, RoutedEventArgs e)
    {
        // 清空自定义路径后再探测，避免探测被旧路径短路
        var resolved = VncLauncher.ResolveViewer(null);
        if (resolved != null)
        {
            TxtViewerPath.Text = resolved.ExecutablePath;
            if (string.IsNullOrWhiteSpace(TxtArgs.Text))
            {
                TxtArgs.Text = VncLauncher.DefaultArgumentTemplate(resolved.Family);
            }
        }

        RefreshViewerStatus();
    }

    private void BtnResetArgs_Click(object sender, RoutedEventArgs e)
    {
        var family = VncLauncher.ResolveViewer(TxtViewerPath.Text)?.Family ?? VncViewerFamily.Unknown;
        TxtArgs.Text = VncLauncher.DefaultArgumentTemplate(family);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        Saved?.Invoke();
        RefreshViewerStatus();
    }
}
