using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Misc;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace LegendaryExplorer.Tools.LightmassBaker;

public class BakeFileItem : NotifyPropertyChangedBase
{
    public string Path { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path);

    private bool editable;
    public bool Editable
    {
        get => editable;
        set => SetProperty(ref editable, value);
    }

    public BakeFileItem(string path, bool editable)
    {
        Path = path;
        this.editable = editable;
    }
}

/// <summary>
/// Builds a level's static lighting with Lightmass (<see cref="LightmassBakeJob"/>), separately from the Level Editor so a bake
/// doesn't share the machine with a renderer. The files are opened from disk and the baked ones saved.
/// </summary>
public partial class LightmassBakerWindow : TrackingNotifyPropertyChangedWindowBase
{
    public ObservableCollectionExtended<BakeFileItem> Files { get; } = [];

    public IReadOnlyList<LightmassQuality> Qualities { get; } = Enum.GetValues<LightmassQuality>();

    private LightmassQuality quality = LightmassQuality.Preview;
    public LightmassQuality Quality { get => quality; set => SetProperty(ref quality, value); }

    private string tfcName = "Textures_DLC_MOD_LEXLightmaps";
    public string TfcName { get => tfcName; set { if (SetProperty(ref tfcName, value)) ScheduleValidation(); } }

    private bool saveInPlace = true;
    public bool SaveInPlace
    {
        get => saveInPlace;
        set
        {
            if (SetProperty(ref saveInPlace, value))
            {
                OnPropertyChanged(nameof(SaveToFolder));
                ScheduleValidation();
            }
        }
    }
    public bool SaveToFolder => !SaveInPlace;

    private string outputDirectory;
    public string OutputDirectory { get => outputDirectory; set { if (SetProperty(ref outputDirectory, value)) ScheduleValidation(); } }

    private string udkBinaries = LightmassBakeJob.DefaultUdkBinaries;
    public string UdkBinaries { get => udkBinaries; set { if (SetProperty(ref udkBinaries, value)) ScheduleValidation(); } }

    private string problems = "";
    public string Problems { get => problems; private set => SetProperty(ref problems, value); }

    private string warnings = "";
    public string Warnings { get => warnings; private set => SetProperty(ref warnings, value); }

    private string status = "";
    public string Status { get => status; private set => SetProperty(ref status, value); }

    private string bakeState = "Ready";
    public string BakeState { get => bakeState; private set => SetProperty(ref bakeState, value); }

    private double bakeProgress;
    public double BakeProgress { get => bakeProgress; private set => SetProperty(ref bakeProgress, value); }

    private bool isBaking;
    public bool IsBaking
    {
        get => isBaking;
        private set
        {
            if (SetProperty(ref isBaking, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(CanBake));
            }
        }
    }
    public bool IsIdle => !IsBaking;

    private bool hasProblems = true;
    public bool CanBake => !IsBaking && !hasProblems;

    private CancellationTokenSource cancellation;

    /// <summary>The files whose Bake checkbox is watched. Kept apart from <see cref="Files"/>, whose Reset events don't say what was removed.</summary>
    private readonly HashSet<BakeFileItem> watchedFiles = [];

    /// <summary>Validation reads the disk (the files, SwarmAgent.exe's version), so typing and adding files only schedule it.</summary>
    private readonly DispatcherTimer validationTimer;

    public LightmassBakerWindow() : base("Lightmass Baker", true)
    {
        DataContext = this;
        validationTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
        validationTimer.Tick += (_, _) => Validate();
        Files.CollectionChanged += (_, _) =>
        {
            foreach (var item in watchedFiles.Except(Files).ToList())
            {
                item.PropertyChanged -= File_PropertyChanged;
                watchedFiles.Remove(item);
            }
            foreach (var item in Files.Where(watchedFiles.Add)) item.PropertyChanged += File_PropertyChanged;
            ScheduleValidation();
        };
        InitializeComponent();
        Validate();
    }

