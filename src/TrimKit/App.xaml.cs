using System.Net.Http;
using System.Security.Principal;
using System.Windows;
using TrimKit.Services;
using TrimKit.ViewModels;

namespace TrimKit;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            System.Windows.MessageBox.Show(
                $"Unhandled error:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "TrimKit - Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            var logService = new LogService();
            var dismService = new DismService(logService);
            var registryService = new RegistryService(logService);
            var presetService = new PresetService();
            var isoService = new IsoService(logService);
            var serviceManager = new WindowsServiceManager(logService);
            var imageToolsService = new ImageToolsService(logService);
            var unattendService = new UnattendService(logService);
            var customizationService = new CustomizationService(logService);
            var componentRemovalService = new ComponentRemovalService(logService);
            var winSxsCleanupService = new WinSxsCleanupService(logService);

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TrimKit/1.0");
            httpClient.Timeout = TimeSpan.FromMinutes(30);

            var uupDumpService = new UupDumpService(httpClient, logService);
            var msDownloadService = new MicrosoftDownloadService(httpClient, logService);
            var dependencyService = new DependencyService(httpClient, logService);

            var downloadViewModel = new DownloadViewModel(uupDumpService, msDownloadService, logService);
            var mainViewModel = new MainViewModel(
                dismService, registryService, presetService, logService,
                downloadViewModel, isoService, serviceManager, imageToolsService,
                unattendService, customizationService);

            var mainWindow = new MainWindow(mainViewModel);
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Startup failed:\n\n{ex}\n\nInner: {ex.InnerException}",
                "TrimKit - Fatal Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}
