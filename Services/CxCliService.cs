using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CxDesktopWrapper.Services;

public partial class CxCliService
{
    public event Action<string>? OutputReceived;
    public event Action<int>? ProgressChanged;

    private readonly object _processLock = new();
    private Process? _activeProcess;

    public Task<bool> RunScanAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        Dictionary<string, string>? envVars,
        CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            () => ExecuteProcess(executable, arguments, workingDirectory, envVars, cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private bool ExecuteProcess(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        Dictionary<string, string>? envVars,
        CancellationToken cancellationToken)
    {
        var process = new Process();
        process.StartInfo.FileName = executable;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        foreach (var arg in arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (envVars != null)
        {
            foreach (var kvp in envVars)
            {
                process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        string workDir = string.IsNullOrEmpty(workingDirectory) ? AppDomain.CurrentDomain.BaseDirectory : workingDirectory;
        process.StartInfo.WorkingDirectory = workDir;

        process.OutputDataReceived += (_, ev) => HandleOutputLine(ev.Data, isError: false);
        process.ErrorDataReceived += (_, ev) => HandleOutputLine(ev.Data, isError: true);

        try
        {
            lock (_processLock) { _activeProcess = process; }

            var safeCmd = $"{executable} {string.Join(" ", arguments.Select(EscapeArgument))}";
            RaiseOutput($"> {safeCmd}");

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.WaitForExit(500))
            {
                if (!cancellationToken.IsCancellationRequested) continue;
                KillActiveProcess();
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            KillActiveProcess();
            return false;
        }
        catch (Exception ex)
        {
            RaiseOutput($"Exceção ao executar o processo: {ex.Message}");
            return false;
        }
        finally
        {
            lock (_processLock) { _activeProcess = null; }
            process.Dispose();
        }
    }

    public void KillActiveProcess()
    {
        lock (_processLock)
        {
            if (_activeProcess == null) return;
            try
            {
                if (!_activeProcess.HasExited)
                {
                    _activeProcess.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                RaiseOutput($"Erro ao finalizar o processo do CLI: {ex.Message}");
            }
        }
    }

    private void HandleOutputLine(string? data, bool isError)
    {
        if (data == null) return;

        RaiseOutput(isError ? $"ERROR: {data}" : data);

        var match = PercentPattern().Match(data);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
        {
            ProgressChanged?.Invoke(Math.Clamp(percent, 0, 100));
        }
    }

    private void RaiseOutput(string message)
    {
        OutputReceived?.Invoke(message);
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        if (arg.Contains(" ") || arg.Contains("\""))
        {
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }
        return arg;
    }

    public static List<string> BuildScanArguments(
        string projectName,
        string branch,
        IEnumerable<string> scanTypes,
        string tags,
        string projectTags,
        string projectGroups,
        string reportOutputPath,
        bool incremental)
    {
        var args = new List<string>
        {
            "scan",
            "create",
            "--project-name", projectName,
            "--branch", branch,
            "--scan-types", string.Join(",", scanTypes)
        };

        if (incremental)
        {
            args.Add("--sast-incremental");
        }

        if (!string.IsNullOrWhiteSpace(tags))
        {
            args.Add("--tags");
            args.Add(tags);
        }
        if (!string.IsNullOrWhiteSpace(projectTags))
        {
            args.Add("--project-tags");
            args.Add(projectTags);
        }
        if (!string.IsNullOrWhiteSpace(projectGroups))
        {
            args.Add("--project-groups");
            args.Add(projectGroups);
        }

        args.Add("--report-format");
        args.Add("json,summaryHTML");

        args.Add("--output-path");
        args.Add(reportOutputPath);

        args.Add("-s");
        args.Add(".");

        return args;
    }

    [GeneratedRegex(@"(\d+)%")]
    private static partial Regex PercentPattern();
}
