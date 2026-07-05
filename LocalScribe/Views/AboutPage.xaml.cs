using LocalScribe.Helpers;
using LocalScribe.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LocalScribe.Views;

public sealed partial class AboutPage : Page
{
    public AboutViewModel ViewModel { get; }

    public AboutPage()
    {
        ViewModel = App.Services.GetRequiredService<AboutViewModel>();
        InitializeComponent();
        Loaded += AboutPage_Loaded;
    }

    private async void AboutPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AppLogoImage.Source = AssetPathHelper.CreateBitmap("Assets/StoreLogo.png", decodePixelWidth: 224);
        await ViewModel.InitializeAsync();
    }
}