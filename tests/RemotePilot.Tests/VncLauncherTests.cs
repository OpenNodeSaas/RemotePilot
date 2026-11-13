using System.IO;
using RemotePilot.Models;
using RemotePilot.Services;
using Xunit;

namespace RemotePilot.Tests;

public class VncLauncherTests
{
    private static ServerAsset SampleAsset(string host = "10.0.0.1", int port = 5900) => new()
    {
        Name = "srv-vnc",
        Host = host,
        Port = port,
        Username = "admin",
        Protocol = "vnc"
    };

    [Fact]
    public void RenderArguments_SubstitutesHostAndPort()
    {
        var args = VncLauncher.RenderArguments("{host}::{port}", SampleAsset("10.0.0.9"), null);

        Assert.Equal(new[] { "10.0.0.9::5900" }, args);
    }

    [Fact]
    public void RenderArguments_FallsBackToDefaultVncPort_WhenPortInvalid()
    {
        var args = VncLauncher.RenderArguments("{host}::{port}", SampleAsset(port: 0), null);

        Assert.Equal(new[] { "10.0.0.1::5900" }, args);
    }

    [Fact]
    public void RenderArguments_SubstitutesUsername()
    {
        var args = VncLauncher.RenderArguments("{username}@{host}", SampleAsset(), null);

        Assert.Equal(new[] { "admin@10.0.0.1" }, args);
    }

    [Fact]
    public void RenderArguments_OmitsPasswordToken_WhenPasswordMissing()
    {
        var args = VncLauncher.RenderArguments("{host}::{port} -password={password}", SampleAsset(), null);

        Assert.Equal(new[] { "10.0.0.1::5900" }, args);
    }

    [Fact]
    public void RenderArguments_KeepsPasswordToken_WhenPasswordProvided()
    {
        var args = VncLauncher.RenderArguments("{host}::{port} -password={password}", SampleAsset(), "s3cret");

        Assert.Equal(new[] { "10.0.0.1::5900", "-password=s3cret" }, args);
    }

    [Fact]
    public void RenderArguments_DropsSeparatePasswordSwitchAndValue_WhenPasswordMissing()
    {
        var args = VncLauncher.RenderArguments("-connect {host}::{port} -password {password}", SampleAsset(), null);

        Assert.Equal(new[] { "-connect", "10.0.0.1::5900" }, args);
    }

    [Fact]
    public void RenderArguments_KeepsSeparatePasswordSwitchAndValue_WhenPasswordProvided()
    {
        var args = VncLauncher.RenderArguments("-connect {host}::{port} -password {password}", SampleAsset(), "s3cret");

        Assert.Equal(new[] { "-connect", "10.0.0.1::5900", "-password", "s3cret" }, args);
    }

    [Fact]
    public void RenderArguments_UsesAddressOnlyDefault_WhenTemplateBlank()
    {
        var args = VncLauncher.RenderArguments("   ", SampleAsset(), null);

        Assert.Equal(new[] { "10.0.0.1::5900" }, args);
    }

    [Theory]
    [InlineData(VncViewerFamily.TightVnc, "{host}::{port} -password={password}")]
    [InlineData(VncViewerFamily.UltraVnc, "-connect {host}::{port} -password {password}")]
    [InlineData(VncViewerFamily.RealVnc, "{host}::{port}")]
    [InlineData(VncViewerFamily.TigerVnc, "{host}::{port}")]
    [InlineData(VncViewerFamily.Unknown, "{host}::{port}")]
    public void DefaultArgumentTemplate_ReturnsExpectedTemplate(VncViewerFamily family, string expected)
    {
        Assert.Equal(expected, VncLauncher.DefaultArgumentTemplate(family));
    }

    [Theory]
    [InlineData(@"C:\Program Files\TightVNC\tvnviewer.exe", VncViewerFamily.TightVnc)]
    [InlineData(@"C:\Program Files\UltraVNC\vncviewer.exe", VncViewerFamily.UltraVnc)]
    [InlineData(@"C:\Program Files\RealVNC\VNC Viewer\vncviewer.exe", VncViewerFamily.RealVnc)]
    [InlineData(@"C:\Program Files\TigerVNC\vncviewer.exe", VncViewerFamily.TigerVnc)]
    [InlineData(@"C:\tools\something.exe", VncViewerFamily.Unknown)]
    public void IdentifyFamily_DetectsKnownClients(string path, VncViewerFamily expected)
    {
        Assert.Equal(expected, VncLauncher.IdentifyFamily(path));
    }

    [Fact]
    public void ResolveViewer_UsesConfiguredPath_WhenFileExists()
    {
        var parent = Path.Combine(Path.GetTempPath(), "RemotePilotTests", Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(parent, "UltraVNC");
        Directory.CreateDirectory(dir);
        var exePath = Path.Combine(dir, "vncviewer.exe");
        File.WriteAllText(exePath, "");

        try
        {
            var viewer = VncLauncher.ResolveViewer(exePath);

            Assert.NotNull(viewer);
            Assert.Equal(exePath, viewer!.ExecutablePath);
            Assert.Equal(VncViewerFamily.UltraVnc, viewer.Family);
            Assert.Equal("UltraVNC Viewer", viewer.DisplayName);
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void ResolveViewer_IgnoresConfiguredPath_WhenFileMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "RemotePilotTests", Guid.NewGuid().ToString("N"), "nope.exe");

        // 路径不存在时回退到自动探测：本机是否装有 VNC 客户端不确定，只要求不返回这个无效路径
        var viewer = VncLauncher.ResolveViewer(missing);

        Assert.NotEqual(missing, viewer?.ExecutablePath);
    }
}
