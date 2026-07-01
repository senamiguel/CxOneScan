using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CxDesktopWrapper.Models;
using CxDesktopWrapper.Services;
using System.Text.Json;

namespace CxDesktopWrapper;

public class StringToVisibilityConverter : IValueConverter
{
  public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
    return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
  }

  public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
  {
    throw new NotSupportedException();
  }
}

public partial class MainWindow : Window
{
  public ObservableCollection<ProjectItem> SavedProjects { get; } = new();
  public ObservableCollection<ScanResult> ScanResults { get; } = new();

  private readonly CxCliService _cliService = new();
  private CancellationTokenSource? _scanCts;
  private ProjectItem? _selectedProject;
  private bool _isUpdatingDetail;

  public MainWindow()
  {
    InitializeComponent();
    dgProjects.ItemsSource = SavedProjects;
    dgResults.ItemsSource = ScanResults;

    LoadProjects();
    LoadSettingsUI();
    WireCliServiceEvents();
  }

  private void WireCliServiceEvents()
  {
    _cliService.OutputReceived += text => AppendToConsole(text);
    _cliService.ProgressChanged += percent => UpdateProgress(percent);
  }

  private void LoadProjects()
  {
    var loaded = ProjectPersistenceService.Load();
    foreach (var p in loaded)
      SavedProjects.Add(p);
  }

  private void SaveProjects()
  {
    ProjectPersistenceService.Save(SavedProjects);
  }

  private void LoadSettingsUI()
  {
    var settings = AppSettingsService.Instance;

    txtSettingCliPath.Text = settings.CliPath;
    txtSettingTenant.Text = settings.Tenant;
    txtSettingBaseUri.Text = settings.BaseUri;
    txtSettingBaseAuthUri.Text = settings.BaseAuthUri;
    txtSettingDefaultBranch.Text = settings.DefaultBranch;
    chkSettingDefaultSast.IsChecked = settings.DefaultRunSast;
    chkSettingDefaultSca.IsChecked = settings.DefaultRunSca;
    chkSettingDefaultIncremental.IsChecked = settings.DefaultIncremental;
    chkSettingBypassValidation.IsChecked = settings.BypassApiValidation;

    cmbSettingTheme.SelectedIndex = settings.Theme == "Light" ? 1 : 0;
    ApplyTheme(settings.Theme);

    txtSettingApiKey.Password = LoadDecryptedApiKey();

    UpdateFooterStatus();
  }

  private void UpdateFooterStatus()
  {
    var settings = AppSettingsService.Instance;
    if (string.IsNullOrEmpty(settings.Tenant))
    {
      lblFooterStatus.Text = "⚠️ Não Configurado";
      lblFooterStatus.Foreground = Brushes.Orange;
    }
    else
    {
      lblFooterStatus.Text = $"🔑 Tenant: {settings.Tenant}";
      lblFooterStatus.Foreground = Brushes.LightGreen;
    }
  }

  private void ApplyTheme(string theme)
  {
    if (theme == "Light")
    {
      iNKORE.UI.WPF.Modern.ThemeManager.Current.ApplicationTheme = iNKORE.UI.WPF.Modern.ApplicationTheme.Light;
    }
    else
    {
      iNKORE.UI.WPF.Modern.ThemeManager.Current.ApplicationTheme = iNKORE.UI.WPF.Modern.ApplicationTheme.Dark;
    }
  }

