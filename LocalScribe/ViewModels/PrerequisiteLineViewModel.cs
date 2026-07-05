using CommunityToolkit.Mvvm.ComponentModel;
using LocalScribe.Models;

namespace LocalScribe.ViewModels;

public partial class PrerequisiteLineViewModel : ObservableObject
{
    public PrerequisiteLineViewModel(PrerequisiteItemStatus status)
    {
        Name = status.Name;
        Description = status.Description;
        Apply(status);
    }

    public string Name { get; }

    public string Description { get; }

    [ObservableProperty]
    private string _statusText = "Checking...";

    [ObservableProperty]
    private string _statusGlyph = "\uE823";

    [ObservableProperty]
    private bool _isReady;

    public void Apply(PrerequisiteItemStatus status)
    {
        IsReady = status.IsReady;
        StatusText = status.IsReady ? $"Ready — {status.Detail}" : $"Missing — {status.Detail}";
        StatusGlyph = status.IsReady ? "\uE73E" : "\uE783";
    }
}