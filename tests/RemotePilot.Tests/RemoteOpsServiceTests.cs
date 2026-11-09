using System.IO;
using RemotePilot.Models;
using RemotePilot.Services;
using Xunit;

namespace RemotePilot.Tests;

public class RemoteOpsServiceTests
{
    [Fact]
    public void TestIss7ColumnsImport()
    {
        // ISS7 面板导出列：分组,负责人,服务器名,IP,用户,PSW,状态,到期,备1,备2
        const string csv =
            "分组,负责人,服务器名,IP,用户,PSW,状态,到期,备1,备2\n" +
            "Jp,ZS,东京节点,45.12.34.56:13389,administrator,Secret123,1,2027-12-31,主用,";

        var result = AssetCsvService.ImportCsv(csv);

        Assert.Single(result.Assets);
        var asset = result.Assets[0];
        Assert.Equal("Jp", asset.Group);
        Assert.Equal("ZS", asset.Owner);
        Assert.Equal("东京节点", asset.Name);
        Assert.Equal("45.12.34.56", asset.Host);
        Assert.Equal(13389, asset.Port);
        Assert.Equal("administrator", asset.Username);
        Assert.Equal("Secret123", asset.Password);
        Assert.Equal(new DateOnly(2027, 12, 31), asset.ExpiresOn);
        Assert.Equal("主用", asset.CustomNote1);
    }

    [Fact]
    public async Task TestReachabilityProbeInvalidHost()
    {
        var probe = new ReachabilityProbe(2);
        var latency = await probe.ProbeSingleAsync("invalid-host-nonexistent-xyz", 3389, 500);
        Assert.Null(latency);
    }

    [Fact]
    public void TestRdpLauncherSanitizationAndGeneration()
    {
        // Test security validation against CR/LF injection
        Assert.Throws<ArgumentException>(() => RdpLauncher.SanitizeRdpValue("host\r\nevil:i:1"));
        Assert.Throws<ArgumentException>(() => RdpLauncher.SanitizeRdpValue("host\nattack"));

        var asset = new ServerAsset
        {
            Name = "Prod RDP",
            Host = "192.168.1.100",
            Port = 3389,
            Username = "admin",
            RdpWidth = 1920,
            RdpHeight = 1080,
            RdpColorDepth = 32,
            RedirectDrives = true,
            RedirectAudio = false,
            RedirectClipboard = true
        };

        var content = RdpLauncher.GenerateRdpContent(asset);
        Assert.Contains("full address:s:192.168.1.100:3389", content);
        Assert.Contains("server port:i:3389", content);
        Assert.Contains("username:s:admin", content);
        Assert.Contains("desktopwidth:i:1920", content);
        Assert.Contains("desktopheight:i:1080", content);
        Assert.Contains("session bpp:i:32", content);
        Assert.Contains("drivestoredirect:i:1", content);
        Assert.Contains("audiomode:i:2", content);
        Assert.Contains("redirectclipboard:i:1", content);
        Assert.Contains("prompt for credentials:i:1", content);
    }

    [Fact]
    public void TestRdpTemplateGatewayApplied()
    {
        // 资产绑定模板后，连接参数应取自模板（尤其是 RDP 网关）
        var asset = new ServerAsset { Name = "t", Host = "10.0.0.9", Port = 3389 };
        var template = new ConnectionTemplate
        {
            Name = "办公网",
            RdpGateway = "gw.example.com",
            RdpWidth = 2560,
            RdpHeight = 1440
        };

        var content = RdpLauncher.GenerateRdpContent(asset, template);

        Assert.Contains("gatewayhostname:s:gw.example.com", content);
        Assert.Contains("gatewayusagemethod:i:1", content);
        Assert.Contains("desktopwidth:i:2560", content);
        Assert.Contains("desktopheight:i:1440", content);
    }

}