    /// <summary>A window with these files in the list, e.g. the Level Editor's (whose read-only files only shadow).</summary>
    public LightmassBakerWindow(IEnumerable<(string Path, bool Editable)> files) : this()
    {
        AddFiles(files);
    }

    private void File_PropertyChanged(object sender, PropertyChangedEventArgs e) => ScheduleValidation();

    private void ScheduleValidation()
    {
        validationTimer.Stop();
        validationTimer.Start();
    }

    public void AddFiles(IEnumerable<(string Path, bool Editable)> files)
    {
        var known = Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<BakeFileItem>();
        foreach (var (path, editable) in files)
        {
            string full = Path.GetFullPath(path);
            if (known.Add(full)) added.Add(new BakeFileItem(full, editable));
        }
        if (added.Count > 0) Files.AddRange(added);
    }

    private LightmassBakeJob ToJob() => new()
    {
        Files = Files.Select(f => new LightmassBakeJobFile { Path = f.Path, Editable = f.Editable }).ToList(),
        Quality = Quality,
        TfcName = TfcName,
        UdkBinaries = UdkBinaries,
        OutputDirectory = SaveInPlace || string.IsNullOrWhiteSpace(OutputDirectory) ? null : OutputDirectory,
    };

    private void LoadJob(LightmassBakeJob job)
    {
        Files.ClearEx();
        AddFiles(job.Files.Select(f => (f.Path, f.Editable)));
        Quality = job.Quality;
        TfcName = job.TfcName;
        UdkBinaries = job.ResolvedUdkBinaries;
        OutputDirectory = job.OutputDirectory;
        SaveInPlace = string.IsNullOrWhiteSpace(job.OutputDirectory);
        BakeState = "Ready";
        BakeProgress = 0;
        Status = "";
    }

    private void Validate()
    {
        validationTimer.Stop();
        var job = ToJob();
        var found = job.Problems();
        if (string.IsNullOrWhiteSpace(UdkBinaries)) found.Add("Choose UDK's Binaries folder.");
        if (SaveToFolder && string.IsNullOrWhiteSpace(OutputDirectory)) found.Add("Choose the folder to save the baked files to.");
        hasProblems = found.Count > 0;
        Problems = string.Join("\n", found);
        Warnings = string.Join("\n", job.Warnings());
        OnPropertyChanged(nameof(CanBake));
    }

