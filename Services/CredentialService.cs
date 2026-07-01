using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CxDesktopWrapper.Services;

public static class CredentialService
{
    private static readonly string KeyFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CxDesktopWrapper", "secure_key.dat");

    private static string? _cachedApiKey;

    public static void SaveEncryptedApiKey(string apiKey)
    {
        try
        {
            var directory = Path.GetDirectoryName(KeyFile);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
            byte[] plainBytes = Encoding.UTF8.GetBytes(apiKey);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFile, encryptedBytes);
            _cachedApiKey = apiKey;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao salvar a API Key: " + ex.Message);
            throw;
        }
    }

    public static string LoadDecryptedApiKey(bool useCache = true)
    {
        if (useCache && _cachedApiKey != null)
        {
            return _cachedApiKey;
        }

        if (!File.Exists(KeyFile)) return string.Empty;

        try
        {
            byte[] encryptedBytes = File.ReadAllBytes(KeyFile);
            byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            _cachedApiKey = Encoding.UTF8.GetString(plainBytes);
            return _cachedApiKey;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao descriptografar a API Key: " + ex.Message);
            return string.Empty;
        }
    }

    public static void ClearCache()
    {
        _cachedApiKey = null;
    }
}
