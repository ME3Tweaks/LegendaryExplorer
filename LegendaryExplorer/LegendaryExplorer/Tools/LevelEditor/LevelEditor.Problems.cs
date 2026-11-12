using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>The Problems tab: a scan of the open levels (<see cref="LevelScan"/>), run when the user asks, listing what it found by check</summary>
public partial class LevelEditor
{
    public ObservableCollectionExtended<LevelProblem> Problems { get; } = [];

    private ListCollectionView problemsView;
    /// <summary><see cref="Problems"/> grouped by check, in <see cref="LevelCheck"/>'s order, problems before warnings</summary>
    public ListCollectionView ProblemsView
    {
        get
        {
            if (problemsView is null)
            {
                problemsView = new ListCollectionView(Problems);
                problemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LevelProblem.CheckTitle)));
                problemsView.SortDescriptions.Add(new SortDescription(nameof(LevelProblem.Check), ListSortDirection.Ascending));
                problemsView.SortDescriptions.Add(new SortDescription(nameof(LevelProblem.Severity), ListSortDirection.Ascending));
            }
            return problemsView;
        }
    }

    public ICommand ScanLevelCommand => scanLevelCommand ??= new GenericCommand(ScanLevel);
    private ICommand scanLevelCommand;

    private string scanSummary = "Scan the open levels for problems the game would have with them.";
    public string ScanSummary
    {
        get => scanSummary;
        private set => SetProperty(ref scanSummary, value);
    }

    private string scanStatus;
    /// <summary>What the scan is doing while it searches the game's files; null when it isn't</summary>
    public string ScanStatus
    {
        get => scanStatus;
        private set => SetProperty(ref scanStatus, value);
    }

    private bool scanIsStale;
    /// <summary>Whether the levels may have changed since the scan: an edit, undo or redo, a commit, another tool's change, or a file opened or closed</summary>
    public bool ScanIsStale
    {
        get => scanIsStale;
        private set => SetProperty(ref scanIsStale, value);
    }

    private bool hasScanned;
    private CancellationTokenSource scanCancellation;
    //the last scan's end, after its copies are let go: a new one waits for it, so that two scans' copies aren't held at once
    private Task scanEnded = Task.CompletedTask;

    private void MarkScanStale()
    {
        if (hasScanned) ScanIsStale = true;
    }

    /// <summary>The open files changed: what was found in a file that's closed goes (it would keep the file's package and views alive)</summary>
    private void OnScannedFilesChanged()
    {
        MarkScanStale();
        if (Problems.Any(p => !OpenFiles.Any(f => f.Package == p.Package)))
        {
            if (SelectedProblem is { } selected && !OpenFiles.Any(f => f.Package == selected.Package)) SelectedProblem = null;
            Problems.ReplaceAll(Problems.Where(p => OpenFiles.Any(f => f.Package == p.Package)).ToList());
        }
    }

    /// <summary>
    /// The scan in steps: the packages are serialized (on the UI thread, and not while one is being saved), the copies read and checked on a worker
    /// thread, then the views' checks run on the UI thread, and last the search of the game's files for missing assets on a worker thread. A scan started
    /// meanwhile, or closing the window, cancels it
    /// </summary>
    private async void ScanLevel()
    {
        scanCancellation?.Cancel();
        scanCancellation?.Dispose();
        var cancellation = scanCancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        bool Current() => cancellation == scanCancellation && !token.IsCancellationRequested;
        SelectedProblem = null;
        hasScanned = true;
        ScanIsStale = false;
        Problems.Clear();
        List<LevelScan.LevelCopy> copies = [];
        var previous = scanEnded;
        var ended = new TaskCompletionSource();
        scanEnded = ended.Task;
        try
        {
            ScanStatus = "Copying the levels…";
            ScanSummary = "Scanning…";
            // The cancelled scan stops at its next check of its token
            await previous;
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            if (!Current()) return;
            // A save writes the offsets that copying a package does
            if (SavingFiles.Count > 0)
            {
                ScanSummary = "A level is being saved. Scan again once it's saved.";
                return;
            }
            copies = LevelScan.Copy(OpenFiles.ToList());
            ScanStatus = "Checking the levels' packages…";
            var packageChecks = await Task.Run(() =>
            {
                LevelScan.Open(copies, token);
                return LevelScan.CheckCopies(copies, token);
            });
            if (!Current()) return;
            var files = OpenFiles.Where(f => copies.Any(c => c.File == f)).ToList();
            ScanStatus = "Checking lighting and decals…";
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            if (!Current()) return;
            var views = await LevelScan.CheckViewsAsync(files, RenderContext, packageChecks.Records,
                async () => await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background), token);
            if (!Current()) return;
            // A file closed while the views were checked has no rows
            Problems.ReplaceAll(views.Where(p => OpenFiles.Any(f => f.Package == p.Package)).Concat(LevelScan.ToProblems(packageChecks.Findings, OpenFiles.ToList())));
            ScanSummary = Summary(searching: true);
            // Missing assets: the game's files are searched last, after the rest is listed
            var progress = new Progress<(int Done, int Total)>(p =>
            {
                if (Current()) ScanStatus = $"Looking for imported objects in the game's files: {p.Done} of {p.Total}…";
            });
            ScanStatus = "Looking for imported objects in the game's files…";
            var missing = await Task.Run(() => LevelScan.FindMissingAssets(copies, progress, token));
            if (!Current()) return;
            Problems.AddRange(LevelScan.ToProblems(missing, OpenFiles.ToList()));
            ScanSummary = Summary(searching: false);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (Current()) ScanSummary = $"The scan couldn't finish: {e.Message}" + (Problems.Count > 0 ? $" {Summary(searching: true)}" : "");
        }
        finally
        {
            if (Current()) ScanStatus = null;
            foreach (var copy in copies) copy.Dispose();
            ended.SetResult();
        }
    }

    private string Summary(bool searching)
    {
        int problems = Problems.Count(p => !p.IsWarning), warnings = Problems.Count - problems;
        string found = problems + warnings == 0 ? "Nothing found" : $"{Count(problems, "problem")}, {Count(warnings, "warning")}";
        return found + (searching ? " so far." : ".");
        static string Count(int n, string what) => $"{n} {what}{(n == 1 ? "" : "s")}";
    }

    private LevelProblem selectedProblem;
    /// <summary>Choosing one selects its actor and shows it</summary>
    public LevelProblem SelectedProblem
    {
        get => selectedProblem;
        set
        {
            if (!SetProperty(ref selectedProblem, value) || value is null) return;
            // Its view may have been rebuilt since the scan
            var actor = value.Actor is { IsDisposed: false } shown && Actors.Contains(shown) ? shown
                : value.Export is { } export ? Actors.FirstOrDefault(a => a.Export == export) : null;
            if (actor is null) return;
            SelectAndRevealActor(actor, false);
            if (actor.HasSpatialRepresentation) FocusOnBounds(actor.GetBounds());
        }
    }
}
