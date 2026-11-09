using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RemotePilot.Models;
using RemotePilot.Services;
using Microsoft.Win32;

namespace RemotePilot;

public partial class ServerEditDialog : Window
{
    public const string ResDlgTitle = "Dlg_Title";
    public const string ResDlgHeader = "Dlg_Header";
    public const string ResFldName = "Fld_Name";
    public const string ResFldGroup = "Fld_Group";
    public const string ResFldHost = "Fld_Host";
    public const string ResFldPort = "Fld_Port";
    public const string ResFldProtocol = "Fld_Protocol";
    public const string ResFldTemplate = "Fld_Template";
    public const string ResFldUsername = "Fld_Username";
    public const string ResFldPassword = "Fld_Password";
    public const string ResFldRememberPassword = "Fld_RememberPassword";
    public const string ResFldKeyPath = "Fld_KeyPath";
    public const string ResFldPassphrase = "Fld_Passphrase";
    public const string ResFldJumpHost = "Fld_JumpHost";
    public const string ResFldOwner = "Fld_Owner";
    public const string ResFldExpiresOn = "Fld_ExpiresOn";
    public const string ResFldNote1 = "Fld_Note1";
    public const string ResFldNote2 = "Fld_Note2";
    public const string ResFldNote3 = "Fld_Note3";
    public const string ResRdpRedirect = "Dlg_RdpRedirect";
    public const string ResDriveMap = "Dlg_DriveMap";
    public const string ResAudio = "Dlg_Audio";
    public const string ResClipboard = "Dlg_Clipboard";
    public const string ResBtnBrowse = "Btn_Browse";
    public const string ResBtnOk = "Btn_Save";
    public const string ResBtnCancel = "Btn_Cancel";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    public ServerAsset Asset { get; private set; }
    public string? PasswordInput => TxtPassword.Password;
    public string? KeyPassphraseInput => TxtKeyPassphrase.Password;
    public bool RememberPassword => ChkRememberPassword.IsChecked == true;

    public ServerEditDialog(ServerAsset? existing = null, System.Collections.Generic.IEnumerable<string>? groups = null, bool defaultRememberPassword = false,
        System.Collections.Generic.IEnumerable<ServerAsset>? jumpCandidates = null, System.Collections.Generic.IEnumerable<ConnectionTemplate>? templates = null)
    {
        InitializeComponent();

        ChkRememberPassword.IsChecked = defaultRememberPassword;

        // 连接模板下拉：首项“无模板”，其余展示摘要
        var templateList = templates?.ToList() ?? new System.Collections.Generic.List<ConnectionTemplate>();
        CmbTemplate.Items.Add(new ComboBoxItem { Content = L10n["Fld_TemplateNone"], Tag = null });
        foreach (var t in templateList)
        {
            CmbTemplate.Items.Add(new ComboBoxItem { Content = t.GetDisplaySummary(), Tag = t.Id });
        }
        CmbTemplate.SelectedIndex = 0;
        if (existing?.TemplateId is Guid templateId)
        {
            foreach (var item in CmbTemplate.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is Guid id && id == templateId)
                {
                    CmbTemplate.SelectedItem = cbi;
                    break;
                }
            }
        }

        // 分组下拉给出已有分组，同时允许直接输入新分组
        if (groups != null)
        {
            foreach (var g in groups.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct())
            {
                CmbGroup.Items.Add(g);
            }
        }