  private async void BtnAddProject_Click(object sender, RoutedEventArgs e)
  {
    string newName = txtNewProject.Text.Trim();
    if (string.IsNullOrEmpty(newName)) return;
    if (SavedProjects.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))) return;

    if (!await ValidateProjectExistsAsync(newName)) return;

    var settings = AppSettingsService.Instance;
    SavedProjects.Add(new ProjectItem
    {
      Name = newName,
      IsSelected = true,
      Branch = settings.DefaultBranch,
      RunSast = settings.DefaultRunSast,
      RunSca = settings.DefaultRunSca,
      Incremental = settings.DefaultIncremental
    });

    SaveProjects();
    txtNewProject.Clear();
  }

  private void BtnRemoveProject_Click(object sender, RoutedEventArgs e)
  {
    if (sender is FrameworkElement element && element.DataContext is ProjectItem project)
    {
      SavedProjects.Remove(project);
      if (_selectedProject == project)
        ClearDetailPanel();
      SaveProjects();
    }
  }

  private async void BtnImportSolution_Click(object sender, RoutedEventArgs e)
  {
    var dialog = new OpenFileDialog
    {
      Filter = "Solution files (*.sln)|*.sln",
      Title = "Selecione um arquivo .sln"
    };

    if (dialog.ShowDialog() != true) return;

    var projects = SolutionParserService.ParseSolution(dialog.FileName);
    int added = await AddValidatedProjectsToList(projects);
    AppendToConsole($"[IMPORTAÇÃO] {added} projeto(s) importado(s) de: {dialog.FileName}");
  }

  private async void BtnScanDirectory_Click(object sender, RoutedEventArgs e)
  {
    var dialog = new OpenFolderDialog
    {
      Title = "Selecione a pasta raiz"
    };

    if (dialog.ShowDialog() != true) return;

    var projects = DirectoryScannerService.ScanDirectory(dialog.FolderName);
    int added = await AddValidatedProjectsToList(projects);
    AppendToConsole($"[SCANNER] {added} projeto(s) encontrado(s) em: {dialog.FolderName}");
  }

  private async Task<int> AddValidatedProjectsToList(List<ProjectItem> projects)
  {
    int count = 0;
    foreach (var p in projects)
    {
      if (SavedProjects.Any(existing => existing.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
        continue;

      if (!await ValidateProjectExistsAsync(p.Name))
        return count;

      SavedProjects.Add(p);
      count++;
    }
    if (count > 0) SaveProjects();
    return count;
  }

  private async Task<bool> ValidateProjectExistsAsync(string projectName)
  {
    var settings = AppSettingsService.Instance;

    if (settings.BypassApiValidation)
    {
      return true;
    }

    string apiKey = LoadDecryptedApiKey();

    if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(settings.Tenant))
    {
      MessageBox.Show("API Key e Tenant são obrigatórios. Configure-os na aba Configurações.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
      return false;
    }

    var apiService = new CheckmarxApiService();
    var validation = await apiService.ValidateProjectExistsAsync(
        projectName, settings.Tenant, apiKey, settings.BaseUri, settings.BaseAuthUri);

    if (validation.ApiCallFailed)
    {
      var result = MessageBox.Show(
          $"{validation.ApiErrorMessage}\n\nDeseja ignorar esta validação de API e adicionar/importar o projeto mesmo assim?",
          "Erro de Validação (API)",
          MessageBoxButton.YesNo,
          MessageBoxImage.Warning);
          
      return result == MessageBoxResult.Yes;
    }

    if (!validation.ProjectFound)
    {
      var result = MessageBox.Show(
          $"{validation.Message}\n\nDeseja adicionar/importar o projeto mesmo assim? (Nota: Ao escanear, se o projeto não existir, ele será criado no Checkmarx One)",
          "Projeto Não Encontrado",
          MessageBoxButton.YesNo,
          MessageBoxImage.Warning);

      return result == MessageBoxResult.Yes;
    }

    return true;
  }

  private void DgProjects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
  {
    if (dgProjects.SelectedItem is ProjectItem project)
      PopulateDetailPanel(project);
    else
      ClearDetailPanel();
  }

  private void PopulateDetailPanel(ProjectItem project)
  {
    _isUpdatingDetail = true;
    _selectedProject = project;

    lblDetailTitle.Text = $"Editando: {project.Name}";
    pnlProjectDetail.IsEnabled = true;

    txtDetailLocalPath.Text = project.LocalPath;
    txtDetailBranch.Text = project.Branch;
    txtDetailTags.Text = project.Tags;
    txtDetailProjectTags.Text = project.ProjectTags;
    txtDetailProjectGroups.Text = project.ProjectGroups;
    chkDetailSast.IsChecked = project.RunSast;
    chkDetailSca.IsChecked = project.RunSca;
    chkDetailIncremental.IsChecked = project.Incremental;

    _isUpdatingDetail = false;
  }

  private void ClearDetailPanel()
  {
    _isUpdatingDetail = true;
    _selectedProject = null;

    lblDetailTitle.Text = "Selecione um projeto na lista";
    pnlProjectDetail.IsEnabled = false;

    txtDetailLocalPath.Text = "";
    txtDetailBranch.Text = "";
    txtDetailTags.Text = "";
    txtDetailProjectTags.Text = "";
    txtDetailProjectGroups.Text = "";
    chkDetailSast.IsChecked = false;
    chkDetailSca.IsChecked = false;
    chkDetailIncremental.IsChecked = false;

    _isUpdatingDetail = false;
  }

  private void DetailField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
  {
    if (_isUpdatingDetail || _selectedProject == null) return;

    _selectedProject.LocalPath = txtDetailLocalPath.Text;
    _selectedProject.Branch = txtDetailBranch.Text;
    _selectedProject.Tags = txtDetailTags.Text;
    _selectedProject.ProjectTags = txtDetailProjectTags.Text;
    _selectedProject.ProjectGroups = txtDetailProjectGroups.Text;
    SaveProjects();
  }

  private void DetailCheckbox_Changed(object sender, RoutedEventArgs e)
  {
    if (_isUpdatingDetail || _selectedProject == null) return;

    _selectedProject.RunSast = chkDetailSast.IsChecked == true;
    _selectedProject.RunSca = chkDetailSca.IsChecked == true;
    _selectedProject.Incremental = chkDetailIncremental.IsChecked == true;
    SaveProjects();
  }

  private void BtnBrowseProjectFolder_Click(object sender, RoutedEventArgs e)
  {
    var dialog = new OpenFolderDialog { Title = "Selecione a Pasta do Projeto" };
    if (dialog.ShowDialog() == true)
    {
      txtDetailLocalPath.Text = dialog.FolderName;
    }
  }

  private void BtnApplyBatchTags_Click(object sender, RoutedEventArgs e)
  {
    var selected = SavedProjects.Where(p => p.IsSelected).ToList();
    if (selected.Count == 0)
    {
      MessageBox.Show("Selecione pelo menos um projeto (☑) na lista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
      return;
    }

    string batchTags = txtBatchTags.Text.Trim();
    string batchProjectTags = txtBatchProjectTags.Text.Trim();
    string batchProjectGroups = txtBatchProjectGroups.Text.Trim();

    foreach (var project in selected)
    {
      if (!string.IsNullOrEmpty(batchTags)) project.Tags = batchTags;
      if (!string.IsNullOrEmpty(batchProjectTags)) project.ProjectTags = batchProjectTags;
      if (!string.IsNullOrEmpty(batchProjectGroups)) project.ProjectGroups = batchProjectGroups;
    }

    SaveProjects();

    if (_selectedProject != null && selected.Contains(_selectedProject))
      PopulateDetailPanel(_selectedProject);

    AppendToConsole($"[LOTE] Tags aplicadas a {selected.Count} projeto(s).");
  }

  private void BtnSettingBrowseCli_Click(object sender, RoutedEventArgs e)
  {
    var dialog = new OpenFileDialog
    {
      Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
      Title = "Selecione o executável do Checkmarx One CLI (cx.exe)"
    };
    if (dialog.ShowDialog() == true)
      txtSettingCliPath.Text = dialog.FileName;
  }

  private async void BtnSettingInstallCli_Click(object sender, RoutedEventArgs e)
  {
    string url = "https://github.com/Checkmarx/ast-cli/releases/latest/download/ast-cli_windows_x64.zip";
    string destFolder = @"C:\Checkmarx";
    string zipPath = Path.Combine(Path.GetTempPath(), "ast-cli.zip");

    btnSettingInstallCli.IsEnabled = false;
    AppendToConsole("Baixando Checkmarx CLI mais recente pelo GitHub...");
    try
    {
      using HttpClient client = new();
      var response = await client.GetByteArrayAsync(url);
      await File.WriteAllBytesAsync(zipPath, response);

      if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
      ZipFile.ExtractToDirectory(zipPath, destFolder, true);

      txtSettingCliPath.Text = Path.Combine(destFolder, "cx.exe");
      AppendToConsole("CLI instalado com sucesso em: " + txtSettingCliPath.Text);
    }
    catch (Exception ex)
    {
      AppendToConsole("Erro ao instalar CLI: " + ex.Message);
    }
    finally
    {
      btnSettingInstallCli.IsEnabled = true;
    }
  }

  private async void BtnSettingAuth_Click(object sender, RoutedEventArgs e)
  {
    string cliPath = txtSettingCliPath.Text;
    string tenant = txtSettingTenant.Text.Trim();
    string apiKey = txtSettingApiKey.Password;
    string baseUri = txtSettingBaseUri.Text.Trim();
    string baseAuthUri = txtSettingBaseAuthUri.Text.Trim();

    if (!File.Exists(cliPath))
    {
      MessageBox.Show("Caminho do CLI inválido. Verifique se o arquivo cx.exe existe.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
      return;
    }
    if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(apiKey))
    {
      MessageBox.Show("Tenant Name e API Key são obrigatórios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
      return;
    }

    SaveEncryptedApiKey(apiKey);

    lblSettingAuthStatus.Text = "Autenticando...";
    lblSettingAuthStatus.Foreground = Brushes.Orange;
    btnSettingAuth.IsEnabled = false;

    AppendToConsole("--- Iniciando Autenticação ---");
    string args = $"auth validate --tenant \"{tenant}\" --apikey \"{apiKey}\" --base-uri \"{baseUri}\" --base-auth-uri \"{baseAuthUri}\"";

    using var cts = new CancellationTokenSource();
    bool success = await _cliService.RunScanAsync(cliPath, args, null!, apiKey, cts.Token);

    if (success)
    {
      lblSettingAuthStatus.Text = "Autenticado";
      lblSettingAuthStatus.Foreground = Brushes.LightGreen;
      btnScan.IsEnabled = true;
      AppendToConsole("Autenticação validada com sucesso.\n");
    }
    else
    {
      lblSettingAuthStatus.Text = "Falha na Validação";
      lblSettingAuthStatus.Foreground = Brushes.Red;
      AppendToConsole("Falha na validação de autenticação. Verifique suas credenciais.\n");
    }
    btnSettingAuth.IsEnabled = true;
    UpdateFooterStatus();
  }

  private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
  {
    var settings = AppSettingsService.Instance;
    settings.CliPath = txtSettingCliPath.Text;
    settings.Tenant = txtSettingTenant.Text.Trim();
    settings.BaseUri = txtSettingBaseUri.Text.Trim();
    settings.BaseAuthUri = txtSettingBaseAuthUri.Text.Trim();
    settings.DefaultBranch = txtSettingDefaultBranch.Text.Trim();
    settings.DefaultRunSast = chkSettingDefaultSast.IsChecked == true;
    settings.DefaultRunSca = chkSettingDefaultSca.IsChecked == true;
    settings.DefaultIncremental = chkSettingDefaultIncremental.IsChecked == true;
    settings.BypassApiValidation = chkSettingBypassValidation.IsChecked == true;
    settings.Theme = cmbSettingTheme.SelectedIndex == 1 ? "Light" : "Dark";

    if (!string.IsNullOrEmpty(txtSettingApiKey.Password))
    {
      SaveEncryptedApiKey(txtSettingApiKey.Password);
    }

    AppSettingsService.Save(settings);
    ApplyTheme(settings.Theme);
    UpdateFooterStatus();

    MessageBox.Show("Configurações salvas com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
  }

  private void CmbSettingTheme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
  {
    if (cmbSettingTheme == null) return;
    string newTheme = cmbSettingTheme.SelectedIndex == 1 ? "Light" : "Dark";
    ApplyTheme(newTheme);
  }

  private async void BtnScan_Click(object sender, RoutedEventArgs e)
  {
    var settings = AppSettingsService.Instance;
    string cliPath = settings.CliPath;
    string tenant = settings.Tenant;
    string apiKey = LoadDecryptedApiKey();

    var selectedProjects = SavedProjects.Where(p => p.IsSelected).ToList();

    if (!File.Exists(cliPath))
    {
      MessageBox.Show("Caminho do CLI inválido nas configurações.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
      return;
    }
    if (selectedProjects.Count == 0)
    {
      MessageBox.Show("Selecione pelo menos um projeto na lista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
      return;
    }

    if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(tenant))
    {
      MessageBox.Show("API Key e Tenant são obrigatórios. Configure-os na aba Configurações.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
      return;
    }

    var apiService = new CheckmarxApiService();
    foreach (var proj in selectedProjects)
    {
      if (settings.BypassApiValidation)
      {
        AppendToConsole($"[VALIDAÇÃO] Validação via API para o projeto \"{proj.Name}\" ignorada (configuração ativa).");
        continue;
      }

      AppendToConsole($"[VALIDAÇÃO] Verificando existência do projeto \"{proj.Name}\"...");
      var validation = await apiService.ValidateProjectExistsAsync(
          proj.Name, tenant, apiKey, settings.BaseUri, settings.BaseAuthUri);

      if (validation.ApiCallFailed)
      {
        AppendToConsole($"[ERRO] {validation.ApiErrorMessage}");
        var result = MessageBox.Show(
            $"{validation.ApiErrorMessage}\n\nDeseja ignorar este erro de validação via API e prosseguir com o scan mesmo assim?",
            "Erro de Validação (API)",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
          return;
        }
        AppendToConsole("[VALIDAÇÃO] Erro de validação ignorado pelo usuário.");
      }
      else if (!validation.ProjectFound)
      {
        AppendToConsole($"[ERRO] {validation.Message}");
        var result = MessageBox.Show(
            $"{validation.Message}\n\nDeseja prosseguir com o scan mesmo assim? (Nota: Se o projeto não existir, ele será criado no Checkmarx One)",
            "Projeto Não Encontrado",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
          return;
        }
        AppendToConsole("[VALIDAÇÃO] Validação ignorada (projeto não encontrado).");
      }
      else
      {
        AppendToConsole($"[VALIDAÇÃO] Projeto \"{proj.Name}\" encontrado (ID: {validation.ProjectId}).");
      }
    }

    SetScanRunningState(true);
    _scanCts = new CancellationTokenSource();

    int projectIndex = 0;
    foreach (var proj in selectedProjects)
    {
      if (_scanCts.Token.IsCancellationRequested) break;

      projectIndex++;
      AppendToConsole($"\n\n=== [{projectIndex}/{selectedProjects.Count}] Scan: {proj.Name} ===");
      UpdateProgressLabel($"Escaneando {proj.Name} ({projectIndex}/{selectedProjects.Count})...");
      progressBar.Value = 0;

      string workingDir = ResolveWorkingDirectory(proj);
      string reportDir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "CxOneScan", "Reports", proj.Name);

      try
      {
          if (Directory.Exists(reportDir))
              Directory.Delete(reportDir, true);
          Directory.CreateDirectory(reportDir);
      }
      catch (Exception ex)
      {
          AppendToConsole($"[AVISO] Falha ao limpar/criar pasta de relatórios: {ex.Message}");
      }
      List<string> scanTypes = BuildScanTypes(proj);

      if (scanTypes.Count == 0)
      {
        AppendToConsole($"[AVISO] {proj.Name}: nenhum tipo de scan selecionado, pulando.");
        continue;
      }

      string args = CxCliService.BuildScanArguments(
          proj.Name, proj.Branch, scanTypes,
          proj.Tags, proj.ProjectTags, proj.ProjectGroups,
          tenant, apiKey, settings.BaseUri, settings.BaseAuthUri, reportDir,
          proj.Incremental);

      bool success = await _cliService.RunScanAsync(cliPath, args, workingDir, apiKey, _scanCts.Token);

      if (_scanCts.Token.IsCancellationRequested)
      {
        AppendToConsole("[CANCELADO] Operação cancelada pelo usuário.");
        break;
      }

      var result = ProcessScanResult(proj.Name, reportDir, success);
      result.ProjectLocalPath = workingDir;
      ScanResults.Add(result);

      string status = success ? "[SUCESSO]" : "[FALHA]";
      AppendToConsole($"{status} Scan finalizado para {proj.Name}.\n");
    }

    SetScanRunningState(false);

    if (ScanResults.Count > 0)
      mainTabControl.SelectedIndex = 1;
  }

  private static string ResolveWorkingDirectory(ProjectItem project)
  {
    if (!string.IsNullOrWhiteSpace(project.LocalPath) && Directory.Exists(project.LocalPath))
      return project.LocalPath;

    return Directory.GetCurrentDirectory();
  }

  private static List<string> BuildScanTypes(ProjectItem project)
  {
    var types = new List<string>();
    if (project.RunSast) types.Add("sast");
    if (project.RunSca) types.Add("sca");
    return types;
  }

  private static ScanResult ProcessScanResult(string projectName, string reportDir, bool scanSuccess)
  {
    if (!scanSuccess)
    {
      return new ScanResult
      {
        ProjectName = projectName,
        Success = false,
        StatusMessage = "Scan falhou"
      };
    }

    return ReportParserService.ParseReport(reportDir, projectName);
  }

  private void SetScanRunningState(bool isRunning)
  {
    btnScan.IsEnabled = !isRunning;
    btnCancel.IsEnabled = isRunning;
    progressBar.Value = isRunning ? 0 : 100;
    UpdateProgressLabel(isRunning ? "Iniciando..." : "Pronto");

    if (!isRunning)
    {
      _scanCts?.Dispose();
      _scanCts = null;
    }
  }

  private void BtnCancel_Click(object sender, RoutedEventArgs e)
  {
    _scanCts?.Cancel();
    _cliService.KillActiveProcess();
    AppendToConsole("[CANCELADO] Operação de scan cancelada pelo usuário.");
    SetScanRunningState(false);
  }

  private void BtnOpenReport_Click(object sender, RoutedEventArgs e)
  {
    if (sender is FrameworkElement element && element.DataContext is ScanResult result)
    {
      if (string.IsNullOrEmpty(result.ReportHtmlPath) || !File.Exists(result.ReportHtmlPath))
      {
        MessageBox.Show("Arquivo de relatório HTML não encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      Process.Start(new ProcessStartInfo
      {
        FileName = result.ReportHtmlPath,
        UseShellExecute = true
      });
    }
  }

  private void BtnAnalyzeCopilot_Click(object sender, RoutedEventArgs e)
  {
    if (sender is FrameworkElement element && element.DataContext is ScanResult result)
    {
      if (string.IsNullOrEmpty(result.SarifFilePath) || !File.Exists(result.SarifFilePath))
      {
        MessageBox.Show("Arquivo de relatório SARIF não encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      try
      {
          string sarifContent = File.ReadAllText(result.SarifFilePath);
          using var doc = JsonDocument.Parse(sarifContent);
          var root = doc.RootElement;

          var vulns = new List<VulnerabilityItem>();

          if (root.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
          {
              foreach (var run in runs.EnumerateArray())
              {
                  string toolName = "";
                  if (run.TryGetProperty("tool", out var tool) && tool.TryGetProperty("driver", out var driver))
                  {
                      if (driver.TryGetProperty("name", out var dName))
                          toolName = dName.GetString() ?? "";
                  }

                  // Build rules severity dictionary
                  var ruleSeverities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                  if (run.TryGetProperty("tool", out var tObj) && tObj.TryGetProperty("driver", out var dObj))
                  {
                      if (dObj.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
                      {
                          foreach (var rule in rules.EnumerateArray())
                          {
                              string ruleId = rule.TryGetProperty("id", out var rId) ? rId.GetString() ?? "" : "";
                              if (string.IsNullOrEmpty(ruleId)) continue;

                              string severity = "Medium";
                              if (rule.TryGetProperty("properties", out var props) && props.TryGetProperty("security-severity", out var secSev))
                              {
                                  string sevStr = secSev.ValueKind == JsonValueKind.Number ? secSev.GetDouble().ToString("0.0", CultureInfo.InvariantCulture) : secSev.GetString() ?? "";
                                  if (sevStr == "9.0") severity = "Critical";
                                  else if (sevStr == "7.0") severity = "High";
                                  else if (sevStr == "4.0") severity = "Medium";
                                  else if (sevStr == "2.0") severity = "Low";
                                  else if (sevStr == "1.0") severity = "Info";
                              }
                              ruleSeverities[ruleId] = severity;
                          }
                      }
                  }

                  if (run.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                  {
                      foreach (var vuln in results.EnumerateArray())
                      {
                          string ruleId = vuln.TryGetProperty("ruleId", out var rId) ? rId.GetString() ?? "Desconhecido" : "Desconhecido";
                          
                          string msg = "Sem detalhes";
                          if (vuln.TryGetProperty("message", out var messageObj) && messageObj.TryGetProperty("text", out var textProp))
                              msg = textProp.GetString() ?? "Sem detalhes";
                          
                          string fileLoc = "Desconhecido";
                          int lineLoc = 0;
                          if (vuln.TryGetProperty("locations", out var locs) && locs.ValueKind == JsonValueKind.Array && locs.GetArrayLength() > 0)
                          {
                              var physLoc = locs[0].TryGetProperty("physicalLocation", out var pLoc) ? pLoc : default;
                              if (physLoc.ValueKind == JsonValueKind.Object)
                              {
                                  if (physLoc.TryGetProperty("artifactLocation", out var artLoc) && artLoc.TryGetProperty("uri", out var uriProp))
                                      fileLoc = uriProp.GetString() ?? "Desconhecido";
                                      
                                  if (physLoc.TryGetProperty("region", out var region) && region.TryGetProperty("startLine", out var sLine))
                                      lineLoc = sLine.GetInt32();
                              }
                          }

                          // Get severity from dictionary
                          string severity = "Medium";
                          if (ruleSeverities.TryGetValue(ruleId, out var mappedSeverity))
                          {
                              severity = mappedSeverity;
                          }

                          // Type parsing (fallback to ruleId if toolName is generic)
                          string type = "SAST";
                          if (toolName.Contains("SCA", StringComparison.OrdinalIgnoreCase) || ruleId.Contains("(sca)", StringComparison.OrdinalIgnoreCase))
                              type = "SCA";

                          vulns.Add(new VulnerabilityItem
                          {
                              Id = ruleId,
                              Message = msg,
                              FileLocation = fileLoc,
                              Line = lineLoc,
                              Severity = severity,
                              Type = type,
                              IsSelected = true
                          });
                      }
                  }
              }
          }

          if (vulns.Count == 0)
          {
              MessageBox.Show("Nenhuma vulnerabilidade encontrada no relatório SARIF para analisar.", "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
              return;
          }

          var filterWindow = new CopilotFilterWindow(vulns) { Owner = this };
          filterWindow.ShowDialog();

          if (!filterWindow.IsConfirmed) return;

          var selectedVulns = filterWindow.SelectedVulnerabilities;
          if (selectedVulns.Count == 0)
          {
              MessageBox.Show("Nenhuma vulnerabilidade selecionada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
              return;
          }

          var prompt = new StringBuilder();
          prompt.AppendLine("Atue como um especialista em segurança de código. O Checkmarx encontrou as seguintes vulnerabilidades no meu projeto. Como posso corrigi-las?");
          
          int count = 1;
          foreach (var v in selectedVulns)
          {
              prompt.AppendLine($"{count}) [{v.Id} ({v.Type})] Arquivo: {v.FileLocation} na linha {v.Line}");
              prompt.AppendLine($"   Detalhe: {v.Message}");
              count++;
          }

          Clipboard.SetText(prompt.ToString());
          MessageBox.Show("O prompt detalhado das vulnerabilidades selecionadas foi copiado para a Área de Transferência!\n\nAbra o chat do Copilot no Visual Studio e cole o texto (Ctrl+V) para analisar.", "Copiado com Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);

          if (!string.IsNullOrEmpty(result.ProjectLocalPath) && Directory.Exists(result.ProjectLocalPath))
          {
              try
              {
                  Process.Start(new ProcessStartInfo
                  {
                      FileName = "devenv",
                      Arguments = $"\"{result.ProjectLocalPath}\"",
                      UseShellExecute = true
                  });
              }
              catch
              {
                  Process.Start(new ProcessStartInfo
                  {
                      FileName = "explorer",
                      Arguments = $"\"{result.ProjectLocalPath}\"",
                      UseShellExecute = true
                  });
              }
          }
      }
      catch (Exception ex)
      {
          MessageBox.Show($"Erro ao processar o SARIF ou abrir o Visual Studio: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }
  }

  private void AppendToConsole(string text)
  {
    Dispatcher.Invoke(() =>
    {
      txtConsole.AppendText(text + Environment.NewLine);
      txtConsole.ScrollToEnd();
    });
  }

  private void UpdateProgress(int percent)
  {
    Dispatcher.Invoke(() =>
    {
      progressBar.Value = percent;
      lblProgress.Text = $"{percent}%";
    });
  }

  private void UpdateProgressLabel(string text)
  {
    Dispatcher.Invoke(() => { lblProgress.Text = text; });
  }

  private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
  {
    Process.Start(new ProcessStartInfo
    {
      FileName = e.Uri.AbsoluteUri,
      UseShellExecute = true
    });
    e.Handled = true;
  }

  private readonly string KeyFile = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "CxDesktopWrapper", "secure_key.dat");

  private void SaveEncryptedApiKey(string apiKey)
  {
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(KeyFile)!);
      byte[] plainBytes = Encoding.UTF8.GetBytes(apiKey);
      byte[] dataToWrite;
      try
      {
        dataToWrite = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
      }
      catch (PlatformNotSupportedException)
      {
        dataToWrite = Encoding.UTF8.GetBytes("FALLBACK:" + Convert.ToBase64String(plainBytes));
      }
      File.WriteAllBytes(KeyFile, dataToWrite);
    }
    catch (Exception ex)
    {
      AppendToConsole("Erro ao salvar a API Key: " + ex.Message);
    }
  }

  private string LoadDecryptedApiKey()
  {
    if (!File.Exists(KeyFile)) return string.Empty;

    try
    {
      byte[] bytes = File.ReadAllBytes(KeyFile);
      try
      {
        byte[] plainBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
      }
      catch (PlatformNotSupportedException)
      {
        string text = Encoding.UTF8.GetString(bytes);
        if (text.StartsWith("FALLBACK:"))
        {
          byte[] plainBytes = Convert.FromBase64String(text.Substring(9));
          return Encoding.UTF8.GetString(plainBytes);
        }
        return string.Empty;
      }
    }
    catch
    {
      return string.Empty;
    }
  }
}