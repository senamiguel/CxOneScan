using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CxDesktopWrapper.Services;
using CxDesktopWrapper.Models;
using CxDesktopWrapper.Common;

namespace CxDesktopWrapper;

public partial class MainWindow : Window
{
  public ObservableCollection<ProjectItem> SavedProjects { get; } = new();
  public ObservableCollection<ScanResult> ScanResults { get; } = new();

  private readonly CxCliService _cliService = new();
  private CancellationTokenSource? _scanCts;
  private ProjectItem? _selectedProject;
  private bool _isUpdatingDetail;
  private bool _isAuthenticatedInSession;
  private CancellationTokenSource? _saveCts;

  public MainWindow()
  {
    InitializeComponent();
    dgProjects.ItemsSource = SavedProjects;
    dgResults.ItemsSource = ScanResults;
    LoadProjects();
    LoadSettingsUI();
    WireCliServiceEvents();
    UpdateScanButtonState();
    TriggerStartupAuthCheck();
  }

  private void WireCliServiceEvents()
  {
    _cliService.OutputReceived += text => AppendToConsole(text);
    _cliService.ProgressChanged += percent => UpdateProgress(percent);
  }

  private void LoadProjects()
  {
    try
    {
      var projects = ProjectPersistenceService.Load();
      SavedProjects.Clear();
      foreach (var p in projects)
      {
        SavedProjects.Add(p);
      }
    }
    catch (Exception ex)
    {
      AppendToConsole("Erro ao carregar lista de projetos: " + ex.Message);
    }
  }

  private void LoadSettingsUI()
  {
    var settings = AppSettingsService.Instance;

    txtSettingApiKey.Password = CredentialService.LoadDecryptedApiKey();
    cmbSettingTheme.SelectedIndex = settings.Theme == "Light" ? 1 : 0;
    ApplyTheme(settings.Theme);

    UpdateFooterStatus();
  }

  private void UpdateFooterStatus()
  {
    var settings = AppSettingsService.Instance;
    string apiKey = CredentialService.LoadDecryptedApiKey();

    if (string.IsNullOrEmpty(settings.Tenant) || string.IsNullOrEmpty(apiKey))
    {
      lblFooterStatus.Text = "⚠️ Não Configurado";
      lblFooterStatus.Foreground = (Brush)FindResource("WarningBrush");
      lblProgress.Text = "Aguardando Configuração";
    }
    else
    {
      lblFooterStatus.Text = "✓ Configurado";
      lblFooterStatus.Foreground = (Brush)FindResource("SuccessBrush");
      lblProgress.Text = "Pronto";
    }
  }

