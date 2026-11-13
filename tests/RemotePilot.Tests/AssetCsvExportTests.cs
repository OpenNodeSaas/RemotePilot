using System;
using System.Linq;
using RemotePilot.Models;
using RemotePilot.Services;
using Xunit;

namespace RemotePilot.Tests;

public class AssetCsvExportTests
{
    // 默认（密码未勾选）导出表头，顺序固定
    private const string DefaultHeader =
        "分组,服务器名称,服务器IP和端口,账号,到期时间,负责人,其他备注1,其他备注2,其他备注3,其他备注4,协议,端口";

    private static string[] GetLines(string csv) =>
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .ToArray();

    private static string[] GetHeaderColumns(string csv) => GetLines(csv)[0].Split(',');

    private static ServerAsset SampleAsset() => new()
    {
        Name = "srv-a",
        Host = "10.0.0.1",
        Port = 3389,
        Username = "admin",
        Group = "HK",
        Owner = "Alice",
        Protocol = "rdp"
    };

    [Fact]
    public void LegacySingleArgExport_HeaderHasNoPasswordColumn()
    {
        var csv = AssetCsvService.ExportCsv([SampleAsset()]);
        var header = GetLines(csv)[0];

        Assert.Equal(DefaultHeader, header);
        Assert.DoesNotContain("密码", header);
    }

    [Fact]
    public void DefaultFieldSelection_HeaderHasNoPasswordColumn()
    {
        var csv = AssetCsvService.ExportCsv([SampleAsset()], new ExportFieldSelection());
        var header = GetLines(csv)[0];

        Assert.Equal(DefaultHeader, header);
        Assert.DoesNotContain("密码", header);
    }

    [Fact]
    public void PasswordSelected_ColumnAfterUsernameAndBeforeExpires_ValueIsPlaintext()
    {
        var asset = new ServerAsset
        {
            Name = "srv-a",
            Host = "10.0.0.1",
            Port = 3389,
            Username = "admin",
            Password = "secret123"
        };

        var csv = AssetCsvService.ExportCsv([asset], new ExportFieldSelection { Password = true });
        var lines = GetLines(csv);
        var headers = lines[0].Split(',');

        var idxUsername = Array.IndexOf(headers, "账号");
        var idxPassword = Array.IndexOf(headers, "密码");
        var idxExpires = Array.IndexOf(headers, "到期时间");

        Assert.True(idxUsername >= 0);
        Assert.True(idxPassword >= 0);
        Assert.True(idxExpires >= 0);
        Assert.Equal(idxUsername + 1, idxPassword);
        Assert.True(idxPassword < idxExpires);

        Assert.Contains("\"secret123\"", lines[1]);
    }

    [Fact]
    public void PasswordSelected_FromEncryptedPassword_UsesDecryptedPlaintext()
    {
        var cipher = CredentialProtector.Protect("p@ss");
        // Windows DPAPI（当前用户）在本机可用；不可用时 Protect 返回 null，测试将失败以提示环境问题。
        Assert.NotNull(cipher);

        var asset = new ServerAsset
        {
            Name = "srv-a",
            Host = "10.0.0.1",
            Port = 3389,
            Username = "admin",
            Password = null,
            EncryptedPassword = cipher
        };

        var csv = AssetCsvService.ExportCsv([asset], new ExportFieldSelection { Password = true });
        var lines = GetLines(csv);

        Assert.Contains("\"p@ss\"", lines[1]);
    }

    [Fact]
    public void CustomSubset_OnlyNameAndIpPortColumns()
    {
        var asset = SampleAsset();
        var fields = new ExportFieldSelection
        {
            Group = false,
            Name = true,
            IpPort = true,
            Username = false,
            Expires = false,
            Owner = false,
            Note1 = false,
            Note2 = false,
            Note3 = false,
            Note4 = false,
            Protocol = false,
            Port = false
        };

        var csv = AssetCsvService.ExportCsv([asset], fields);
        var lines = GetLines(csv);

        Assert.Equal("服务器名称,服务器IP和端口", lines[0]);
        Assert.Equal(2, lines[1].Split(',').Length);
    }

    [Fact]
    public void CsvEscaping_StillApplied()
    {
        var asset = new ServerAsset
        {
            Name = "He said \"hi\", ok",
            Host = "10.0.0.1",
            Port = 3389
        };

        var csv = AssetCsvService.ExportCsv([asset]);
        var row = GetLines(csv)[1];

        // 逗号与双引号被正确包裹、双引号被转义为两个双引号
        Assert.Contains("\"He said \"\"hi\"\", ok\"", row);
    }

    [Fact]
    public void SshKeyPath_NotSelected_HeaderUnchanged()
    {
        var csv = AssetCsvService.ExportCsv([SampleAsset()], new ExportFieldSelection());
        var header = GetLines(csv)[0];

        Assert.Equal(DefaultHeader, header);
        Assert.DoesNotContain("密钥路径", header);
    }

    [Fact]
    public void SshKeyPath_Selected_ColumnPresentWithValue()
    {
        var asset = SampleAsset() with { SshKeyPath = "/home/u/.ssh/id_rsa" };

        var csv = AssetCsvService.ExportCsv([asset], new ExportFieldSelection { SshKeyPath = true });
        var lines = GetLines(csv);

        Assert.Contains("密钥路径", lines[0]);
        Assert.Contains("\"/home/u/.ssh/id_rsa\"", lines[1]);
    }

    [Fact]
    public void LineCount_EqualsAssetCountPlusHeader()
    {
        var assets = new[]
        {
            new ServerAsset { Name = "a", Host = "10.0.0.1", Port = 3389 },
            new ServerAsset { Name = "b", Host = "10.0.0.2", Port = 3389 },
            new ServerAsset { Name = "c", Host = "10.0.0.3", Port = 3389 }
        };

        var csv = AssetCsvService.ExportCsv(assets);

        Assert.Equal(assets.Length + 1, GetLines(csv).Length);
    }

    [Fact]
    public void Note4_ExportContainsValue_AndRoundTripsThroughImport()
    {
        var asset = SampleAsset() with { CustomNote4 = "备注4内容" };

        var csv = AssetCsvService.ExportCsv([asset]);
        Assert.Contains("其他备注4", GetLines(csv)[0]);
        Assert.Contains("\"备注4内容\"", csv);

        var result = AssetCsvService.ImportCsv(csv);
        var imported = Assert.Single(result.Assets);
        Assert.Equal("备注4内容", imported.CustomNote4);
    }

    [Fact]
    public void Note4_NotSelected_HeaderExcludesColumn()
    {
        var csv = AssetCsvService.ExportCsv([SampleAsset()], new ExportFieldSelection { Note4 = false });
        var header = GetLines(csv)[0];

        Assert.DoesNotContain("其他备注4", header);
        Assert.Contains("其他备注3", header);
    }

    [Theory]
    [InlineData("Note4")]
    [InlineData("CustomNote4")]
    public void Note4_HeaderAlias_Imported(string header)
    {
        var csv = "服务器名称,IP和端口," + header + "\nsrv-a,10.0.0.1:3389,别名备注";

        var result = AssetCsvService.ImportCsv(csv);
        var imported = Assert.Single(result.Assets);

        Assert.Equal("别名备注", imported.CustomNote4);
    }
}
