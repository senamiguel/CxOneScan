using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using CxDesktopWrapper.Services;
using CxDesktopWrapper.Models;
using CxDesktopWrapper.Common;

namespace CxDesktopWrapper;

public partial class SetupWizardWindow : Window
{
    private int _currentStep = 0;
    private readonly CxCliService _cliService = new();
    private bool _isWizardAuthenticated = false;

    public SetupWizardWindow()
    {
        InitializeComponent();
        ApplyTheme(AppSettingsService.Instance.Theme);
        UpdateStepUI();
    }

    private void ApplyTheme(string theme)
    {
        try
        {
            iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, theme == "Light" ? iNKORE.UI.WPF.Modern.ElementTheme.Light : iNKORE.UI.WPF.Modern.ElementTheme.Dark);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao aplicar tema no Wizard: " + ex.Message);
        }
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_currentStep == 1)
            {
                string cliPath = txtCliPath.Text;
                if (!File.Exists(cliPath))
                {
                    MessageBox.Show("Por favor, selecione um caminho de CLI (cx.exe) válido ou clique em Instalar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (_currentStep == 2)
            {
                if (!_isWizardAuthenticated)
                {
                    var result = MessageBox.Show("Você não testou a conexão com sucesso. Deseja continuar mesmo assim?", "Aviso", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes) return;
                }
            }

            if (_currentStep < 3)
            {
                _currentStep++;
                UpdateStepUI();
            }
            else
            {
                var settings = AppSettingsService.Instance;
                settings.CliPath = txtCliPath.Text;
                settings.Tenant = txtTenant.Text.Trim();
                settings.BaseUri = txtBaseUri.Text.Trim();
                settings.BaseAuthUri = txtBaseAuthUri.Text.Trim();
                settings.IsFirstRunCompleted = true;
                AppSettingsService.Save(settings);

                if (!string.IsNullOrEmpty(txtApiKey.Password))
                {
                    CredentialService.SaveEncryptedApiKey(txtApiKey.Password);
                }

                DialogResult = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erro ao avançar no assistente: " + ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 0)
        {
            _currentStep--;
            UpdateStepUI();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdateStepUI()
    {
        wizardTabs.SelectedIndex = _currentStep;

        btnBack.Visibility = _currentStep == 0 ? Visibility.Collapsed : Visibility.Visible;
        btnNext.Content = _currentStep == 3 ? "Concluir" : "Avançar";

        // Pré-popular campos com valores salvos (ou defaults) ao entrar nos steps
        if (_currentStep == 1)
        {
            string savedCliPath = AppSettingsService.Instance.CliPath;
            if (!string.IsNullOrEmpty(savedCliPath))
                txtCliPath.Text = savedCliPath;
        }
        else if (_currentStep == 2)
        {
            string savedBaseUri = AppSettingsService.Instance.BaseUri;
            string savedBaseAuthUri = AppSettingsService.Instance.BaseAuthUri;
            txtBaseUri.Text = !string.IsNullOrEmpty(savedBaseUri) ? savedBaseUri : "https://ast.checkmarx.net";
            txtBaseAuthUri.Text = !string.IsNullOrEmpty(savedBaseAuthUri) ? savedBaseAuthUri : "https://iam.checkmarx.net";
        }

        UpdateIndicators();
    }

    private void UpdateIndicators()
    {
        var activeBrush = (Brush)FindResource("SystemControlHighlightAccentBrush");
        var inactiveBrush = (Brush)FindResource("SystemControlDisabledBaseLowBrush");

        ind1.Fill = _currentStep == 0 ? activeBrush : inactiveBrush;
        ind2.Fill = _currentStep == 1 ? activeBrush : inactiveBrush;
        ind3.Fill = _currentStep == 2 ? activeBrush : inactiveBrush;
        ind4.Fill = _currentStep == 3 ? activeBrush : inactiveBrush;
    }

    private void BtnBrowseCli_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Selecione o executável do Checkmarx One CLI (cx.exe)"
        };
        if (dialog.ShowDialog() == true)
        {
            txtCliPath.Text = dialog.FileName;
            lblCliStatus.Text = "CLI selecionado: " + dialog.FileName;
            lblCliStatus.Foreground = (Brush)FindResource("SuccessBrush");
        }
    }

    private async void BtnInstallCli_Click(object sender, RoutedEventArgs e)
    {
        btnInstallCli.IsEnabled = false;
        lblCliStatus.Text = "Baixando Checkmarx CLI...";
        lblCliStatus.Foreground = (Brush)FindResource("WarningBrush");

        try
        {
            using var cts = new CancellationTokenSource();
            string cliPath = await CliInstallerService.DownloadAndInstallCliAsync(
                CheckmarxApiService.SharedHttpClient,
                AppConstants.CliDownloadUrl,
                AppConstants.DefaultCliDirectory,
                msg => Dispatcher.Invoke(() => lblCliStatus.Text = msg),
                cts.Token);

            txtCliPath.Text = cliPath;
            lblCliStatus.Text = "CLI instalado com sucesso em: " + cliPath;
            lblCliStatus.Foreground = (Brush)FindResource("SuccessBrush");
        }
        catch (Exception ex)
        {
            lblCliStatus.Text = "Erro ao instalar CLI: " + ex.Message;
            lblCliStatus.Foreground = (Brush)FindResource("ErrorBrush");
        }
        finally
        {
            btnInstallCli.IsEnabled = true;
        }
    }

    private async void BtnValidateAuth_Click(object sender, RoutedEventArgs e)
    {
        string cliPath = txtCliPath.Text;
        string tenant = txtTenant.Text.Trim();
        string apiKey = txtApiKey.Password;
        string baseUri = txtBaseUri.Text.Trim();
        string baseAuthUri = txtBaseAuthUri.Text.Trim();

        if (!File.Exists(cliPath))
        {
            MessageBox.Show("Por favor, instale ou configure o caminho do CLI primeiro.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(baseUri) || string.IsNullOrEmpty(baseAuthUri))
        {
            MessageBox.Show("Todos os campos são obrigatórios para validar a conexão.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        btnValidateAuth.IsEnabled = false;
        lblAuthStatus.Text = "Conectando...";
        lblAuthStatus.Foreground = (Brush)FindResource("WarningBrush");

        try
        {
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
                lblAuthStatus.Text = "Autenticado";
                lblAuthStatus.Foreground = (Brush)FindResource("SuccessBrush");
                _isWizardAuthenticated = true;
            }
            else
            {
                lblAuthStatus.Text = "Falha na validação";
                lblAuthStatus.Foreground = (Brush)FindResource("ErrorBrush");
                _isWizardAuthenticated = false;
            }
        }
        catch (Exception ex)
        {
            lblAuthStatus.Text = "Erro na validação: " + ex.Message;
            lblAuthStatus.Foreground = (Brush)FindResource("ErrorBrush");
            _isWizardAuthenticated = false;
        }
        finally
        {
            btnValidateAuth.IsEnabled = true;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao abrir link: " + ex.Message);
        }
    }
}
