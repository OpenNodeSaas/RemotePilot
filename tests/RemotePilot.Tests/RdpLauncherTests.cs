using System;
using System.IO;
using System.Text;
using RemotePilot.Models;
using RemotePilot.Services;
using Xunit;

namespace RemotePilot.Tests;

public class RdpLauncherTests
{
    private static ServerAsset SampleAsset(string host = "10.0.0.1") => new()
    {
        Name = "srv-a",
        Host = host,
        Port = 3389,
        Username = "admin",
        Protocol = "rdp"
    };

    [Fact]
    public void RedirectDrives_Enabled_WritesDriveStoreRedirectAll()
    {
        var asset = SampleAsset() with { RedirectDrives = true };

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.Contains("drivestoredirect:s:*", content);
    }

    [Fact]
    public void RedirectDrives_Disabled_OmitsKey()
    {
        var asset = SampleAsset() with { RedirectDrives = false };

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.DoesNotContain("drivestoredirect", content);
    }

    [Fact]
    public void RedirectAudio_Enabled_UsesAudioModeZero()
    {
        var asset = SampleAsset() with { RedirectAudio = true };

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.Contains("audiomode:i:0", content);
    }

    [Fact]
    public void RedirectAudio_Disabled_UsesAudioModeTwo()
    {
        var asset = SampleAsset() with { RedirectAudio = false };

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.Contains("audiomode:i:2", content);
    }

    [Fact]
    public void RedirectClipboard_Enabled_UsesOne()
    {
        var asset = SampleAsset() with { RedirectClipboard = true };

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.Contains("redirectclipboard:i:1", content);
    }

    [Fact]
    public void RedirectClipboard_Disabled_UsesZero()
    {
        var asset = SampleAsset() with { RedirectClipboard = false };

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.Contains("redirectclipboard:i:0", content);
    }

    [Fact]
    public void TempRdpFile_WrittenAsUtf16Le_WithBom()
    {
        var asset = SampleAsset();
        var path = RdpLauncher.CreateTempRdpFile(asset);
        try
        {
            var bytes = File.ReadAllBytes(path);

            Assert.True(bytes.Length >= 2);
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0xFE, bytes[1]);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void TempRdpFile_NamedAfterAssetNameAndHost_WithInvalidCharsStripped()
    {
        var asset = SampleAsset("10.0.0.9") with { Name = "测试:服务器/1" };
        var path = RdpLauncher.CreateTempRdpFile(asset);
        try
        {
            Assert.Equal("测试服务器1 (10.0.0.9).rdp", Path.GetFileName(path));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void TempRdpFile_ChineseHost_PreservedAfterReRead()
    {
        const string host = "中文服务器.example.com";
        var asset = SampleAsset(host) with { Port = 3389 };
        var path = RdpLauncher.CreateTempRdpFile(asset);
        try
        {
            var content = File.ReadAllText(path, Encoding.Unicode);

            Assert.Contains($"full address:s:{host}:3389", content);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void RedirectClipboard_UnsetAndNoTemplate_DefaultsToOne()
    {
        var asset = SampleAsset();

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.Contains("redirectclipboard:i:1", content);
    }

    [Fact]
    public void RedirectClipboard_UnsetButTemplateEnabled_UsesTemplateValue()
    {
        var asset = SampleAsset();
        var template = new ConnectionTemplate { RedirectClipboard = true };

        var content = RdpLauncher.GenerateRdpContent(asset, template);

        Assert.Contains("redirectclipboard:i:1", content);
    }

    [Fact]
    public void RedirectClipboard_AssetDisabledOverridesEnabledTemplate()
    {
        var asset = SampleAsset() with { RedirectClipboard = false };
        var template = new ConnectionTemplate { RedirectClipboard = true };

        var content = RdpLauncher.GenerateRdpContent(asset, template);

        Assert.Contains("redirectclipboard:i:0", content);
    }

    [Fact]
    public void RedirectClipboard_AssetEnabledOverridesDisabledTemplate()
    {
        var asset = SampleAsset() with { RedirectClipboard = true };
        var template = new ConnectionTemplate { RedirectClipboard = false };

        var content = RdpLauncher.GenerateRdpContent(asset, template);

        Assert.Contains("redirectclipboard:i:1", content);
    }

    [Fact]
    public void RedirectDrives_UnsetAndNoTemplate_OmitsKey()
    {
        var asset = SampleAsset();

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.DoesNotContain("drivestoredirect", content);
    }

    [Fact]
    public void RedirectAudio_UnsetAndNoTemplate_DefaultsToEnabled()
    {
        var asset = SampleAsset();

        var content = RdpLauncher.GenerateRdpContent(asset);

        Assert.Contains("audiomode:i:0", content);
    }
}
