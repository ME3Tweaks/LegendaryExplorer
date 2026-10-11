using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// Runs a <see cref="LightmassBakeJob"/> without any UI: <c>LegendaryExplorer.exe --bake-lighting job.json [--log file]</c>.
/// It starts before the WPF app (and its single-instance handoff), so it can run while LEX is open. Game and UDK paths come
/// from LEX's settings. The log goes to the console it was started from, if any, and to a file (job.json.log by default).
/// Exit codes: 0 when everything was baked and saved, 2 when it was saved but some mappings failed (they keep their original
/// lighting), 1 when nothing was saved. LEX is a Windows program, so a console doesn't wait for it: use <c>start /wait</c> in
/// cmd, or <c>Start-Process -Wait</c> in PowerShell, to get the exit code.
/// </summary>
public static class LightmassBakeCli
{
    public const string Switch = "--bake-lighting";

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public const int Saved = 0, NothingSaved = 1, SavedIncomplete = 2;

    public static bool IsInvocation(string[] args) => args.Length > 1 && args[1].Equals(Switch, StringComparison.OrdinalIgnoreCase);

    /// <param name="args">The process's arguments, as <see cref="Environment.GetCommandLineArgs"/> gives them.</param>
    public static int Run(string[] args)
    {
        bool hasConsole = AttachConsole(-1);
        string jobPath = args.Length > 2 ? Path.GetFullPath(args[2]) : null;
        if (jobPath == null || !File.Exists(jobPath))
        {
            if (hasConsole) Console.WriteLine($"Usage: LegendaryExplorer.exe {Switch} <job.json> [--log <file>]");
            return NothingSaved;
        }
        int logIndex = Array.FindIndex(args, a => a.Equals("--log", StringComparison.OrdinalIgnoreCase));
        string logPath = logIndex > 0 && logIndex + 1 < args.Length ? Path.GetFullPath(args[logIndex + 1]) : jobPath + ".log";

        using var logFile = OpenLog(logPath, hasConsole);
        if (logFile == null) return NothingSaved;
        var logLock = new object();
        var start = DateTime.Now;
        void Log(string message)
        {
            string line = $"[{DateTime.Now - start:hh\\:mm\\:ss}] {message}";
            lock (logLock)
            {
                logFile.WriteLine(line);
                if (hasConsole) Console.WriteLine(line);
            }
        }

        using var cancel = new CancellationTokenSource();
        if (hasConsole)
        {
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Log("Cancelling...");
                cancel.Cancel();
            };
        }
        try
        {
            Settings.LoadSettings();
            LegendaryExplorerCoreLib.InitLib(null, message => Log($"Save failed: {message}"));
            CoreLibSettingsBridge.MapSettingsIntoBridge();
            LE3Directory.ReloadDefaultGamePath();

            var job = LightmassBakeJob.Load(jobPath);
            foreach (string warning in job.Warnings()) Log($"Warning: {warning}");
            var result = job.Run(Log, cancel.Token);
            foreach (string line in LightmassBakeReport.Summary(result).Split('\n')) Log(line);
            return result.SavedFiles.Count == 0 ? NothingSaved : result.Complete ? Saved : SavedIncomplete;
        }
        catch (OperationCanceledException)
        {
            Log("Cancelled. Nothing was saved.");
            return NothingSaved;
        }
        catch (Exception e)
        {
            Log($"Failed: {e}");
            return NothingSaved;
        }
    }

    static StreamWriter OpenLog(string path, bool hasConsole)
    {
        try
        {
            return new StreamWriter(path, append: false) { AutoFlush = true };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (hasConsole) Console.WriteLine($"The log file {path} can't be written: {e.Message}");
            return null;
        }
    }
}
