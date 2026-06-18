using System.Windows;
using CxDesktopWrapper.Services;

namespace CxDesktopWrapper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettingsService.Load();

        if (!settings.IsFirstRunCompleted)
        {
            var wizard = new SetupWizardWindow();
            if (wizard.ShowDialog() == true)
            {
                var mainWin = new MainWindow();
                mainWin.Show();
            }
            else
            {
                Shutdown();
            }
        }
        else
        {
            var mainWin = new MainWindow();
            mainWin.Show();
        }
    }
}
