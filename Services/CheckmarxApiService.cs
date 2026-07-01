using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace CxDesktopWrapper.Services;

public class CheckmarxApiService
{
    private static readonly HttpClient _httpClient = new();
    public static HttpClient SharedHttpClient => _httpClient;

    public async Task<ProjectValidationResult> ValidateProjectExistsAsync(
        string projectName,
        string tenant,
        string apiKey,
        string baseUri,
        string baseAuthUri)
    {
        try
        {
            string token = await GetAccessTokenAsync(tenant, apiKey, baseAuthUri);
            return await FindProjectByNameAsync(projectName, token, baseUri);
        }
        catch (HttpRequestException httpEx)
        {
            string detailedError = "Erro de autenticação/conexão com a API do Checkmarx One:\n\n";
            if (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                detailedError += "• Acesso não autorizado (401). A sua API Key ou o Tenant Name podem estar incorretos ou expirados.";
            }
            else if (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                detailedError += "• Servidor/Caminho não encontrado (404). Verifique se as URLs configuradas (Base URI / Base Auth URI) estão corretas.";
            }
            else if (httpEx.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                detailedError += "• Acesso proibido (403). Suas credenciais não possuem privilégios suficientes para validar o projeto.";
            }
            else
            {
                detailedError += $"• Código de status HTTP: {httpEx.StatusCode}\n• Detalhes: {httpEx.Message}";
            }

            detailedError += "\n\nA operação foi abortada para evitar criação acidental de projetos no portal.";

            return new ProjectValidationResult
            {
                ApiCallFailed = true,
                ApiErrorMessage = detailedError
            };
        }
        catch (Exception ex)
        {
            return new ProjectValidationResult
            {
                ApiCallFailed = true,
                ApiErrorMessage = $"Erro inesperado ao validar o projeto via API:\n• {ex.Message}\n\nA operação foi abortada para evitar criação acidental de projetos."
            };
        }
    }

    public async Task<bool> TestConnectionAsync(string tenant, string apiKey, string baseAuthUri)
    {
        try
        {
            string token = await GetAccessTokenAsync(tenant, apiKey, baseAuthUri);
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao testar conexão com a API Checkmarx One: " + ex.Message);
            return false;
        }
    }

    private async Task<string> GetAccessTokenAsync(string tenant, string apiKey, string baseAuthUri)
    {
        string tokenUrl = CombineUrl(baseAuthUri, $"/auth/realms/{tenant}/protocol/openid-connect/token");

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        tokenRequest.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("client_id", "ast-app"),
            new KeyValuePair<string, string>("refresh_token", apiKey)
        });

        using var tokenResponse = await _httpClient.SendAsync(tokenRequest);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(tokenJson);
        if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
        {
            return tokenProp.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private async Task<ProjectValidationResult> FindProjectByNameAsync(
        string projectName, string token, string baseUri)
    {
        string query = $"name={Uri.EscapeDataString(projectName)}&limit=10";
        string projectsUrl = CombineUrl(baseUri, "/api/projects-overview", query);

        using var request = new HttpRequestMessage(HttpMethod.Get, projectsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("projects", out var projectsElement) || projectsElement.ValueKind != JsonValueKind.Array)
        {
            return new ProjectValidationResult
            {
                ProjectFound = false,
                Message = $"Estrutura de resposta da API inválida. Não foi possível encontrar a propriedade 'projects'."
            };
        }

        // 1. Procurar correspondência exata sem alocações extras
        JsonElement? exactMatch = null;
        foreach (var p in projectsElement.EnumerateArray())
        {
            if (p.TryGetProperty("projectName", out var nameProp) && nameProp.GetString() == projectName)
            {
                exactMatch = p;
                break;
            }
        }

        if (exactMatch != null)
        {
            string? id = null;
            if (exactMatch.Value.TryGetProperty("projectId", out var idProp))
            {
                id = idProp.GetString();
            }

            return new ProjectValidationResult
            {
                ProjectFound = true,
                ProjectId = id
            };
        }

        // 2. Coletar projetos similares
        var similarProjects = new List<string>();
        foreach (var p in projectsElement.EnumerateArray())
        {
            if (p.TryGetProperty("projectName", out var nameProp))
            {
                string? name = nameProp.GetString();
                if (name != null && name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                {
                    similarProjects.Add(name);
                }
            }
        }

        if (similarProjects.Count == 0)
        {
            similarProjects = await SearchSimilarProjectsAsync(projectName, token, baseUri);
        }

        return new ProjectValidationResult
        {
            ProjectFound = false,
            SimilarProjects = similarProjects,
            Message = FormatErrorMessage(projectName, similarProjects)
        };
    }

    private async Task<List<string>> SearchSimilarProjectsAsync(string projectName, string token, string baseUri)
    {
        string query = $"search={Uri.EscapeDataString(projectName)}&limit=10";
        string searchUrl = CombineUrl(baseUri, "/api/projects-overview", query);

        using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new List<string>();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("projects", out var projectsElement) || projectsElement.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        var list = new List<string>();
        foreach (var p in projectsElement.EnumerateArray())
        {
            if (p.TryGetProperty("projectName", out var nameProp))
            {
                string? name = nameProp.GetString();
                if (name != null && !name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(name);
                }
            }
        }

        return list.Distinct().Take(5).ToList();
    }

    private static string FormatErrorMessage(string projectName, List<string> similarProjects)
    {
        var msg = $"PROJETO \"{projectName}\" NÃO ENCONTRADO no Checkmarx One. " +
                  "O nome é case-sensitive. Nenhum novo projeto será criado.";

        if (similarProjects.Count > 0)
        {
            msg += "\n\nProjetos similares encontrados:";
            foreach (var name in similarProjects)
            {
                msg += $"\n• {name}";
            }
        }
        return msg;
    }

    public async Task<bool> CheckHasCompletedScanAsync(
        string projectId,
        string branch,
        string tenant,
        string apiKey,
        string baseUri,
        string baseAuthUri)
    {
        try
        {
            string token = await GetAccessTokenAsync(tenant, apiKey, baseAuthUri);
            string query = $"project-id={Uri.EscapeDataString(projectId)}&branch={Uri.EscapeDataString(branch)}&limit=20";
            string scansUrl = CombineUrl(baseUri, "/api/scans", query);

            using var request = new HttpRequestMessage(HttpMethod.Get, scansUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("scans", out var scansElement) && scansElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var scan in scansElement.EnumerateArray())
                {
                    if (scan.TryGetProperty("status", out var statusElement) && statusElement.GetString() == "Completed")
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao verificar scans completados: " + ex.Message);
            return false;
        }
    }

    private static string CombineUrl(string baseUri, string path, string? query = null)
    {
        var cleanBase = baseUri.TrimEnd('/');
        var cleanPath = path.TrimStart('/');
        var url = $"{cleanBase}/{cleanPath}";
        if (!string.IsNullOrEmpty(query))
        {
            url += $"?{query}";
        }
        return url;
    }
}

public class ProjectValidationResult
{
    public bool ProjectFound { get; set; }
    public bool ApiCallFailed { get; set; }
    public string? ProjectId { get; set; }
    public List<string> SimilarProjects { get; set; } = new();
    public string? Message { get; set; }
    public string? ApiErrorMessage { get; set; }
}
