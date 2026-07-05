namespace LocalScribe.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickMediaFilesAsync();

    Task<string?> PickFolderAsync();

    Task<string?> PickPythonExecutableAsync();

    Task<string?> PickSaveFileAsync(string suggestedFileName, string extension);
}