using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AiRadar.App.Services;

/// <summary>
/// Keeps the Product Hunt developer token encrypted with Windows DPAPI, readable only by the
/// current Windows user on this machine.
/// </summary>
public sealed class TokenStore(string filePath)
{
    private static readonly byte[] Entropy = "AiRadar.ProductHunt.v1"u8.ToArray();

    public bool HasToken => File.Exists(filePath);

    public string? Read()
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(filePath), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            // Encrypted by another Windows user or machine; treat as no token so the user can enter it again.
            return null;
        }
    }

    public void Save(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Trim()), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(filePath, encrypted);
    }
}
