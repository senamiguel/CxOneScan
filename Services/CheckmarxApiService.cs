using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CxDesktopWrapper.Services;

public class CheckmarxApiService
{
    private readonly HttpClient _httpClient;

    public CheckmarxApiService()
    {
        _httpClient = new HttpClient();
    }

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
        catch (Exception ex)
        {
            return new ProjectValidationResult
            {
                ApiCallFailed = true,
                ApiErrorMessage = $"Não foi possível validar o projeto via API: {ex.Message}. " +
                                   "A operação foi abortada para evitar criação acidental de projetos."
            };
        }
    }

    private async Task<string> GetAccessTokenAsync(string tenant, string apiKey, string baseAuthUri)
    {
        var tokenUrl = $"{baseAuthUri.TrimEnd('/')}/auth/realms/{tenant}/protocol/openid-connect/token";

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        tokenRequest.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", "ast-app"),
            new KeyValuePair<string, string>("client_secret", apiKey)
        });

        using var tokenResponse = await _httpClient.SendAsync(tokenRequest);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(tokenJson);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<ProjectValidationResult> FindProjectByNameAsync(
        string projectName, string token, string baseUri)
    {
        var projectsUrl = $"{baseUri.TrimEnd('/')}/api/projects-overview?name={Uri.EscapeDataString(projectName)}&limit=10";

        using var request = new HttpRequestMessage(HttpMethod.Get, projectsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var projects = doc.RootElement.GetProperty("projects").EnumerateArray().ToList();

        var exactMatches = projects.Where(p =>
            p.GetProperty("projectName").GetString() == projectName).ToList();

        if (exactMatches.Count > 0)
        {
            return new ProjectValidationResult
            {
                ProjectFound = true,
                ProjectId = exactMatches[0].GetProperty("projectId").GetString()
            };
        }

        var similarProjects = projects
            .Select(p => p.GetProperty("projectName").GetString()!)
            .Where(name => name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (similarProjects.Count == 0)
            similarProjects = await SearchSimilarProjectsAsync(projectName, token, baseUri);

        return new ProjectValidationResult
        {
            ProjectFound = false,
            SimilarProjects = similarProjects,
            Message = FormatErrorMessage(projectName, similarProjects)
        };
    }

    private async Task<List<string>> SearchSimilarProjectsAsync(string projectName, string token, string baseUri)
    {
        var searchUrl = $"{baseUri.TrimEnd('/')}/api/projects-overview?search={Uri.EscapeDataString(projectName)}&limit=10";

        using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new List<string>();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.GetProperty("projects")
            .EnumerateArray()
            .Select(p => p.GetProperty("projectName").GetString()!)
            .Where(n => !n.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(5)
            .ToList();
    }

    private static string FormatErrorMessage(string projectName, List<string> similarProjects)
    {
        var msg = $"PROJETO \"{projectName}\" NÃO ENCONTRADO no Checkmarx One. " +
                  "O nome é case-sensitive. Nenhum novo projeto será criado.";

        if (similarProjects.Count > 0)
        {
            msg += "\n\nProjetos similares encontrados:";
            foreach (var name in similarProjects)
                msg += $"\n  • {name}";
            msg += "\n\nUse um dos nomes acima (exatamente como escrito).";
        }

        return msg;
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
