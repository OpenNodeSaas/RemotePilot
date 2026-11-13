using System.Windows;
using RemotePilot.Controls;
using RemotePilot.Models;

namespace RemotePilot;

public partial class SshWindow : Window
{
    private readonly SshTerminalHost _host;

    public SshWindow(ServerAsset asset, string? password, string? privateKeyPath, string? privateKeyPassphrase,
        ServerAsset? jumpAsset, string? jumpPassword)
    {
        InitializeComponent();

        // 标题格式与 RDP 一致：资产名称 (主机地址)
        Title = $"{asset.Name} ({asset.Host})";

        _host = new SshTerminalHost(asset, password, privateKeyPath, privateKeyPassphrase, jumpAsset, jumpPassword);
        RootGrid.Children.Add(_host);

        Closed += (s, e) => _host.DisposeSession();
    }
}
