namespace LocalScribe.Services;

public interface IShellService
{
    void OpenFile(string path);

    void OpenFolder(string path);

    void OpenUrl(string url);
}