using System.IO;
using Renci.SshNet;
using RemotePilot.Models;

namespace RemotePilot.Services;

/// <summary>SSH 跳板连接：先登录跳板机，在它上面开一个本地端口转发，目标机通过 127.0.0.1:随机端口 访问。</summary>
public sealed class SshJumpConnection : IDisposable
{
    private readonly SshClient _jumpClient;
    private readonly ForwardedPortLocal _forwardedPort;

    private SshJumpConnection(SshClient jumpClient, ForwardedPortLocal forwardedPort)
    {
        _jumpClient = jumpClient;
        _forwardedPort = forwardedPort;
    }

    public string LoopbackHost => "127.0.0.1";
    public int LoopbackPort => (int)_forwardedPort.BoundPort;

    /// <summary>建立跳板连接；password / privateKeyPath 二选一。</summary>
    public static SshJumpConnection Create(ServerAsset jumpAsset, string? password, string? privateKeyPath, string targetHost, int targetPort)
    {
        var username = string.IsNullOrWhiteSpace(jumpAsset.Username) ? "root" : jumpAsset.Username;

        SshClient client;
        if (!string.IsNullOrWhiteSpace(privateKeyPath) && File.Exists(privateKeyPath))
        {
            client = new SshClient(jumpAsset.Host, jumpAsset.Port, username, new PrivateKeyFile(privateKeyPath));
        }
        else if (!string.IsNullOrWhiteSpace(password))
        {
            client = new SshClient(jumpAsset.Host, jumpAsset.Port, username, password);
        }
        else
        {
            throw new InvalidOperationException("jump credential missing");
        }

        client.Connect();

        // boundPort = 0 表示由系统分配空闲端口
        var forwardedPort = new ForwardedPortLocal("127.0.0.1", 0, targetHost, (uint)targetPort);
        client.AddForwardedPort(forwardedPort);
        forwardedPort.Start();

        return new SshJumpConnection(client, forwardedPort);
    }

    public void Dispose()
    {
        try { _forwardedPort.Stop(); } catch { }
        try { _jumpClient.Dispose(); } catch { }
    }
}
