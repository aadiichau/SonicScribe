using System.Diagnostics;
using System.Text;

namespace LocalScribe.Helpers;

public sealed class StreamingProcessHost : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly Task _stdoutPump;
    private readonly CancellationTokenSource _lifetimeCts = new();

    private StreamingProcessHost(Process process, StreamWriter stdin, Task stdoutPump)
    {
        _process = process;
        _stdin = stdin;
        _stdoutPump = stdoutPump;
    }

    public bool HasExited => _process.HasExited;

    public static Task<StreamingProcessHost> StartAsync(
        string executablePath,
        string arguments,
        Func<string, Task> onStdoutLine,
        Action<string>? onStderrLine = null,
        CancellationToken cancellationToken = default)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };
        ProcessRunner.ApplyPythonUtf8Environment(process.StartInfo);

        process.Start();
        var stdin = process.StandardInput;
        stdin.AutoFlush = true;

        if (onStderrLine is not null)
        {
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    onStderrLine(args.Data);
                }
            };
            process.BeginErrorReadLine();
        }

        var stdoutPump = PumpStdoutAsync(process, onStdoutLine, cancellationToken);
        return Task.FromResult(new StreamingProcessHost(process, stdin, stdoutPump));
    }

    public async Task SendCommandAsync(object command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var json = System.Text.Json.JsonSerializer.Serialize(command);
        await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
    }

    public async Task StopAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                await SendCommandAsync(new { cmd = "shutdown" });
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _process.WaitForExitAsync(timeoutCts.Token);
            }
        }
        catch
        {
            // Ignore shutdown write failures.
        }

        KillProcessTree();
        _lifetimeCts.Cancel();

        try
        {
            await _stdoutPump;
        }
        catch
        {
            // Pump ends when the process exits.
        }
    }

    public void Kill()
    {
        KillProcessTree();
        _lifetimeCts.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifetimeCts.Dispose();
        _stdin.Dispose();
        _process.Dispose();
    }

    private static async Task PumpStdoutAsync(
        Process process,
        Func<string, Task> onStdoutLine,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!process.HasExited && !cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                await onStdoutLine(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation.
        }
    }

    private void KillProcessTree()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}