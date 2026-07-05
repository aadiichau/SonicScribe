namespace LocalScribe.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}