    #region File list

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = AppDirectories.GetOpenPackageDialog("Add level files");
        dialog.Multiselect = true;
        if (dialog.ShowDialog(this) == true) AddFiles(dialog.FileNames.Select(f => (f, true)));
    }

    private void AddStreamingLevels_Click(object sender, RoutedEventArgs e)
    {
        var persistent = Files.Where(f => f.FileName.StartsWith("BioP_", StringComparison.OrdinalIgnoreCase)).ToList();
        if (persistent.Count == 0)
        {
            MessageBox.Show(this, "Add a persistent level (BioP_...) first: its streaming levels are the ones added.", "Lightmass Baker");
            return;
        }
        int before = Files.Count;
        try
        {
            foreach (var p in persistent) AddFiles(LightmassBakeJob.StreamingLevels(p.Path).Select(path => (path, true)));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't read the streaming levels:\n{ex.Message}", "Lightmass Baker", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        Status = $"Added {Files.Count - before} streaming levels.";
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in FilesGrid.SelectedItems.OfType<BakeFileItem>().ToList()) Files.Remove(item);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => Files.ClearEx();

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        bool ok = !IsBaking && e.Data.GetDataPresent(DataFormats.FileDrop)
                  && ((string[])e.Data.GetData(DataFormats.FileDrop)).Any(f => Path.GetExtension(f).Equals(".pcc", StringComparison.OrdinalIgnoreCase));
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (IsBaking || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        AddFiles(files.Where(f => Path.GetExtension(f).Equals(".pcc", StringComparison.OrdinalIgnoreCase)).Select(f => (f, true)));
    }

    #endregion

    #region Settings and jobs

    private string PickFolder(string title, string initial)
    {
        var dialog = new CommonOpenFileDialog(title) { IsFolderPicker = true, InitialDirectory = initial };
        return dialog.ShowDialog(this) == CommonFileDialogResult.Ok ? dialog.FileName : null;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Select the folder to save the baked files to", OutputDirectory) is { } folder) OutputDirectory = folder;
    }

    private void BrowseUdk_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Select UDK's Binaries folder (February 2015 UDK)", UdkBinaries) is { } folder) UdkBinaries = folder;
    }

    private const string JobFilter = "Lightmass bake job (*.json)|*.json";

    private void NewJob_Click(object sender, RoutedEventArgs e) => LoadJob(new LightmassBakeJob());

    private void OpenJob_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = JobFilter, Title = "Open bake job" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            LoadJob(LightmassBakeJob.Load(dialog.FileName));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't open the job:\n{ex.Message}", "Lightmass Baker", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveJob_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = JobFilter, Title = "Save bake job", FileName = Files.FirstOrDefault()?.FileName is { } first ? Path.ChangeExtension(first, ".json") : "" };
        if (dialog.ShowDialog(this) == true) ToJob().Save(dialog.FileName);
    }

    #endregion

    #region Bake

    private async void Bake_Click(object sender, RoutedEventArgs e)
    {
        Validate();
        if (!CanBake) return;
        var job = ToJob();
        if (string.IsNullOrWhiteSpace(job.OutputDirectory) && Files.Any(f => f.Editable)
            && MessageBox.Show(this, "The baked files will be saved over the originals. Continue?", "Lightmass Baker", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
        {
            return;
        }

        LogBox.Clear();
        BakeProgress = 0;
        BakeState = "Bake in progress...";
        Status = "Starting...";
        IsBaking = true;
        cancellation = new CancellationTokenSource();
        var start = DateTime.Now;
        void AppendLog(string message)
        {
            LogBox.AppendText($"[{DateTime.Now - start:hh\\:mm\\:ss}] {message}\n");
            LogBox.ScrollToEnd();
            if (IsBaking && !cancellation.IsCancellationRequested) Status = message;
        }
        void Log(string message)
        {
            if (Dispatcher.CheckAccess()) AppendLog(message);
            else Dispatcher.InvokeAsync(() => AppendLog(message));
        }
        foreach (string warning in job.Warnings()) Log($"Warning: {warning}");
        try
        {
            var token = cancellation.Token;
            var result = await Task.Run(() => job.Run(Log, token));
            foreach (string line in LightmassBakeReport.Summary(result).Split('\n')) Log(line);
            Status = result.SavedFiles.Count == 0 ? "Done: nothing was saved."
                : result.Complete ? $"Done: saved {result.SavedFiles.Count} files."
                : $"Done: saved {result.SavedFiles.Count} files, but {result.FailedTasks + result.MissingMappings} lightmaps weren't baked and keep their original lighting.";
            BakeState = Status;
            BakeProgress = 100;
        }
        catch (OperationCanceledException)
        {
            Log("Cancelled. Nothing was saved.");
            Status = BakeState = "Cancelled";
        }
        catch (Exception ex)
        {
            Log($"Failed: {ex}");
            Status = $"Failed: {ex.Message}";
            BakeState = "Bake failed. See the log for details.";
        }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            IsBaking = false;
            Validate();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (cancellation is { IsCancellationRequested: false })
        {
            cancellation.Cancel();
            BakeState = "Cancelling...";
            Status = "Cancelling...";
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (IsBaking)
        {
            MessageBox.Show(this, "A bake is running. Cancel it, or wait for it to finish, before closing.", "Lightmass Baker");
            e.Cancel = true;
            return;
        }
        validationTimer.Stop();
    }

    #endregion
}
