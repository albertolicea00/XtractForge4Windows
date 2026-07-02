using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using XtractForge.Core.Models;

namespace XtractForge.Core.Engine;

/// <summary>A spawned child process with a merged stdout+stderr line stream.</summary>
public sealed class RunningProcess
{
    private readonly Channel<string> _lines;

    public Process Process { get; }
    /// <summary>Merged stdout + stderr, line by line (handles \n and bare \r progress redraws).</summary>
    public IAsyncEnumerable<string> Lines => _lines.Reader.ReadAllAsync();

    internal RunningProcess(Process process, Channel<string> lines)
    {
        Process = process;
        _lines = lines;
    }

    public bool IsRunning => !Process.HasExited;
    public int ExitCode => Process.ExitCode;

    /// <summary>Kill the entire process tree (cancel / Windows-style pause).</summary>
    public void Kill()
    {
        try
        {
            if (!Process.HasExited) Process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and the kill.
        }
    }

    public Task<int> WaitForExitAsync() =>
        Process.WaitForExitAsync().ContinueWith(_ => Process.ExitCode);
}

public sealed record CaptureResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;
}

public static class ProcessRunner
{
    /// <summary>
    /// Resolve a bare binary name against PATH (adding .exe/.cmd/.bat when
    /// needed). Paths containing a separator pass through unchanged.
    /// </summary>
    public static string ResolveBinary(string binary)
    {
        if (binary.Contains('\\') || binary.Contains('/'))
            return binary;

        var extensions = OperatingSystem.IsWindows()
            ? new[] { "", ".exe", ".cmd", ".bat" }
            : [""];
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in pathDirs)
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, binary + ext);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return binary;
    }

    private static ProcessStartInfo StartInfo(string binary, IEnumerable<string> args, string? workingDir)
    {
        var info = new ProcessStartInfo
        {
            FileName = ResolveBinary(binary),
            UseShellExecute = false,
            CreateNoWindow = true, // never flash console windows
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (workingDir is not null) info.WorkingDirectory = workingDir;
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    /// <summary>Spawn a long-running command, streaming merged stdout+stderr lines.</summary>
    public static RunningProcess Run(Command command, string? workingDir = null)
    {
        var process = new Process { StartInfo = StartInfo(command.Binary, command.Args, workingDir) };
        var channel = Channel.CreateUnbounded<string>();

        if (!process.Start())
            throw new DownloadException($"Could not start {command.Binary}");

        var stdout = PumpAsync(process.StandardOutput, channel.Writer);
        var stderr = PumpAsync(process.StandardError, channel.Writer);
        _ = Task.WhenAll(stdout, stderr).ContinueWith(_ => channel.Writer.TryComplete());

        return new RunningProcess(process, channel);
    }

    /// <summary>Read a stream, splitting on \n and bare \r (progress-meter redraws).</summary>
    private static async Task PumpAsync(StreamReader reader, ChannelWriter<string> writer)
    {
        var buffer = new char[4096];
        var line = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (c is '\n' or '\r')
                {
                    if (line.Length > 0)
                    {
                        await writer.WriteAsync(line.ToString());
                        line.Clear();
                    }
                }
                else
                {
                    line.Append(c);
                }
            }
        }
        if (line.Length > 0) await writer.WriteAsync(line.ToString());
    }

    /// <summary>Run a short command to completion and capture its output.</summary>
    public static async Task<CaptureResult> CaptureAsync(string binary, params string[] args)
    {
        using var process = new Process { StartInfo = StartInfo(binary, args, null) };
        try
        {
            if (!process.Start())
                return new CaptureResult(-1, "", $"Could not start {binary}");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new CaptureResult(-1, "", e.Message);
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CaptureResult(process.ExitCode, await stdout, await stderr);
    }
}
