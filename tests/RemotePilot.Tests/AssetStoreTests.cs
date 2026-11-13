using System.IO;
using RemotePilot.Models;
using RemotePilot.Services;
using Xunit;

namespace RemotePilot.Tests;

public class AssetStoreTests
{
    [Fact]
    public void TestModelSerializationAndNoPasswordLeak()
    {
        var asset = new ServerAsset
        {
            Name = "Test Server",
            Host = "127.0.0.1",
            Port = 3389,
            Protocol = "rdp",
            Username = "administrator",
            Group = "HK",
            Owner = "Alice",
            ExpiresOn = new DateOnly(2026, 12, 31),
            Notes = "Important notes",
            CustomNote1 = "Value1",
            CustomNote2 = "Value2",
            CustomNote3 = "Value3",
            RedirectDrives = true,
            RedirectAudio = false,
            RedirectClipboard = false,
            RdpWidth = 1920,
            RdpHeight = 1080,
            RdpColorDepth = 24,
            RemoteAssetId = "sync-12345",
            Password = "SuperSecretPassword123!",
            PrivateKey = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQ..."
        };

        var tmpl = new ConnectionTemplate
        {
            Name = "Default RDP",
            Protocol = "rdp",
            Port = 3389,
            RdpWidth = 1920,
            RdpHeight = 1080,
            RdpColorDepth = 32,
            RedirectDrives = false,
            RedirectAudio = true,
            RedirectClipboard = true,
            RemoteTemplateId = "tmpl-sync-999",
            Password = "TmplSecretPassword",
            PrivateKey = "TmplPrivateKeyContent"
        };

        var data = new AssetStoreData
        {
            Version = 1,
            Assets = [asset],
            Templates = [tmpl],
            ExpiryLeadDays = 15
        };

        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);
            store.Save(data);

            var loaded = store.Load();
            Assert.Equal(1, loaded.Version);
            Assert.Single(loaded.Assets);
            Assert.Single(loaded.Templates);
            Assert.Equal(15, loaded.ExpiryLeadDays);

            var loadedAsset = loaded.Assets[0];
            Assert.Equal("Test Server", loadedAsset.Name);
            Assert.Equal("127.0.0.1", loadedAsset.Host);
            Assert.Equal(3389, loadedAsset.Port);
            Assert.Equal("Value1", loadedAsset.CustomNote1);
            Assert.Equal("Value2", loadedAsset.CustomNote2);
            Assert.Equal("Value3", loadedAsset.CustomNote3);
            Assert.True(loadedAsset.RedirectDrives);
            Assert.False(loadedAsset.RedirectAudio);
            Assert.False(loadedAsset.RedirectClipboard);
            Assert.Equal(1920, loadedAsset.RdpWidth);
            Assert.Equal(1080, loadedAsset.RdpHeight);
            Assert.Equal(24, loadedAsset.RdpColorDepth);
            Assert.Equal("sync-12345", loadedAsset.RemoteAssetId);

            // Ensure Password and PrivateKey are null when deserialized
            Assert.Null(loadedAsset.Password);
            Assert.Null(loadedAsset.PrivateKey);

            var loadedTmpl = loaded.Templates[0];
            Assert.Null(loadedTmpl.Password);
            Assert.Null(loadedTmpl.PrivateKey);
            Assert.Equal("tmpl-sync-999", loadedTmpl.RemoteTemplateId);

