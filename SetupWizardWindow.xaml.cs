using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using CxDesktopWrapper.Services;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper;

public partial class SetupWizardWindow : Window
{
    private int _currentStep = 0;
    private readonly CxCliService _cliService = new();
    private readonly string KeyFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CxDesktopWrapper", "secure_key.dat");

    public SetupWizardWindow()
    {
        InitializeComponent();
        UpdateStepUI();
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
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
            if (lblAuthStatus.Text != "Autenticado")
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
            settings.IsFirstRunCompleted = true;
            AppSettingsService.Save(settings);

            if (!string.IsNullOrEmpty(txtApiKey.Password))
            {
                SaveEncryptedApiKey(txtApiKey.Password);
            }

            DialogResult = true;
            Close();
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
            lblCliStatus.Foreground = Brushes.LightGreen;
        }
    }

    private async void BtnInstallCli_Click(object sender, RoutedEventArgs e)
    {
        string url = "https://github.com/Checkmarx/ast-cli/releases/latest/download/ast-cli_windows_x64.zip";
        string destFolder = @"C:\Checkmarx";
        string zipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ast-cli.zip");

        btnInstallCli.IsEnabled = false;
        lblCliStatus.Text = "Baixando Checkmarx CLI mais recente...";
        lblCliStatus.Foreground = Brushes.Orange;

        try
        {
            using HttpClient client = new();
            var response = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(zipPath, response);

            if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
            ZipFile.ExtractToDirectory(zipPath, destFolder, true);

            txtCliPath.Text = System.IO.Path.Combine(destFolder, "cx.exe");
            lblCliStatus.Text = "CLI instalado com sucesso em: " + txtCliPath.Text;
            lblCliStatus.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            lblCliStatus.Text = "Erro ao instalar CLI: " + ex.Message;
            lblCliStatus.Foreground = Brushes.Red;
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

        if (!File.Exists(cliPath))
        {
            MessageBox.Show("Por favor, instale ou configure o caminho do CLI primeiro.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(apiKey))
        {
            MessageBox.Show("Tenant e API Key são obrigatórios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        btnValidateAuth.IsEnabled = false;
        lblAuthStatus.Text = "Conectando...";
        lblAuthStatus.Foreground = Brushes.Orange;

        string args = $"auth validate --tenant \"{tenant}\" --apikey \"{apiKey}\" --base-uri \"https://ast.checkmarx.net\" --base-auth-uri \"https://iam.checkmarx.net\"";

        using var cts = new CancellationTokenSource();
        bool success = await _cliService.RunScanAsync(cliPath, args, null!, apiKey, cts.Token);

        if (success)
        {
            lblAuthStatus.Text = "Autenticado";
            lblAuthStatus.Foreground = Brushes.LightGreen;
        }
        else
        {
            lblAuthStatus.Text = "Falha na validação";
            lblAuthStatus.Foreground = Brushes.Red;
        }
        btnValidateAuth.IsEnabled = true;
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

    private void SaveEncryptedApiKey(string apiKey)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(KeyFile)!);
            byte[] plainBytes = Encoding.UTF8.GetBytes(apiKey);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFile, encryptedBytes);
        }
        catch
        {
        }
    }
}
