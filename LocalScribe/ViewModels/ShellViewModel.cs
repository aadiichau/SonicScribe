using CommunityToolkit.Mvvm.ComponentModel;
using LocalScribe.Core.Navigation;
using LocalScribe.Services;
using Microsoft.UI.Xaml;

namespace LocalScribe.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IDeviceDetectionService _deviceDetectionService;

    [ObservableProperty]
    private string _gpuLabel = "Detecting GPU...";

    [ObservableProperty]
    private bool _isGpuAccelerated;

    [ObservableProperty]
    private string _selectedNavigationTag = Core.Navigation.NavigationTag.Transcribe;

    public ShellViewModel(IDeviceDetectionService deviceDetectionService)
    {
        _deviceDetectionService = deviceDetectionService;
        _deviceDetectionService.DeviceChanged += OnDeviceChanged;
    }

    public async Task InitializeAsync()
    {
        var device = await _deviceDetectionService.DetectAsync();
        ApplyDeviceInfo(device);
    }

    private void OnDeviceChanged(object? sender, DeviceInfo info) => ApplyDeviceInfo(info);

    private void ApplyDeviceInfo(DeviceInfo info)
    {
        GpuLabel = info.DisplayName;
        IsGpuAccelerated = info.IsCudaAvailable;
        NotifyGpuVisibility();
    }

    public Visibility GpuAcceleratedVisibility =>
        IsGpuAccelerated ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GpuNotAcceleratedVisibility =>
        IsGpuAccelerated ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GpuBadgeVisibility =>
        string.Equals(SelectedNavigationTag, NavigationTag.Transcribe, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    partial void OnIsGpuAcceleratedChanged(bool value) => NotifyGpuVisibility();

    partial void OnSelectedNavigationTagChanged(string value) => NotifyGpuVisibility();

    private void NotifyGpuVisibility()
    {
        OnPropertyChanged(nameof(GpuAcceleratedVisibility));
        OnPropertyChanged(nameof(GpuNotAcceleratedVisibility));
        OnPropertyChanged(nameof(GpuBadgeVisibility));
    }
}