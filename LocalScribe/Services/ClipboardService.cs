using Windows.ApplicationModel.DataTransfer;

namespace LocalScribe.Services;

public sealed class ClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        return Task.CompletedTask;
    }
}