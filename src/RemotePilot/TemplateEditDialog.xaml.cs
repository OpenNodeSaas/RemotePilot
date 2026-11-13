using System;
using System.Windows;
using System.Windows.Controls;
using RemotePilot.Models;
using RemotePilot.Services;

namespace RemotePilot;

public partial class TemplateEditDialog : Window
{
    public const string ResDlgTitle = "TmplEdit_DlgTitle";
    public const string ResDlgHeader = "TmplEdit_DlgHeader";
    public const string ResFldName = "Fld_Name";
    public const string ResFldProtocol = "Fld_Protocol";
    public const string ResFldPort = "Fld_Port";
    public const string ResFldUsername = "Fld_Username";
    public const string ResRdpSettings = "TmplMgmt_RdpSettings";
    public const string ResFldResolution = "TmplMgmt_Resolution";
    public const string ResFldColorDepth = "TmplMgmt_ColorDepth";
    public const string ResFldGateway = "TmplMgmt_Gateway";
    public const string ResRdpRedirect = "Dlg_RdpRedirect";
    public const string ResSshSettings = "TmplMgmt_SshSettings";
    public const string ResFldSshWindowSize = "Setting_SshWindowSize";
    public const string ResDriveMap = "Dlg_DriveMap";
    public const string ResAudio = "Dlg_Audio";
    public const string ResClipboard = "Dlg_Clipboard";
    public const string ResBtnOk = "Btn_Save";
    public const string ResBtnCancel = "Btn_Cancel";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    private readonly ConnectionTemplate? _existing;
    public ConnectionTemplate? Result { get; private set; }

    public TemplateEditDialog(ConnectionTemplate? existing = null)
    {
        InitializeComponent();
        _existing = existing;

        if (existing != null)
        {
            TxtName.Text = existing.Name;
            
            for (int i = 0; i < CmbProtocol.Items.Count; i++)
            {
                if (CmbProtocol.Items[i] is ComboBoxItem item && 
                    item.Content?.ToString()?.Equals(existing.Protocol, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CmbProtocol.SelectedIndex = i;
                    break;
                }
            }

            TxtPort.Text = existing.Port.ToString();
            TxtUsername.Text = existing.Username;
            TxtRdpWidth.Text = existing.RdpWidth.ToString();
            TxtRdpHeight.Text = existing.RdpHeight.ToString();

            for (int i = 0; i < CmbColorDepth.Items.Count; i++)
            {
                if (CmbColorDepth.Items[i] is ComboBoxItem item && 
                    item.Tag?.ToString() == existing.RdpColorDepth.ToString())
                {
                    CmbColorDepth.SelectedIndex = i;
                    break;
                }
            }

            TxtRdpGateway.Text = existing.RdpGateway;
            TxtSshWindowWidth.Text = existing.SshWindowWidth.ToString();
            TxtSshWindowHeight.Text = existing.SshWindowHeight.ToString();
            ChkRedirectDrives.IsChecked = existing.RedirectDrives;
            ChkRedirectAudio.IsChecked = existing.RedirectAudio;
            ChkRedirectClipboard.IsChecked = existing.RedirectClipboard;
        }

        CmbProtocol_SelectionChanged(this, null!);
    }

    private void CmbProtocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelRdpSettings == null || TxtPort == null) return;

        var proto = (CmbProtocol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "rdp";
        var isSsh = proto.Equals("ssh", StringComparison.OrdinalIgnoreCase);
        var isVnc = proto.Equals("vnc", StringComparison.OrdinalIgnoreCase);
        // VNC 不使用 RDP 的分辨率/网关/重定向
        PanelRdpSettings.Visibility = isSsh || isVnc ? Visibility.Collapsed : Visibility.Visible;
        // SSH 独立窗口模式才有窗口尺寸
        PanelSshSettings.Visibility = isSsh ? Visibility.Visible : Visibility.Collapsed;

        var port = TxtPort.Text.Trim();
        if (port.Length == 0 || port == "3389" || port == "22" || port == "5900")
            TxtPort.Text = isSsh ? "22" : (isVnc ? "5900" : "3389");
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show(
                L10n["TmplEdit_NameRequired"] ?? "请输入模板名称",
                L10n["TmplEdit_ValidationError"] ?? "验证错误",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        if (!int.TryParse(TxtPort.Text, out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show(
                L10n["TmplEdit_InvalidPort"] ?? "端口号必须在 1-65535 之间",
                L10n["TmplEdit_ValidationError"] ?? "验证错误",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            TxtPort.Focus();
            return;
        }

        if (!int.TryParse(TxtRdpWidth.Text, out var width) || width < 640 || width > 7680)
        {
            MessageBox.Show(
                L10n["TmplEdit_InvalidWidth"] ?? "宽度必须在 640-7680 之间",
                L10n["TmplEdit_ValidationError"] ?? "验证错误",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            TxtRdpWidth.Focus();
            return;
        }

        if (!int.TryParse(TxtRdpHeight.Text, out var height) || height < 480 || height > 4320)
        {
            MessageBox.Show(
                L10n["TmplEdit_InvalidHeight"] ?? "高度必须在 480-4320 之间",
                L10n["TmplEdit_ValidationError"] ?? "验证错误",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            TxtRdpHeight.Focus();
            return;
        }

        var colorDepth = int.Parse((CmbColorDepth.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "32");
        var protocol = (CmbProtocol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "rdp";

        // SSH 模板额外校验独立窗口尺寸（仅在 SSH 面板可见时生效）
        int sshWidth = 0, sshHeight = 0;
        if (PanelSshSettings.Visibility == Visibility.Visible)
        {
            if (!int.TryParse(TxtSshWindowWidth.Text, out sshWidth) || sshWidth < 320 || sshWidth > 7680)
            {
                MessageBox.Show(
                    L10n["TmplEdit_InvalidWidth"] ?? "宽度必须在 640-7680 之间",
                    L10n["TmplEdit_ValidationError"] ?? "验证错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                TxtSshWindowWidth.Focus();
                return;
            }

            if (!int.TryParse(TxtSshWindowHeight.Text, out sshHeight) || sshHeight < 240 || sshHeight > 4320)
            {
                MessageBox.Show(
                    L10n["TmplEdit_InvalidHeight"] ?? "高度必须在 480-4320 之间",
                    L10n["TmplEdit_ValidationError"] ?? "验证错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                TxtSshWindowHeight.Focus();
                return;
            }
        }

        Result = (_existing ?? new ConnectionTemplate()) with
        {
            Name = TxtName.Text.Trim(),
            Protocol = protocol,
            Port = port,
            Username = TxtUsername.Text.Trim(),
            RdpWidth = width,
            RdpHeight = height,
            RdpColorDepth = colorDepth,
            RdpGateway = TxtRdpGateway.Text.Trim(),
            SshWindowWidth = sshWidth > 0 ? sshWidth : (_existing?.SshWindowWidth ?? 1000),
            SshWindowHeight = sshHeight > 0 ? sshHeight : (_existing?.SshWindowHeight ?? 640),
            RedirectDrives = ChkRedirectDrives.IsChecked == true,
            RedirectAudio = ChkRedirectAudio.IsChecked == true,
            RedirectClipboard = ChkRedirectClipboard.IsChecked == true
        };

        DialogResult = true;
    }
}
