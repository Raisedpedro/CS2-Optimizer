using System.Diagnostics;
using System.Text;

namespace CS2Optimizer.Core.Services;

public sealed record ProcessResult(bool Started, int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Executa ferramentas do Windows sem janela, capturando saída/erro e código de saída.
/// </summary>
public sealed class ExternalProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string label,
        Action<string>? onOutputLine = null,
        Action<string>? onStatus = null,
        CancellationToken cancellationToken = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Environment.SystemDirectory
        };

        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

        Process process;
        try
        {
            process = new Process { StartInfo = psi };
            if (!process.Start())
                return new ProcessResult(false, -1, "", "Não foi possível iniciar o processo.");
        }
        catch (Exception ex)
        {
            return new ProcessResult(false, -1, "", ex.Message);
        }

        using (process)
        {
            var started = Stopwatch.StartNew();
            var outTask = ReadAsync(process.StandardOutput, stdout, onOutputLine);
            var errTask = ReadAsync(process.StandardError, stderr, line =>
            {
                if (!string.IsNullOrWhiteSpace(line)) onOutputLine?.Invoke("[!] " + line);
            });

            var statusTask = Task.Run(async () =>
            {
                while (!process.HasExited)
                {
                    if (onStatus is not null)
                    {
                        var elapsed = started.Elapsed;
                        onStatus($"{label} - {elapsed:mm\\:ss}");
                    }
                    try { await Task.Delay(500, cancellationToken); }
                    catch (OperationCanceledException) { break; }
                }
            }, cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
                await Task.WhenAll(outTask, errTask);
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            finally
            {
                try { await statusTask; } catch { }
            }

            return new ProcessResult(true, process.ExitCode, stdout.ToString(), stderr.ToString());
        }
    }

    private static async Task ReadAsync(StreamReader reader, StringBuilder sink, Action<string>? onLine)
    {
        var buffer = new char[4096];
        var pending = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read == 0) break;
            for (var i = 0; i < read; i++)
            {
                var ch = buffer[i];
                if (ch is '\r' or '\n')
                {
                    if (pending.Length > 0)
                    {
                        var line = pending.ToString().Trim();
                        pending.Clear();
                        if (line.Length > 0)
                        {
                            if (line.Length > 200) line = line[..200] + "...";
                            sink.AppendLine(line);
                            onLine?.Invoke(line);
                        }
                    }
                }
                else
                {
                    pending.Append(ch);
                }
            }
        }

        if (pending.Length > 0)
        {
            var line = pending.ToString().Trim();
            if (line.Length > 0)
            {
                if (line.Length > 200) line = line[..200] + "...";
                sink.AppendLine(line);
                onLine?.Invoke(line);
            }
        }
    }

    public static string? FindOnPath(string fileName)
    {
        if (File.Exists(fileName)) return fileName;

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in paths)
        {
            try
            {
                var candidate = System.IO.Path.Combine(dir.Trim(), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }

        var system32 = System.IO.Path.Combine(Environment.SystemDirectory, fileName);
        return File.Exists(system32) ? system32 : null;
    }
}
