using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TrimKit.ViewModels;

namespace TrimKit;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyDarkTitleBar();
        ApplyMicaBackdrop();
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
