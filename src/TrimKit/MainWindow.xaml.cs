using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TrimKit.ViewModels;

namespace TrimKit;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _cleanupDone;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyDarkTitleBar();
        ApplyMicaBackdrop();
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_cleanupDone)
            return;

        // Always cleanup on close — no questions asked.
        // If anything is mounted or a work folder exists, clean it up (discard unsaved changes).
        if (_viewModel.IsMounted || _viewModel.IsBootMounted || _viewModel.IsInstallMounted ||
            !string.IsNullOrEmpty(_viewModel.WorkFolder))
        {
            e.Cancel = true;
            _viewModel.StatusText = "Shutting down — cleaning up...";

            await _viewModel.GracefulCleanupAsync(commitChanges: false);
            _cleanupDone = true;

            // Now actually close
            Closing -= OnClosing;
            Close();
        }
    }

    /// <summary>
    /// Synchronous cleanup for process-exit scenarios where async isn't viable.
    /// Called from App.xaml.cs ProcessExit handler.
    /// </summary>
    public void ForceCleanupSync()
    {
        if (_cleanupDone) return;
        _cleanupDone = true;
        _viewModel.ForceCleanupSync();
    }

    #region DWM Dark Title Bar + Mica (Win11 22H2+)

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMSBT_MAINWINDOW = 2; // Mica

    private void ApplyDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }
        catch
        {
            // Older Windows — silently ignore
        }
    }

    private void ApplyMicaBackdrop()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int backdrop = DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        catch
        {
            // Older Windows — falls back to flat dark background which still looks good
        }
    }

    #endregion
}
