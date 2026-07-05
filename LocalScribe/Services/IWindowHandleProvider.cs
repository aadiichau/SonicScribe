namespace LocalScribe.Services;

public interface IWindowHandleProvider
{
    IntPtr WindowHandle { get; set; }
}