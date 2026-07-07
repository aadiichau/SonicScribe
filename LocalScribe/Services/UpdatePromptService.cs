using LocalScribe.Helpers;
using LocalScribe.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalScribe.Services;

public sealed class UpdatePromptService
{
    private readonly IAppUpdateService _appUpdateService;
    private readonly IWindowHandleProvider _windowContext;

    public UpdatePromptService(
        IAppUpdateService appUpdateService,
        IWindowHandleProvider windowContext)
    {
        _appUpdateService = appUpdateService;
        _windowContext = windowContext;
    }

    public Task<bool> PromptAndApplyAsync(UpdateCheckResult result, CancellationToken cancellationToken = default) =>
        ApplyUpdateAsync(result, cancellationToken);

    public async Task<bool> ApplyUpdateAsync(
        UpdateCheckResult result,
        CancellationToken cancellationToken = default)
    {
        var xamlRoot = _windowContext.XamlRoot;
        if (xamlRoot is null)
        {
            return false;
        }

        if (!result.IsUpdateAvailable)
        {
            var unavailableDialog = new ContentDialog
            {
                Title = "No update available",
                Content = $"You are already on SonicScribe v{result.CurrentVersion}.",
                CloseButtonText = "Close",
                XamlRoot = xamlRoot
            };
            await unavailableDialog.ShowAsync();
            return false;
        }

        using var dialogCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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
            CloseButtonText = "Cancel",
            XamlRoot = xamlRoot
        };

        progressDialog.CloseButtonClick += (_, _) => dialogCts.Cancel();

        var progress = new Progress<AppUpdateProgress>(update =>
        {
            UiDispatcher.Invoke(() =>
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

        var applyTask = _appUpdateService.DownloadAndApplyAsync(result, progress, dialogCts.Token);
        var dialogTask = progressDialog.ShowAsync();
        AppUpdateResult applyResult;

        try
        {
            applyResult = await applyTask;
        }
        catch (OperationCanceledException)
        {
            applyResult = new AppUpdateResult { ErrorMessage = "Update cancelled." };
        }

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
            if (applyResult.ErrorMessage is "Update cancelled.")
            {
                return false;
            }

            var errorDialog = new ContentDialog
            {
                Title = "Update failed",
                Content = applyResult.ErrorMessage ?? "The update could not be completed.",
                CloseButtonText = "Close",
                XamlRoot = xamlRoot
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