  private void BtnAddProject_Click(object sender, RoutedEventArgs e)
  {
    string newName = txtNewProject.Text.Trim();
    if (string.IsNullOrEmpty(newName)) return;
    if (SavedProjects.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))) return;

    var settings = AppSettingsService.Instance;
    var newProj = new ProjectItem
    {
      Name = newName,
      IsSelected = true,
      Branch = settings.DefaultBranch,
      RunSast = settings.DefaultRunSast,
      RunSca = settings.DefaultRunSca,
      Incremental = settings.DefaultIncremental,
      Tags = settings.DefaultTags,
      ProjectTags = settings.DefaultProjectTags,
      ProjectGroups = settings.DefaultProjectGroups
    };

    SavedProjects.Add(newProj);
    ProjectPersistenceService.Save(SavedProjects);
    dgProjects.SelectedItem = newProj;
    txtNewProject.Clear();
  }

  private void TxtNewProject_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
  {
    if (e.Key == System.Windows.Input.Key.Enter)
    {
      BtnAddProject_Click(sender, e);
    }
  }

  private void BtnRemoveProject_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      if (sender is FrameworkElement element && element.DataContext is ProjectItem project)
      {
        var confirm = MessageBox.Show($"Deseja realmente remover o projeto \"{project.Name}\"?", "Confirmar Remoção", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        SavedProjects.Remove(project);
        if (_selectedProject == project)
          ClearDetailPanel();
        ProjectPersistenceService.Save(SavedProjects);
      }
    }
    catch (Exception ex)
    {
      MessageBox.Show("Erro ao remover projeto: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
    }
  }

  private void DgProjects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
  {
    try
    {
      if (dgProjects.SelectedItem is ProjectItem project)
      {
        PopulateDetailPanel(project);
      }
      else
      {
        ClearDetailPanel();
      }
      UpdateScanButtonState();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine("Erro ao selecionar projeto: " + ex.Message);
    }
  }

  private void PopulateDetailPanel(ProjectItem project)
  {
    _isUpdatingDetail = true;
    _selectedProject = project;

    lblDetailTitle.Text = $"Editando: {project.Name}";
    pnlProjectDetail.IsEnabled = true;

    txtDetailName.Text = project.Name;
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

    lblDetailTitle.Text = "Dê duplo clique em um projeto para editar";
    pnlProjectDetail.IsEnabled = false;

    txtDetailName.Text = "";
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

  private async void DetailField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
  {
    try
    {
      if (_isUpdatingDetail || _selectedProject == null) return;

      string newName = txtDetailName.Text.Trim();
      if (!string.IsNullOrEmpty(newName))
      {
        _selectedProject.Name = newName;
        lblDetailTitle.Text = $"Editando: {newName}";
      }

      _selectedProject.LocalPath = txtDetailLocalPath.Text;
      _selectedProject.Branch = txtDetailBranch.Text;
      _selectedProject.Tags = txtDetailTags.Text;
      _selectedProject.ProjectTags = txtDetailProjectTags.Text;
      _selectedProject.ProjectGroups = txtDetailProjectGroups.Text;

      await SaveProjectsDebouncedAsync();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine("Erro em DetailField_TextChanged: " + ex.Message);
    }
  }

  private async Task SaveProjectsDebouncedAsync()
  {
    _saveCts?.Cancel();
    _saveCts = new CancellationTokenSource();
    var token = _saveCts.Token;
    try
    {
      await Task.Delay(300, token);
      await Task.Run(() => ProjectPersistenceService.Save(SavedProjects), token);
    }
    catch (TaskCanceledException)
    {
      System.Diagnostics.Debug.WriteLine("Save debounce cancelado.");
    }
  }

  private async void DetailCheckbox_Changed(object sender, RoutedEventArgs e)
  {
    try
    {
      if (_isUpdatingDetail || _selectedProject == null) return;

      _selectedProject.RunSast = chkDetailSast.IsChecked == true;
      _selectedProject.RunSca = chkDetailSca.IsChecked == true;
      _selectedProject.Incremental = chkDetailIncremental.IsChecked == true;

      await SaveProjectsDebouncedAsync();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine("Erro em DetailCheckbox_Changed: " + ex.Message);
    }
  }

  private void BtnBrowseProjectFolder_Click(object sender, RoutedEventArgs e)
  {
    var dialog = new OpenFolderDialog { Title = "Selecione a Pasta do Projeto" };
    if (dialog.ShowDialog() == true)
    {
      txtDetailLocalPath.Text = dialog.FolderName;
    }
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
    btnSettingInstallCli.IsEnabled = false;
    AppendToConsole("Baixando Checkmarx CLI mais recente pelo GitHub...");
    try
    {
      using var cts = new CancellationTokenSource();
      string cliPath = await CliInstallerService.DownloadAndInstallCliAsync(
          CheckmarxApiService.SharedHttpClient,
          AppConstants.CliDownloadUrl,
          AppConstants.DefaultCliDirectory,
          msg => AppendToConsole(msg),
          cts.Token);

      txtSettingCliPath.Text = cliPath;
      UpdateScanButtonState();
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

  private void BtnScanDirectory_Click(object sender, RoutedEventArgs e)
  {
    var dialog = new OpenFolderDialog
    {
      Title = "Selecione a pasta raiz do projeto"
    };

    if (dialog.ShowDialog() != true) return;

    var projects = DirectoryScannerService.ScanDirectory(dialog.FolderName);
    int added = AddProjectsToList(projects);
    AppendToConsole($"[SCANNER] {added} projeto(s) encontrado(s) em: {dialog.FolderName}");
  }

  private int AddProjectsToList(List<ProjectItem> projects)
  {
    int count = 0;
    ProjectItem? lastAdded = null;
    foreach (var p in projects)
    {
      if (SavedProjects.Any(existing => existing.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
        continue;

      SavedProjects.Add(p);
      lastAdded = p;
      count++;
    }
    if (count > 0)
    {
      ProjectPersistenceService.Save(SavedProjects);
      if (lastAdded != null)
      {
        dgProjects.SelectedItem = lastAdded;
      }
    }
    return count;
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
    if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(baseUri) || string.IsNullOrEmpty(baseAuthUri))
    {
      MessageBox.Show("Todos os campos de credenciais e conexão são obrigatórios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
      return;
    }

    lblSettingAuthStatus.Text = "Autenticando...";
    lblSettingAuthStatus.Foreground = (Brush)FindResource("WarningBrush");
    btnSettingAuth.IsEnabled = false;

    try
    {
      AppendToConsole("--- Iniciando Autenticação ---");

      var envVars = new Dictionary<string, string>
      {
          { "CX_APIKEY", apiKey },
          { "CX_TENANT", tenant },
          { "CX_BASE_URI", baseUri },
          { "CX_BASE_AUTH_URI", baseAuthUri }
      };

      using var cts = new CancellationTokenSource();
      bool success = await _cliService.RunScanAsync(cliPath, new[] { "auth", "validate" }, AppDomain.CurrentDomain.BaseDirectory, envVars, cts.Token);

      if (success)
      {
        CredentialService.SaveEncryptedApiKey(apiKey);
        lblSettingAuthStatus.Text = "Autenticado";
        lblSettingAuthStatus.Foreground = (Brush)FindResource("SuccessBrush");
        _isAuthenticatedInSession = true;
        UpdateScanButtonState();
        AppendToConsole("Autenticação validada com sucesso.\n");
      }
      else
      {
        lblSettingAuthStatus.Text = "Falha na Validação";
        lblSettingAuthStatus.Foreground = (Brush)FindResource("ErrorBrush");
        _isAuthenticatedInSession = false;
        UpdateScanButtonState();
        AppendToConsole("Falha na validação de autenticação. Verifique suas credenciais.\n");
      }
    }
    catch (Exception ex)
    {
      AppendToConsole("Erro inesperado na autenticação: " + ex.Message);
      lblSettingAuthStatus.Text = "Erro na Autenticação";
      lblSettingAuthStatus.Foreground = (Brush)FindResource("ErrorBrush");
      _isAuthenticatedInSession = false;
      UpdateScanButtonState();
    }
    finally
    {
      btnSettingAuth.IsEnabled = true;
      UpdateFooterStatus();
    }
  }

  private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      var settings = AppSettingsService.Instance;
      
      settings.Theme = cmbSettingTheme.SelectedIndex == 1 ? "Light" : "Dark";

      string apiKey = txtSettingApiKey.Password;
      if (!string.IsNullOrEmpty(apiKey))
      {
        CredentialService.SaveEncryptedApiKey(apiKey);
      }

      AppSettingsService.Save();
      ApplyTheme(settings.Theme);
      UpdateFooterStatus();
      UpdateScanButtonState();

      MessageBox.Show("Configurações salvas com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
      MessageBox.Show("Erro ao salvar configurações: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
    }
  }

  private void CmbSettingTheme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
  {
    if (cmbSettingTheme == null) return;
    string newTheme = cmbSettingTheme.SelectedIndex == 1 ? "Light" : "Dark";
    ApplyTheme(newTheme);
  }

  private async void BtnScan_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      var settings = AppSettingsService.Instance;
      string cliPath = settings.CliPath;
      string tenant = settings.Tenant;
      string apiKey = CredentialService.LoadDecryptedApiKey();

      if (dgProjects.SelectedItem is not ProjectItem selectedProject)
      {
        MessageBox.Show("Selecione um projeto na lista lateral para iniciar o scan.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      var selectedProjects = new List<ProjectItem> { selectedProject };

      if (!ValidateScanPrerequisites(selectedProjects, cliPath, tenant, apiKey))
      {
        return;
      }

      if (!await ConfirmFullScanIfNeededAsync(selectedProjects))
      {
        return;
      }

      var apiService = new CheckmarxApiService();
      var projectIds = await ValidateProjectsInPortalAsync(selectedProjects, tenant, apiKey, settings, apiService);
      if (projectIds == null)
      {
        return;
      }

      await ExecuteScanQueueAsync(selectedProjects, cliPath, tenant, apiKey, settings, apiService, projectIds);
    }
    catch (Exception ex)
    {
      MessageBox.Show("Erro ao iniciar a fila de scans: " + ex.Message, "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
      SetScanRunningState(false);
    }
  }

  private bool ValidateScanPrerequisites(List<ProjectItem> selectedProjects, string cliPath, string tenant, string apiKey)
  {
    if (!File.Exists(cliPath))
    {
      MessageBox.Show("Caminho do CLI inválido nas configurações.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
      return false;
    }
    if (selectedProjects.Count == 0)
    {
      MessageBox.Show("Selecione pelo menos um projeto na lista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
      return false;
    }
    if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(tenant))
    {
      MessageBox.Show("API Key e Tenant são obrigatórios. Configure-os na aba Configurações.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
      return false;
    }
    return true;
  }

  private async Task<bool> ConfirmFullScanIfNeededAsync(List<ProjectItem> selectedProjects)
  {
    bool hasFullScan = selectedProjects.Any(p => !p.Incremental);
    if (hasFullScan)
    {
      var result = MessageBox.Show(
          "Você selecionou realizar scan completo (não incremental) para um ou mais projetos.\n\n" +
          "Atenção: Executar um scan completo pode fazer com que vulnerabilidades antigas sejam tratadas como novas no Checkmarx One.\n\n" +
          "Deseja prosseguir com o scan?",
          "Confirmação de Scan Completo",
          MessageBoxButton.YesNo,
          MessageBoxImage.Warning);

      return result == MessageBoxResult.Yes;
    }
    return true;
  }

  private async Task<Dictionary<string, string>?> ValidateProjectsInPortalAsync(
      List<ProjectItem> selectedProjects, string tenant, string apiKey, AppSettings settings, CheckmarxApiService apiService)
  {
    var projectIds = new Dictionary<string, string>();
    foreach (var proj in selectedProjects)
    {
      AppendToConsole($"[VALIDAÇÃO] Verificando existência do projeto \"{proj.Name}\"...");
      var validation = await apiService.ValidateProjectExistsAsync(
          proj.Name, tenant, apiKey, settings.BaseUri, settings.BaseAuthUri);

      if (validation.ApiCallFailed)
      {
        AppendToConsole($"[ERRO] {validation.ApiErrorMessage}");
        MessageBox.Show(validation.ApiErrorMessage, "Erro de Validação", MessageBoxButton.OK, MessageBoxImage.Error);
        return null;
      }

      if (!validation.ProjectFound)
      {
        AppendToConsole($"[ERRO] {validation.Message}");
        MessageBox.Show(validation.Message, "Projeto Não Encontrado", MessageBoxButton.OK, MessageBoxImage.Error);
        return null;
      }

      projectIds[proj.Name] = validation.ProjectId ?? string.Empty;
      AppendToConsole($"[VALIDAÇÃO] Projeto \"{proj.Name}\" encontrado (ID: {validation.ProjectId}).");
    }
    return projectIds;
  }

  private async Task ExecuteScanQueueAsync(
      List<ProjectItem> selectedProjects, string cliPath, string tenant, string apiKey, AppSettings settings, 
      CheckmarxApiService apiService, Dictionary<string, string> projectIds)
  {
    SetScanRunningState(true);
    _scanCts = new CancellationTokenSource();

    int projectIndex = 0;
    foreach (var proj in selectedProjects)
    {
      if (_scanCts.Token.IsCancellationRequested) break;

      projectIndex++;
      AppendToConsole($"\n\n=== [{projectIndex}/{selectedProjects.Count}] Scan: {proj.Name} ===");
      UpdateProgressLabel($"Escaneando {proj.Name} ({projectIndex}/{selectedProjects.Count})...");
      
      Dispatcher.Invoke(() => progressBar.Value = 0);

      string workingDir = ResolveWorkingDirectory(proj);
      string reportDir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          "CxOneScan", "Reports", proj.Name);

      CleanReportDirectory(reportDir);

      List<string> scanTypes = BuildScanTypes(proj);
      if (scanTypes.Count == 0)
      {
        AppendToConsole($"[AVISO] {proj.Name}: nenhum tipo de scan selecionado, pulando.");
        continue;
      }

      bool runIncremental = proj.Incremental;
      if (runIncremental)
      {
        runIncremental = await VerifyBaselineScanAsync(proj, tenant, apiKey, settings, apiService, projectIds);
      }

      var args = CxCliService.BuildScanArguments(
          proj.Name, proj.Branch, scanTypes,
          proj.Tags, proj.ProjectTags, proj.ProjectGroups,
          reportDir,
          runIncremental);

      var envVars = new Dictionary<string, string>
      {
          { "CX_APIKEY", apiKey },
          { "CX_TENANT", tenant },
          { "CX_BASE_URI", settings.BaseUri },
          { "CX_BASE_AUTH_URI", settings.BaseAuthUri }
      };

      bool success = await _cliService.RunScanAsync(cliPath, args, workingDir, envVars, _scanCts.Token);

      if (_scanCts.Token.IsCancellationRequested)
      {
        AppendToConsole("[CANCELADO] Operação cancelada pelo usuário.");
        break;
      }

      var result = ProcessScanResult(proj.Name, reportDir, success);
      ScanResults.Add(result);

      string status = success ? "[SUCESSO]" : "[FALHA]";
      AppendToConsole($"{status} Scan finalizado para {proj.Name}.\n");
    }

    SetScanRunningState(false);

    if (ScanResults.Count > 0)
      mainTabControl.SelectedIndex = 1;
  }

  private void CleanReportDirectory(string reportDir)
  {
    try
    {
      Directory.CreateDirectory(reportDir);
      string jsonReport = Path.Combine(reportDir, "cx_result.json");
      string htmlReport = Path.Combine(reportDir, "cx_result.html");
      if (File.Exists(jsonReport)) File.Delete(jsonReport);
      if (File.Exists(htmlReport)) File.Delete(htmlReport);
    }
    catch (Exception ex)
    {
      AppendToConsole($"[AVISO] Falha ao limpar arquivos de relatórios anteriores: {ex.Message}");
    }
  }

  private async Task<bool> VerifyBaselineScanAsync(
      ProjectItem proj, string tenant, string apiKey, AppSettings settings, CheckmarxApiService apiService, Dictionary<string, string> projectIds)
  {
    AppendToConsole($"[VALIDAÇÃO] Verificando histórico de scans da branch \"{proj.Branch}\" no Checkmarx One...");
    if (projectIds.TryGetValue(proj.Name, out string? projectId) && !string.IsNullOrEmpty(projectId))
    {
      bool hasCompletedScan = await apiService.CheckHasCompletedScanAsync(
          projectId, proj.Branch, tenant, apiKey, settings.BaseUri, settings.BaseAuthUri);

      if (!hasCompletedScan)
      {
        AppendToConsole($"[AVISO] Nenhum scan completo anterior foi encontrado na branch \"{proj.Branch}\". O primeiro scan será realizado como FULL para estabelecer a baseline.");
        return false;
      }
      else
      {
        AppendToConsole($"[INFO] Scan completo anterior detectado. Executando scan incremental.");
      }
    }
    return true;
  }

  private static string ResolveWorkingDirectory(ProjectItem project)
  {
    if (!string.IsNullOrWhiteSpace(project.LocalPath) && Directory.Exists(project.LocalPath))
      return project.LocalPath;

    return AppDomain.CurrentDomain.BaseDirectory;
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
    progressBar.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
    progressBar.Value = isRunning ? 0 : 100;
    UpdateProgressLabel(isRunning ? "Iniciando..." : "Pronto");

    if (!isRunning)
    {
      _scanCts?.Dispose();
      _scanCts = null;
      UpdateScanButtonState();
    }
  }

  private void UpdateScanButtonState()
  {
    var settings = AppSettingsService.Instance;
    string apiKey = CredentialService.LoadDecryptedApiKey();

    bool hasCredentials = !string.IsNullOrEmpty(settings.Tenant) && !string.IsNullOrEmpty(apiKey);
    bool hasSelectedProject = dgProjects.SelectedItem is ProjectItem;

    if (!hasSelectedProject)
    {
      btnScan.IsEnabled = false;
      btnScan.ToolTip = "Selecione um projeto na lista lateral para habilitar o scan.";
      return;
    }

    bool canScan = hasCredentials && (settings.KeepLoggedIn || _isAuthenticatedInSession);

    if (canScan)
    {
      btnScan.IsEnabled = true;
      btnScan.ToolTip = null;
    }
    else
    {
      btnScan.IsEnabled = false;
      btnScan.ToolTip = "Por favor, realize a autenticação na aba Configurações para habilitar o scan.";
    }
  }

  private void BtnCancel_Click(object sender, RoutedEventArgs e)
  {
    _scanCts?.Cancel();
    _cliService.KillActiveProcess();
    AppendToConsole("[CANCELADO] Operação de scan cancelada pelo usuário.");
    SetScanRunningState(false);
  }

  private void BtnClearResults_Click(object sender, RoutedEventArgs e)
  {
    try
    {
      ScanResults.Clear();
      AppendToConsole("[DASHBOARD] Resultados limpos com sucesso.");
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine("Erro ao limpar resultados: " + ex.Message);
    }
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

  private void ApplyTheme(string theme)
  {
    try
    {
      iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, theme == "Light" ? iNKORE.UI.WPF.Modern.ElementTheme.Light : iNKORE.UI.WPF.Modern.ElementTheme.Dark);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine("Erro ao aplicar tema: " + ex.Message);
    }
  }

  private void TriggerStartupAuthCheck()
  {
    var settings = AppSettingsService.Instance;
    string apiKey = CredentialService.LoadDecryptedApiKey();

    if (settings.KeepLoggedIn && !string.IsNullOrEmpty(settings.Tenant) && !string.IsNullOrEmpty(apiKey))
    {
      lblFooterStatus.Text = "⚡ Conectando...";
      lblFooterStatus.Foreground = (Brush)FindResource("WarningBrush");

      Task.Run(async () =>
      {
        try
        {
          var api = new CheckmarxApiService();
          bool connected = await api.TestConnectionAsync(settings.Tenant, apiKey, settings.BaseAuthUri);
          Dispatcher.Invoke(() =>
          {
            if (connected)
            {
              lblFooterStatus.Text = "✓ Autenticado";
              lblFooterStatus.Foreground = (Brush)FindResource("SuccessBrush");
              _isAuthenticatedInSession = true;
            }
            else
            {
              lblFooterStatus.Text = "⚠️ Falha na Autenticação";
              lblFooterStatus.Foreground = (Brush)FindResource("ErrorBrush");
              _isAuthenticatedInSession = false;
            }
            UpdateScanButtonState();
          });
        }
        catch (Exception ex)
        {
          System.Diagnostics.Debug.WriteLine("Erro no TriggerStartupAuthCheck: " + ex.Message);
          Dispatcher.Invoke(() =>
          {
            lblFooterStatus.Text = "⚠️ Erro de Rede";
            lblFooterStatus.Foreground = (Brush)FindResource("ErrorBrush");
            _isAuthenticatedInSession = false;
            UpdateScanButtonState();
          });
        }
      });
    }
  }
}