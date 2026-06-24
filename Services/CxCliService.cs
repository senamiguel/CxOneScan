using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CxDesktopWrapper.Services;

public class CxCliService
{
    private Process? _activeProcess;
    private readonly object _processLock = new();

    public event Action<string>? OutputReceived;
    public event Action<int>? ProgressChanged;

    private static readonly Regex PercentRegex = new(@"(\d{1,3})%", RegexOptions.Compiled);

    public bool HasActiveProcess
    {
        get
        {
            lock (_processLock)
            {
                return _activeProcess != null && !_activeProcess.HasExited;
            }
        }
    }

    public void KillActiveProcess()
    {
        lock (_processLock)
        {
            if (_activeProcess == null || _activeProcess.HasExited) return;

            try
            {
                _activeProcess.Kill(entireProcessTree: true);
                RaiseOutput("[CANCELADO] Processo encerrado pelo usuário.");
            }
            catch (Exception ex)
            {
                RaiseOutput($"[ERRO] Falha ao cancelar processo: {ex.Message}");
            }
        }
    }

    public Task<bool> RunScanAsync(
        string executable,
        string arguments,
        string workingDirectory,
        string sensitiveKeyToMask,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => ExecuteProcess(executable, arguments, workingDirectory, sensitiveKeyToMask, cancellationToken), cancellationToken);
    }

    private bool ExecuteProcess(
        string executable,
        string arguments,
        string workingDirectory,
        string sensitiveKeyToMask,
        CancellationToken cancellationToken)
    {
        var process = new Process();
        process.StartInfo.FileName = executable;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        if (!string.IsNullOrEmpty(workingDirectory))
            process.StartInfo.WorkingDirectory = workingDirectory;

        process.OutputDataReceived += (_, ev) => HandleOutputLine(ev.Data, sensitiveKeyToMask, isError: false);
        process.ErrorDataReceived += (_, ev) => HandleOutputLine(ev.Data, sensitiveKeyToMask, isError: true);

        try
        {
            lock (_processLock) { _activeProcess = process; }

            string safeArgs = MaskSensitiveData(arguments, sensitiveKeyToMask);
            RaiseOutput($"> {executable} {safeArgs}");

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

    private void HandleOutputLine(string? data, string sensitiveKey, bool isError)
    {
        if (data == null) return;

        string safeData = MaskSensitiveData(data, sensitiveKey);
        RaiseOutput(isError ? $"ERROR: {safeData}" : safeData);

        var match = PercentRegex.Match(data);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
        {
            ProgressChanged?.Invoke(Math.Clamp(percent, 0, 100));
        }
    }

    private static string MaskSensitiveData(string text, string sensitiveKey)
    {
        return string.IsNullOrEmpty(sensitiveKey) ? text : text.Replace(sensitiveKey, "********");
    }

    private void RaiseOutput(string message)
    {
        OutputReceived?.Invoke(message);
    }

    public static string BuildScanArguments(
        string projectName,
        string branch,
        IEnumerable<string> scanTypes,
        string tags,
        string projectTags,
        string projectGroups,
        string tenant,
        string apiKey,
        string baseUri,
        string baseAuthUri,
        string reportOutputPath,
        bool incremental = false)
    {
        var args = new StringBuilder();
        args.Append($"scan create --project-name \"{projectName}\"");
        args.Append($" --branch \"{branch}\"");
        args.Append($" --scan-types \"{string.Join(",", scanTypes)}\"");

        if (incremental)
            args.Append(" --incremental");

        if (!string.IsNullOrWhiteSpace(tags))
            args.Append($" --tags \"{tags}\"");
        if (!string.IsNullOrWhiteSpace(projectTags))
            args.Append($" --project-tags \"{projectTags}\"");
        if (!string.IsNullOrWhiteSpace(projectGroups))
            args.Append($" --project-groups \"{projectGroups}\"");

        args.Append($" --report-format json,summaryHTML");
        args.Append($" --output-path \"{reportOutputPath}\"");

        args.Append($" --tenant \"{tenant}\"");
        args.Append($" --apikey \"{apiKey}\"");
        args.Append($" --base-uri \"{baseUri}\"");
        args.Append($" --base-auth-uri \"{baseAuthUri}\"");
        args.Append(" -s .");

        return args.ToString();
    }
}