        // 跳板机下拉：引用已有资产（SSH 连接时经它中转），默认“不使用”
        CmbJumpHost.Items.Add(new ComboBoxItem { Content = L10n["Fld_JumpHostNone"], Tag = null });
        if (jumpCandidates != null)
        {
            foreach (var candidate in jumpCandidates)
            {
                if (existing != null && candidate.Id == existing.Id) continue;
                CmbJumpHost.Items.Add(new ComboBoxItem
                {
                    Content = $"{candidate.Name} ({candidate.Host}:{candidate.Port})",
                    Tag = candidate.Id
                });
            }
        }
        CmbJumpHost.SelectedIndex = 0;
        if (existing?.JumpAssetId is Guid jumpId)
        {
            foreach (var item in CmbJumpHost.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is Guid id && id == jumpId)
                {
                    CmbJumpHost.SelectedItem = cbi;
                    break;
                }
            }
        }

        if (existing != null)
        {
            Asset = existing;
            TxtName.Text = existing.Name;
            CmbGroup.Text = existing.Group;
            TxtHost.Text = existing.Host;
            TxtPort.Text = existing.Port.ToString();
            
            for (int i = 0; i < CmbProtocol.Items.Count; i++)
            {
                if (CmbProtocol.Items[i] is ComboBoxItem item && item.Content?.ToString()?.Equals(existing.Protocol, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CmbProtocol.SelectedIndex = i;
                    break;
                }
            }

            TxtUsername.Text = existing.Username;
            TxtPassword.Password = existing.Password ?? "";
            TxtSshKeyPath.Text = existing.SshKeyPath ?? "";
            TxtOwner.Text = existing.Owner;
            if (existing.ExpiresOn.HasValue)
            {
                DpExpiresOn.SelectedDate = existing.ExpiresOn.Value.ToDateTime(TimeOnly.MinValue);
            }
            TxtNote1.Text = existing.CustomNote1;
            TxtNote2.Text = existing.CustomNote2;
            TxtNote3.Text = existing.CustomNote3;

            ChkRedirectDrives.IsChecked = existing.RedirectDrives ?? false;
            ChkRedirectAudio.IsChecked = existing.RedirectAudio ?? true;
            ChkRedirectClipboard.IsChecked = existing.RedirectClipboard ?? false;
        }
        else
        {
            Asset = new ServerAsset();
        }

        // 初始化时按协议同步字段可见性（默认 RDP 隐藏 SSH 专属项）
        CmbProtocol_SelectionChanged(this, null!);
    }

    private void CmbProtocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelServerSshFields == null || TxtPort == null) return;

        var proto = (CmbProtocol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "rdp";
        var isSsh = proto.Equals("ssh", StringComparison.OrdinalIgnoreCase);
        PanelServerSshFields.Visibility = isSsh ? Visibility.Visible : Visibility.Collapsed;
        PanelRdpRedirect.Visibility = isSsh ? Visibility.Collapsed : Visibility.Visible;

        // 仅在端口仍为默认值时跟随协议切换，避免覆盖用户自定义端口
        var port = TxtPort.Text.Trim();
        if (port.Length == 0 || port == "3389" || port == "22")
            TxtPort.Text = isSsh ? "22" : "3389";
    }

    private void BtnBrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = L10n["Dlg_BrowseKey"],
            Filter = L10n["Dlg_AllFilesFilter"]
        };
        if (dlg.ShowDialog() == true)
        {
            TxtSshKeyPath.Text = dlg.FileName;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtHost.Text))
        {
            MessageBox.Show(L10n["Msg_HostRequired"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtHost.Focus();
            return;
        }

        if (!int.TryParse(TxtPort.Text.Trim(), out var port) || port <= 0 || port > 65535)
        {
            MessageBox.Show(L10n["Msg_PortInvalid"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPort.Focus();
            return;
        }

        DateOnly? expiresOn = null;
        if (DpExpiresOn.SelectedDate.HasValue)
        {
            expiresOn = DateOnly.FromDateTime(DpExpiresOn.SelectedDate.Value);
        }

        var proto = (CmbProtocol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "rdp";

        Asset = Asset with
        {
            Name = !string.IsNullOrWhiteSpace(TxtName.Text) ? TxtName.Text.Trim() : TxtHost.Text.Trim(),
            Group = CmbGroup.Text.Trim(),
            Host = TxtHost.Text.Trim(),
            Port = port,
            Protocol = proto,
            Username = TxtUsername.Text.Trim(),
            SshKeyPath = TxtSshKeyPath.Text.Trim(),
            Owner = TxtOwner.Text.Trim(),
            ExpiresOn = expiresOn,
            CustomNote1 = TxtNote1.Text.Trim(),
            CustomNote2 = TxtNote2.Text.Trim(),
            CustomNote3 = TxtNote3.Text.Trim(),
            RedirectDrives = ChkRedirectDrives.IsChecked ?? false,
            RedirectAudio = ChkRedirectAudio.IsChecked ?? true,
            RedirectClipboard = ChkRedirectClipboard.IsChecked ?? false,
            JumpAssetId = (CmbJumpHost.SelectedItem as ComboBoxItem)?.Tag as Guid?,
            TemplateId = (CmbTemplate.SelectedItem as ComboBoxItem)?.Tag as Guid?,
            Password = string.IsNullOrEmpty(TxtPassword.Password) ? null : TxtPassword.Password
        };

        DialogResult = true;
        Close();
    }
}