            // Verify raw JSON string does not contain password or private key plaintext
            var jsonContent = File.ReadAllText(tempFile);
            Assert.DoesNotContain("SuperSecretPassword123!", jsonContent);
            Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", jsonContent);
            Assert.DoesNotContain("TmplSecretPassword", jsonContent);
            Assert.DoesNotContain("TmplPrivateKeyContent", jsonContent);
            Assert.DoesNotContain("\"Password\"", jsonContent);
            Assert.DoesNotContain("\"PrivateKey\"", jsonContent);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestAssetStoreValidationFiltering()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);
            
            // Invalid data with missing host, invalid port, duplicate IDs
            var validId = Guid.NewGuid();
            var invalidAsset1 = new ServerAsset { Id = validId, Name = "Valid", Host = "localhost", Port = 22 };
            var invalidAsset2 = new ServerAsset { Id = validId, Name = "Duplicate ID", Host = "localhost", Port = 22 };
            var invalidAsset3 = new ServerAsset { Id = Guid.NewGuid(), Name = "", Host = "localhost", Port = 22 }; // Empty name
            var invalidAsset4 = new ServerAsset { Id = Guid.NewGuid(), Name = "No Host", Host = "", Port = 22 }; // Empty host
            var invalidAsset5 = new ServerAsset { Id = Guid.NewGuid(), Name = "Bad Port", Host = "localhost", Port = 99999 }; // Bad port

            var data = new AssetStoreData
            {
                Assets = [invalidAsset1, invalidAsset2, invalidAsset3, invalidAsset4, invalidAsset5]
            };

            store.Save(data);

            var loaded = store.Load();
            Assert.Single(loaded.Assets);
            Assert.Equal("Valid", loaded.Assets[0].Name);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestIsLeftNavCollapsedRoundTrip()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);

            store.Save(new AssetStoreData { IsLeftNavCollapsed = true });
            Assert.True(store.Load().IsLeftNavCollapsed);

            store.Save(new AssetStoreData { IsLeftNavCollapsed = false });
            Assert.False(store.Load().IsLeftNavCollapsed);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestEncryptedPasswordPersistsButPlainPasswordDoesNot()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);
            var asset = new ServerAsset
            {
                Name = "srv-a",
                Host = "127.0.0.1",
                Port = 3389,
                EncryptedPassword = "cipher-blob",
                Password = "plain"
            };

            store.Save(new AssetStoreData { Assets = [asset] });

            var loaded = store.Load();
            var loadedAsset = Assert.Single(loaded.Assets);
            Assert.Equal("cipher-blob", loadedAsset.EncryptedPassword);
            Assert.Null(loadedAsset.Password);

            // 明文密码绝不落盘
            var json = File.ReadAllText(tempFile);
            Assert.DoesNotContain("plain", json);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestCorruptedJsonHandling()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{ this is not valid json --- !!! }");
            var store = new AssetStore(tempFile);
            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Empty(loaded.Assets);
            Assert.Empty(loaded.Templates);
            Assert.Equal(1, loaded.Version);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestAssetListPreferencesRoundTrip()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);
            var data = new AssetStoreData
            {
                AssetListHiddenColumns = ["Note1", "Owner"],
                AssetListColumnOrder = ["Name", "Action", "Favorite"],
                AssetListFontSize = 16
            };

            store.Save(data);

            var loaded = store.Load();
            Assert.Equal(new List<string> { "Note1", "Owner" }, loaded.AssetListHiddenColumns);
            Assert.Equal(new List<string> { "Name", "Action", "Favorite" }, loaded.AssetListColumnOrder);
            Assert.Equal(16, loaded.AssetListFontSize);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestAssetListColumnsWhitelistFiltering()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);
            var data = new AssetStoreData
            {
                AssetListHiddenColumns = ["Note1", "NotAColumn", "", "Note1", "Owner"],
                AssetListColumnOrder = ["Name", "Bogus", "Name", "Action"]
            };

            store.Save(data);

            var loaded = store.Load();
            Assert.Equal(new List<string> { "Note1", "Owner" }, loaded.AssetListHiddenColumns);
            Assert.Equal(new List<string> { "Name", "Action" }, loaded.AssetListColumnOrder);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestAssetListPreferencesForwardCompatibleWithLegacyJson()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{\"Version\":1,\"Assets\":[],\"Templates\":[],\"ExpiryLeadDays\":30}");
            var store = new AssetStore(tempFile);

            var loaded = store.Load();

            Assert.Empty(loaded.AssetListHiddenColumns);
            Assert.Empty(loaded.AssetListColumnOrder);
            Assert.Equal(14, loaded.AssetListFontSize);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData(8.0, 14.0)]
    [InlineData(30.0, 14.0)]
    [InlineData(12.0, 12.0)]
    public void TestAssetListFontSizeOutOfRangeFallsBackToDefault(double saved, double expected)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);

            store.Save(new AssetStoreData { AssetListFontSize = saved });

            Assert.Equal(expected, store.Load().AssetListFontSize);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestLoadMissingFileReturnsDefaultPreferences()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "assets.json");
        var store = new AssetStore(tempFile);

        var loaded = store.Load();

        Assert.Empty(loaded.AssetListHiddenColumns);
        Assert.Empty(loaded.AssetListColumnOrder);
        Assert.Equal(14, loaded.AssetListFontSize);
    }

    [Fact]
    public void TestSshPreferencesRoundTrip()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);
            store.Save(new AssetStoreData
            {
                SshStandaloneWindow = true,
                SshTerminalFontSize = 16,
                SshTerminalScheme = 2
            });

            var loaded = store.Load();
            Assert.True(loaded.SshStandaloneWindow);
            Assert.Equal(16, loaded.SshTerminalFontSize);
            Assert.Equal(2, loaded.SshTerminalScheme);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestSshPreferencesOutOfRangeFallsBackToDefaults()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);
            store.Save(new AssetStoreData
            {
                SshTerminalFontSize = 100,
                SshTerminalScheme = 99
            });

            var loaded = store.Load();
            Assert.Equal(13, loaded.SshTerminalFontSize);
            Assert.Equal(0, loaded.SshTerminalScheme);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TestSshPreferencesForwardCompatibleWithLegacyJson()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{\"Version\":1,\"Assets\":[],\"Templates\":[],\"ExpiryLeadDays\":30}");
            var store = new AssetStore(tempFile);

            var loaded = store.Load();

            Assert.False(loaded.SshStandaloneWindow);
            Assert.Equal(13, loaded.SshTerminalFontSize);
            Assert.Equal(0, loaded.SshTerminalScheme);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData(9.0, 9.0)]
    [InlineData(28.0, 28.0)]
    [InlineData(8.0, 13.0)]
    [InlineData(29.0, 13.0)]
    public void TestSshTerminalFontSizeBoundaryFallsBackToDefault(double saved, double expected)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);

            store.Save(new AssetStoreData { SshTerminalFontSize = saved });

            Assert.Equal(expected, store.Load().SshTerminalFontSize);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    public void TestSshTerminalSchemeOutOfRangeFallsBackToDefault(int saved, int expected)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new AssetStore(tempFile);

            store.Save(new AssetStoreData { SshTerminalScheme = saved });

            Assert.Equal(expected, store.Load().SshTerminalScheme);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
