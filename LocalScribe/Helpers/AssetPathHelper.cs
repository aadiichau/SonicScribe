using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LocalScribe.Helpers;

public static class AssetPathHelper
{
    public static string GetAssetPath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalized));
    }

    public static Uri GetAssetUri(string relativePath)
    {
        return new Uri(GetAssetPath(relativePath));
    }

    public static bool AssetExists(string relativePath) => File.Exists(GetAssetPath(relativePath));

    public static BitmapImage CreateBitmap(string relativePath, int? decodePixelWidth = null)
    {
        var image = new BitmapImage
        {
            UriSource = GetAssetUri(relativePath),
        };

        if (decodePixelWidth is > 0)
        {
            image.DecodePixelWidth = decodePixelWidth.Value;
        }

        return image;
    }

    public static ImageSource CreateImageSource(string relativePath, int? decodePixelWidth = null) =>
        CreateBitmap(relativePath, decodePixelWidth);
}