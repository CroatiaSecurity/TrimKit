using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimKit.Models;
using TrimKit.Services;

namespace TrimKit.ViewModels;

public partial class DownloadViewModel : ObservableObject
{
    private readonly IUupDumpService _uupDumpService;
    private readonly IMicrosoftDownloadService _msDownloadService;
    private readonly ILogService _logService;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Select a source and search for builds";
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private WindowsBuild? _selectedBuild;
    [ObservableProperty] private WindowsEdition? _selectedEdition;
    [ObservableProperty] private WindowsLanguage? _selectedLanguage;
    [ObservableProperty] private string _outputDirectory = string.Empty;
    [ObservableProperty] private IsoSource _selectedSource = IsoSource.UupDump;
    [ObservableProperty] private bool _skipCumulativeUpdate = true;
    [ObservableProperty] private MicrosoftProduct? _selectedMsProduct;
    [ObservableProperty] private DownloadLink? _selectedDownloadLink;

    public ObservableCollection<WindowsBuild> Builds { get; } = [];
    public ObservableCollection<WindowsEdition> Editions { get; } = [];
    public ObservableCollection<WindowsLanguage> Languages { get; } = [];
    public ObservableCollection<MicrosoftProduct> MsProducts { get; } = [];
    public ObservableCollection<DownloadLink> DownloadLinks { get; } = [];

    private CancellationTokenSource? _cts;

    public DownloadViewModel(IUupDumpService uupDumpService, IMicrosoftDownloadService msDownloadService, ILogService logService)
    {
        _uupDumpService = uupDumpService;
        _msDownloadService = msDownloadService;
        _logService = logService;

        OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TrimKit", "Downloads");

        // Load MS products
        _ = LoadMsProductsAsync();
    }

    private async Task LoadMsProductsAsync()
    {
        var products = await _msDownloadService.GetAvailableProductsAsync();
        foreach (var p in products)
            MsProducts.Add(p);
    }

    [RelayCommand]
    private async Task SearchBuildsAsync()
    {
        if (SelectedSource != IsoSource.UupDump)
            return;

        try
        {
            IsBusy = true;
            StatusText = "Searching...";
            Builds.Clear();

            var builds = string.IsNullOrWhiteSpace(SearchQuery)
                ? await _uupDumpService.GetLatestBuildsAsync()
                : await _uupDumpService.SearchBuildsAsync(SearchQuery);

            foreach (var b in builds)
                Builds.Add(b);

            StatusText = $"Found {builds.Count} build(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Search failed: {ex.Message}";
            _logService.Log(Models.LogLevel.Error, $"UUP dump search failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadBuildDetailsAsync()
    {
        if (SelectedBuild == null)
            return;

        try
        {
            IsBusy = true;
            StatusText = $"Loading details for: {SelectedBuild.Id}...";

            Editions.Clear();
            Languages.Clear();

            var editions = await _uupDumpService.GetEditionsAsync(SelectedBuild.Id);
            foreach (var e in editions)
                Editions.Add(e);

            var languages = await _uupDumpService.GetLanguagesAsync(SelectedBuild.Id);
            foreach (var l in languages)
                Languages.Add(l);

            StatusText = $"Loaded {Editions.Count} edition(s), {Languages.Count} language(s). Select and download.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
            _logService.Log(Models.LogLevel.Error, $"Build details failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (SelectedBuild == null)
        {
            StatusText = "Please select a build first";
            return;
        }

        var edition = SelectedEdition?.EditionId ?? "professional";
        var language = SelectedLanguage?.LangCode ?? "en-us";

        // Open the UUP dump download page for this build — user gets the full converter package
        var url = $"https://uupdump.net/get.php?id={SelectedBuild.Id}&pack={language}&edition={edition}";

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        StatusText = $"UUP dump page opened — click 'Create download package' on the website, then run the script to build your ISO";
        _logService.Log(Models.LogLevel.Info, $"Opened: {url}");
    }

    [RelayCommand]
    private async Task DownloadDirectAsync()
    {
        // Ask user where to save
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Official Windows ISO as",
            Filter = "ISO Image (*.iso)|*.iso",
            FileName = "Windows_Latest.iso"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        try
        {
            IsBusy = true;
            _cts = new CancellationTokenSource();
            StatusText = "Getting direct download link from Microsoft...";

            var progress = new Progress<(int percent, string status)>(p =>
            {
                ProgressValue = p.percent;
                StatusText = p.status;
            });

            // Use the language from the combobox if selected, otherwise default to en-us
            var lang = SelectedLanguage?.LangCode ?? "en-us";

            await _msDownloadService.DownloadIsoAsync(
                lang,
                saveDialog.FileName,
                progress,
                _cts.Token);

            StatusText = $"ISO downloaded: {saveDialog.FileName}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Download cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Direct download not available: {ex.Message}. Use UUP dump instead.";
            _logService.Log(Models.LogLevel.Error, $"Direct download failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private async Task GetMsDownloadLinksAsync()
    {
        if (SelectedMsProduct == null || SelectedLanguage == null)
        {
            StatusText = "Please select a product and language";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Getting download links from Microsoft...";
            DownloadLinks.Clear();

            var links = await _msDownloadService.GetDownloadLinksAsync(
                SelectedMsProduct.ProductId,
                SelectedLanguage.LangCode,
                SelectedMsProduct.SessionId);

            foreach (var link in links)
                DownloadLinks.Add(link);

            StatusText = links.Count > 0
                ? $"Found {links.Count} download link(s)"
                : "No direct links found. Microsoft may require browser-based download. Try UUP dump instead.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadMsLanguagesAsync()
    {
        if (SelectedMsProduct == null)
            return;

        try
        {
            IsBusy = true;
            Languages.Clear();

            var languages = await _msDownloadService.GetProductLanguagesAsync(
                SelectedMsProduct.ProductId, SelectedMsProduct.SessionId);

            foreach (var l in languages)
                Languages.Add(l);

            StatusText = $"Found {Languages.Count} language(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenMicrosoftPage()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://www.microsoft.com/software-download/windows11",
            UseShellExecute = true
        });
        StatusText = "Microsoft download page opened in browser";
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Download Output Directory",
            UseDescriptionForTitle = true,
            SelectedPath = OutputDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputDirectory = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// Gets the path to the downloaded/extracted WIM file for use by the main image tab.
    /// </summary>
    public string? GetWimFilePath()
    {
        var wimPath = Path.Combine(OutputDirectory, "install.wim");
        return File.Exists(wimPath) ? wimPath : null;
    }
}
