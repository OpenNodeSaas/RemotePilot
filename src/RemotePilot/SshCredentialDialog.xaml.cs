using System.Windows;
using RemotePilot.Services;
using Microsoft.Win32;

namespace RemotePilot;

public partial class SshCredentialDialog : Window
{
    public const string ResCredTitle = "Cred_Title";
    public const string ResCredDialogHint = "Cred_DialogHint";
    public const string ResCredUsername = "Cred_UsernameLabel";
    public const string ResCredPassword = "Cred_PasswordLabel";
    public const string ResCredKeyPath = "Cred_KeyPathLabel";
    public const string ResCredConnect = "Cred_Connect";
    public const string ResBtnBrowse = "Btn_Browse";
    public const string ResBtnCancel = "Btn_Cancel";
    public const string ResCredRemember = "Cred_RememberLocal";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    public string Username => TxtUsername.Text.Trim();
    public string? Password => string.IsNullOrEmpty(TxtPassword.Password) ? null : TxtPassword.Password;
    public string? PrivateKeyPath => string.IsNullOrWhiteSpace(TxtKeyPath.Text) ? null : TxtKeyPath.Text.Trim();
    public bool RememberPassword => ChkRemember.IsChecked == true;

    public SshCredentialDialog(string? defaultUsername = null, bool offerRememberPassword = false)
    {
        InitializeComponent();
        TxtUsername.Text = string.IsNullOrWhiteSpace(defaultUsername) ? "root" : defaultUsername;
        ChkRemember.Visibility = offerRememberPassword ? Visibility.Visible : Visibility.Collapsed;
        ChkRemember.IsChecked = offerRememberPassword;
        Loaded += (s, e) => TxtPassword.Focus();
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = L10n["Dlg_BrowseKey"],
            Filter = L10n["Dlg_AllFilesFilter"]
        };
        if (dlg.ShowDialog() == true)
        {
            TxtKeyPath.Text = dlg.FileName;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            MessageBox.Show(L10n["Msg_SshUserRequired"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtUsername.Focus();
            return;
        }

        if (Password == null && PrivateKeyPath == null)
        {
            MessageBox.Show(L10n["Msg_SshCredRequired"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPassword.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }
}
