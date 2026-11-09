using System.Security.Cryptography;
using System.Text;

namespace RemotePilot.Services;

/// <summary>本机密码加密：Windows DPAPI（当前用户）。换机或换用户后无法解密。</summary>
public static class CredentialProtector
{
    public static string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return null;
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }

    public static string? Unprotect(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(cipherText), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }
}
