using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CxDesktopWrapper.Services;

public static class CliInstallerService
{
    public static async Task<string> DownloadAndInstallCliAsync(
        HttpClient httpClient,
        string downloadUrl,
        string destFolder,
        Action<string> logAction,
        CancellationToken cancellationToken)
    {
        string zipPath = Path.Combine(Path.GetTempPath(), "ast-cli.zip");

        logAction("Baixando Checkmarx CLI mais recente...");
        
        using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                await response.Content.CopyToAsync(fs, cancellationToken);
            }
        }

        logAction("Extraindo arquivos de forma segura...");
        if (!Directory.Exists(destFolder))
        {
            Directory.CreateDirectory(destFolder);
        }

        string targetDirAbsolute = Path.GetFullPath(destFolder)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (!targetDirAbsolute.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            targetDirAbsolute += Path.DirectorySeparatorChar;
        }

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in archive.Entries)
            {
                string entryDestinationPath = Path.GetFullPath(Path.Combine(destFolder, entry.FullName))
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                if (!entryDestinationPath.StartsWith(targetDirAbsolute, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Tentativa de extração insegura (Zip Slip): {entry.FullName}");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(entryDestinationPath);
                }
                else
                {
                    var parentDir = Path.GetDirectoryName(entryDestinationPath);
                    if (parentDir != null)
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    entry.ExtractToFile(entryDestinationPath, overwrite: true);
                }
            }
        }

        try
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao deletar arquivo temporário: " + ex.Message);
        }

        string executablePath = Path.Combine(destFolder, "cx.exe");
        logAction($"CLI instalado com sucesso em: {executablePath}");
        return executablePath;
    }
}
