using System.Windows;
using RemotePilot.Services;

namespace RemotePilot;

public partial class ExportFieldsDialog : Window
{
    public const string ResTitle = "Dlg_ExportFieldsTitle";
    public const string ResHint = "ExpFld_Hint";
    public const string ResGroup = "Col_Group";
    public const string ResName = "Insp_Name";
    public const string ResIpPort = "Col_IpPort";
    public const string ResUsername = "Col_Username";
    public const string ResPassword = "Cred_Password";
    public const string ResExpires = "Col_ExpiresOn";
    public const string ResOwner = "Col_Owner";
    public const string ResNote1 = "Col_Note1";
    public const string ResNote2 = "Col_Note2";
    public const string ResNote3 = "Col_Note3";
    public const string ResProtocol = "Col_Protocol";
    public const string ResPort = "Insp_Port";
    public const string ResSshKeyPath = "Col_SshKeyPath";
    public const string ResPasswordWarn = "ExpFld_PasswordWarn";
    public const string ResBtnOk = "Btn_Save";
    public const string ResBtnCancel = "Btn_Cancel";

    public ExportFieldSelection SelectedFields { get; private set; } = new();

    public ExportFieldsDialog()
    {
        InitializeComponent();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        SelectedFields = new ExportFieldSelection
        {
            Group = ChkGroup.IsChecked == true,
            Name = ChkName.IsChecked == true,
            IpPort = ChkIpPort.IsChecked == true,
            Username = ChkUsername.IsChecked == true,
            Password = ChkPassword.IsChecked == true,
            Expires = ChkExpires.IsChecked == true,
            Owner = ChkOwner.IsChecked == true,
            Note1 = ChkNote1.IsChecked == true,
            Note2 = ChkNote2.IsChecked == true,
            Note3 = ChkNote3.IsChecked == true,
            Protocol = ChkProtocol.IsChecked == true,
            Port = ChkPort.IsChecked == true,
            SshKeyPath = ChkSshKeyPath.IsChecked == true,
        };
        DialogResult = true;
        Close();
    }
}
