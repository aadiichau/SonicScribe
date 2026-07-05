using LocalScribe.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalScribe.Services;

public sealed class UpdatePromptService
{
    private readonly IAppUpdateService _appUpdateService;

    public UpdatePromptService(IAppUpdateService appUpdateService)
    {
        _appUpdateService = appUpdateService;
    }

    public Task<bool> PromptAndApplyAsync(Window window, UpdateCheckResult result) =>
        ApplyUpdateAsync(window, result);

    public async Task<bool> ApplyUpdateAsync(Window window, UpdateCheckResult result)
    {
        if (window.Content?.XamlRoot is null || !result.IsUpdateAvailable)
        {
            return false;
        }

        var progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Maximum = 100,
            Width = 360
        };

        var statusText = new TextBlock
        {
            Text = "Starting download...",
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var progressPanel = new StackPanel { Spacing = 12 };
        progressPanel.Children.Add(statusText);
        progressPanel.Children.Add(progressBar);

        var progressDialog = new ContentDialog
        {
            Title = "Updating SonicScribe",
            Content = progressPanel,
            XamlRoot = window.Content.XamlRoot
        };

        var progress = new Progress<AppUpdateProgress>(update =>
        {
            window.DispatcherQueue.TryEnqueue(() =>
            {
                statusText.Text = update.Message;
                if (update.Percent is >= 0 and <= 100)
                {
                    progressBar.IsIndeterminate = false;
                    progressBar.Value = update.Percent.Value;
                }
                else
                {
                    progressBar.IsIndeterminate = true;
                }
            });
        });

        var applyTask = _appUpdateService.DownloadAndApplyAsync(result, progress);
        var dialogTask = progressDialog.ShowAsync();
        var applyResult = await applyTask;

        try
        {
            progressDialog.Hide();
        }
        catch
        {
            // Dialog may already be closed.
        }

        try
        {
            await dialogTask;
        }
        catch
        {
            // Hide() completes the dialog task.
        }

        if (!applyResult.Success)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Update failed",
                Content = applyResult.ErrorMessage ?? "The update could not be completed.",
                CloseButtonText = "Close",
                XamlRoot = window.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
            return false;
        }

        if (applyResult.RestartScheduled)
        {
            await App.ShutdownAsync();
            Application.Current.Exit();
        }

        return true;
    }
}