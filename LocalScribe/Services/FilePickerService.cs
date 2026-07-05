using LocalScribe.Helpers;
using Microsoft.Extensions.Logging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LocalScribe.Services;

public sealed class FilePickerService : IFilePickerService
{
    private readonly IWindowHandleProvider _windowHandleProvider;
    private readonly ILogger<FilePickerService> _logger;

    public FilePickerService(IWindowHandleProvider windowHandleProvider, ILogger<FilePickerService> logger)
    {
        _windowHandleProvider = windowHandleProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> PickMediaFilesAsync()
    {
        if (_windowHandleProvider.WindowHandle == IntPtr.Zero)
        {
            _logger.LogWarning("File picker requested before window handle was initialized.");
            return Array.Empty<string>();
        }

        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.MusicLibrary
        };

        foreach (var extension in MediaFileTypes.DisplayExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        InitializeWithWindow.Initialize(picker, _windowHandleProvider.WindowHandle);

        var files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
        {
            return Array.Empty<string>();
        }

        var paths = files
            .Select(file => file.Path)
            .Where(path => MediaFileTypes.IsAllowed(path))
            .ToList();

        _logger.LogInformation("Selected {Count} media files from picker.", paths.Count);
        return paths;
    }

    public async Task<string?> PickFolderAsync()
    {
        if (_windowHandleProvider.WindowHandle == IntPtr.Zero)
        {
            _logger.LogWarning("Folder picker requested before window handle was initialized.");
            return null;
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };

        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _windowHandleProvider.WindowHandle);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return null;
        }

        _logger.LogInformation("Selected output folder: {Path}", folder.Path);
        return folder.Path;
    }

    public async Task<string?> PickPythonExecutableAsync()
    {
        if (_windowHandleProvider.WindowHandle == IntPtr.Zero)
        {
            _logger.LogWarning("Python picker requested before window handle was initialized.");
            return null;
        }

        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };

        picker.FileTypeFilter.Add(".exe");
        InitializeWithWindow.Initialize(picker, _windowHandleProvider.WindowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        _logger.LogInformation("Selected Python executable: {Path}", file.Path);
        return file.Path;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedFileName, string extension)
    {
        if (_windowHandleProvider.WindowHandle == IntPtr.Zero)
        {
            _logger.LogWarning("Save picker requested before window handle was initialized.");
            return null;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        picker.FileTypeChoices.Add("Text file", [extension]);
        InitializeWithWindow.Initialize(picker, _windowHandleProvider.WindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        _logger.LogInformation("Selected save path: {Path}", file.Path);
        return file.Path;
    }
}