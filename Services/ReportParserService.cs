using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper.Services;

public static class ReportParserService
{
    public static ScanResult ParseReport(string reportDirectory, string projectName)
    {
        var result = new ScanResult
        {
            ProjectName = projectName,
            ScanDate = DateTime.Now,
            Success = false,
            StatusMessage = "Relatório não encontrado"
        };

        if (!Directory.Exists(reportDirectory))
            return result;

        // Lê o diretório uma única vez e filtra por extensão — evita duas chamadas ao SO
        var allFiles = GetDirectoryFiles(reportDirectory);
        string? jsonPath = allFiles.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                    .FirstOrDefault();
        string? htmlPath = allFiles.Where(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                                    .FirstOrDefault();

        if (jsonPath != null)
            result.ReportJsonPath = jsonPath;
        if (htmlPath != null)
            result.ReportHtmlPath = htmlPath;

        if (jsonPath == null)
        {
            result.StatusMessage = "Arquivo JSON de relatório não encontrado em: " + reportDirectory;
            return result;
        }

        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            CountVulnerabilities(jsonContent, result);
            result.Success = true;
            result.StatusMessage = "Relatório processado com sucesso";
        }
        catch (Exception ex)
        {
            result.StatusMessage = $"Erro ao processar relatório: {ex.Message}";
        }

        return result;
    }

    private static void CountVulnerabilities(string jsonContent, ScanResult result)
    {
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        if (TryParseFromResults(root, result))
            return;

        if (TryParseFromCounters(root, result))
            return;

        if (root.ValueKind == JsonValueKind.Array)
            CountFromArray(root, result);
    }

    private static bool TryParseFromResults(JsonElement root, ScanResult result)
    {
        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return false;

        CountFromArray(results, result);
        return true;
    }

    private static void CountFromArray(JsonElement array, ScanResult result)
    {
        foreach (var item in array.EnumerateArray())
        {
            string severity = GetSeverityFromElement(item);
            IncrementSeverityCount(result, severity);
        }
    }

    private static bool TryParseFromCounters(JsonElement root, ScanResult result)
    {
        if (!root.TryGetProperty("totalCounters", out var counters))
            return false;

        if (!counters.TryGetProperty("severityCounters", out var severityCounters)
            || severityCounters.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var counter in severityCounters.EnumerateArray())
        {
            string severity = counter.TryGetProperty("severity", out var sev)
                ? sev.GetString()?.ToUpperInvariant() ?? ""
                : "";
            int count = counter.TryGetProperty("counter", out var cnt)
                ? cnt.GetInt32()
                : 0;

            switch (severity)
            {
                case "HIGH":   result.HighCount += count; break;
                case "MEDIUM": result.MediumCount += count; break;
                case "LOW":    result.LowCount += count; break;
                case "INFO":
                case "INFORMATIONAL": result.InfoCount += count; break;
            }
        }

        return true;
    }

    private static string GetSeverityFromElement(JsonElement element)
    {
        if (element.TryGetProperty("severity", out var severity))
            return severity.GetString()?.ToUpperInvariant() ?? "";

        if (element.TryGetProperty("Severity", out severity))
            return severity.GetString()?.ToUpperInvariant() ?? "";

        return "";
    }

    private static void IncrementSeverityCount(ScanResult result, string severity)
    {
        switch (severity)
        {
            case "HIGH":   result.HighCount++; break;
            case "MEDIUM": result.MediumCount++; break;
            case "LOW":    result.LowCount++; break;
            case "INFO":
            case "INFORMATIONAL": result.InfoCount++; break;
        }
    }

    /// <summary>
    /// Lê o diretório uma única vez e retorna todos os arquivos ordenados do mais recente ao mais antigo.
    /// Evita múltiplas chamadas ao sistema de arquivos para padrões diferentes.
    /// </summary>
    private static IEnumerable<string> GetDirectoryFiles(string directory)
    {
        try
        {
            return new DirectoryInfo(directory)
                .GetFiles("*")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => f.FullName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao enumerar diretório de relatórios: " + ex.Message);
            return Enumerable.Empty<string>();
        }
    }
}
