using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Picks an asset of a kind for a file being edited, from the file's own entries and every asset of the kind in the game that the asset database lists,
/// marked with where each is, with search and a preview. The highlighted asset is opened and checked as its use asks. One in another file is chosen by
/// that file and its path (<see cref="AssetReference"/>): choosing it adds nothing to the file
/// </summary>
public partial class AssetPickerWindow : TrackingNotifyPropertyChangedWindowBase
{
    /// <summary>
    /// The asset chosen, an entry of <see cref="AssetPickRequest.Destination"/> or another file's export, or null if none was. If the game's asset database is missing or out of date,
    /// it asks to rebuild it, unless its game is in <paramref name="declinedDatabasePrompts"/>; it adds the game when that's declined
    /// </summary>
    public static AssetReference Pick(Window owner, ISet<MEGame> declinedDatabasePrompts, AssetPickRequest request)
    {
        var window = new AssetPickerWindow(request, declinedDatabasePrompts) { Owner = owner };
        return window.ShowDialog() == true ? window.chosen : null;
    }

    private const int MaxCachedPackages = 12;

    private readonly AssetPickRequest request;
    private readonly ISet<MEGame> declinedDatabasePrompts;
    private readonly AssetPreviewScene preview = new();
    private readonly DispatcherTimer searchTimer;
    private List<AssetChoice> choices = [];
    //the export previewed, to show again on another shape
    private ExportEntry previewed;
    private CancellationTokenSource selectionCancellation;
    //what the highlighted choice refers to, once it's been opened and can be used, and what was chosen
    private AssetReference usable, chosen;
    private bool closed;

    private AssetPickerWindow(AssetPickRequest request, ISet<MEGame> declinedDatabasePrompts) : base("Asset Picker", false)
    {
        this.request = request;
        this.declinedDatabasePrompts = declinedDatabasePrompts;
        PickTitle = request.Title;
        IsMaterialPick = request.Kind is AssetKind.Material or AssetKind.DecalMaterial;
        //a decal is projected onto surfaces, so its material is seen flat
        shape = request.Kind is AssetKind.DecalMaterial ? PreviewShape.Plane : PreviewShape.Sphere;
        searchTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Normal, (_, _) => ApplySearch(), Dispatcher);
        searchTimer.Stop();
        Status = $"Listing this file's {Noun(2)}…";
        DataContext = this;
        InitializeComponent();
        PreviewViewer.Context = preview.Context;
        Loaded += (_, _) =>
        {
            //the control starts with rendering off
            PreviewViewer.SetShouldRender(true);
            SearchBox.Focus();
            //once shown, since it may ask to rebuild the database
            LoadCatalog();
        };
    }

    public string PickTitle { get; }

    private string query = "";
    public string Query
    {
        get => query;
        set
        {
            if (!SetProperty(ref query, value ?? "")) return;
            searchTimer.Stop();
            searchTimer.Start();
        }
    }

    private IReadOnlyList<AssetChoice> visibleChoices = [];
    public IReadOnlyList<AssetChoice> VisibleChoices { get => visibleChoices; private set => SetProperty(ref visibleChoices, value); }

    private IReadOnlyList<object> folderRoots = [];
    /// <summary>The visible choices as a tree of folders by path, built while <see cref="ShowFolders"/></summary>
    public IReadOnlyList<object> FolderRoots { get => folderRoots; private set => SetProperty(ref folderRoots, value); }

    private bool showFolders;
    public bool ShowFolders
    {
        get => showFolders;
        set
        {
            if (!SetProperty(ref showFolders, value)) return;
            OnPropertyChanged(nameof(ShowList));
            ApplySearch();
        }
    }
    public bool ShowList { get => !showFolders; set => ShowFolders = !value; }

    public bool IsMaterialPick { get; }

    private PreviewShape shape;
    private void SetShape(PreviewShape value, bool on)
    {
        if (!on || shape == value) return;
        shape = value;
        OnPropertyChanged(nameof(ShowOnSphere));
        OnPropertyChanged(nameof(ShowOnCube));
        OnPropertyChanged(nameof(ShowOnPlane));
        if (previewed is not null && IsMaterialPick) preview.ShowMaterial(previewed, shape);
    }
    public bool ShowOnSphere { get => shape is PreviewShape.Sphere; set => SetShape(PreviewShape.Sphere, value); }
    public bool ShowOnCube { get => shape is PreviewShape.Cube; set => SetShape(PreviewShape.Cube, value); }
    public bool ShowOnPlane { get => shape is PreviewShape.Plane; set => SetShape(PreviewShape.Plane, value); }

    private AssetChoice selected;
    public AssetChoice Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value)) ShowSelected(value);
        }
    }

    private string status;
    public string Status { get => status; private set => SetProperty(ref status, value); }

    private bool canOpenDatabase;
    public bool CanOpenDatabase { get => canOpenDatabase; private set => SetProperty(ref canOpenDatabase, value); }

    private bool canUse;
    public bool CanUse { get => canUse; private set => SetProperty(ref canUse, value); }

    private string selectedName;
    public string SelectedName { get => selectedName; private set => SetProperty(ref selectedName, value); }

    private string selectedPath;
    public string SelectedPath { get => selectedPath; private set => SetProperty(ref selectedPath, value); }

    private string selectedDetails;
    public string SelectedDetails { get => selectedDetails; private set => SetProperty(ref selectedDetails, value); }

    private string selectedProblem;
    public string SelectedProblem
    {
        get => selectedProblem;
        private set
        {
            if (SetProperty(ref selectedProblem, value)) OnPropertyChanged(nameof(HasSelectedProblem));
        }
    }
    public bool HasSelectedProblem => selectedProblem is not null;

    private bool hasPreview;
    public bool HasPreview { get => hasPreview; private set => SetProperty(ref hasPreview, value); }

    private string previewNote = "Select an asset to preview it.";
    public string PreviewNote { get => previewNote; private set => SetProperty(ref previewNote, value); }

    private string Count(int count) => $"{count:N0} {Noun(count)}";

    private string Noun(int count) => request.Kind switch
        {
            AssetKind.StaticMesh => count == 1 ? "static mesh" : "static meshes",
            AssetKind.ParticleSystem => count == 1 ? "particle system" : "particle systems",
            AssetKind.DecalMaterial => count == 1 ? "decal material" : "decal materials",
            _ => count == 1 ? "material" : "materials"
        };

    private async void LoadCatalog()
    {
        MEGame game = request.Destination.Game;
        var loading = AssetIndex.Load(game);
        //listing a file's decal materials checks each one, which can open the packages its imports are in
        var cache = preview.Context.PackageCache;
        try
        {
            choices = await Task.Run(() => AssetPicking.Choices(request.Destination, request.Kind, null, cache));
        }
        catch (Exception e)
        {
            Status = $"This file's assets couldn't be listed: {e.Message}";
            return;
        }
        if (closed) return;
        ApplySearch();
        Status = $"{Count(choices.Count)} in this file. Loading the {game} asset database to list the rest of the game's…";
        AssetIndexLoad load;
        try
        {
            load = await loading;
        }
        catch (Exception e)
        {
            load = new(null, $"The {game} asset database couldn't be loaded: {e.Message}");
        }
        if (closed) return;
        int inFile = choices.Count;
        if (load.Catalog is { } catalog)
        {
            choices = await Task.Run(() => AssetPicking.Choices(request.Destination, request.Kind, catalog, cache));
            if (closed) return;
            inFile = choices.Count(c => c.Entry is not null);
            Status = $"{Count(inFile)} in this file, and {Count(choices.Count - inFile)} elsewhere in {game}, from its asset database of {catalog.GenerationDate}.";
            ApplySearch();
        }
        else
        {
            Status = $"{Count(inFile)} in this file. {load.Problem} Build it in the Asset Database window to pick from the whole game.";
            CanOpenDatabase = true;
            if (!declinedDatabasePrompts.Contains(game))
            {
                var answer = MessageBox.Show(this, $"{load.Problem}\n\nUntil it's rebuilt, asset pickers list only the file's own assets. OK opens the Asset Database window, to rebuild it there. " +
                    $"Cancel won't ask again about {game}'s database while this Level Editor is open.", "Asset database", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (answer == MessageBoxResult.OK) OpenAssetDatabase();
                else declinedDatabasePrompts.Add(game);
            }
        }
    }

    private void ApplySearch()
    {
        searchTimer.Stop();
        var current = selected;
        VisibleChoices = AssetPicking.Search(choices, query).ToList();
        //a search narrowed to a few is shown with its folders open
        FolderRoots = showFolders ? AssetFolder.Build(VisibleChoices, expanded: query.Trim().Length > 0 && VisibleChoices.Count <= 200) : [];
        //a choice remade when the catalog loaded is the same asset
        if (current is not null && !VisibleChoices.Contains(current))
            Selected = VisibleChoices.FirstOrDefault(c => c.Origin == current.Origin && string.Equals(c.InstancedFullPath, current.InstancedFullPath, StringComparison.OrdinalIgnoreCase));
    }

    private async void ShowSelected(AssetChoice choice)
    {
        selectionCancellation?.Cancel();
        var cancellation = selectionCancellation = new CancellationTokenSource();
        CanUse = false;
        usable = null;
        HasPreview = false;
        previewed = null;
        preview.Clear(MaxCachedPackages);
        SelectedName = choice?.Name;
        SelectedPath = choice?.InstancedFullPath;
        SelectedDetails = choice is null ? null : Details(choice, null);
        SelectedProblem = null;
        if (choice is null)
        {
            PreviewNote = "Select an asset to preview it.";
            return;
        }
        PreviewNote = "Opening…";
        ResolvedAssetChoice resolved;
        try
        {
            //moving through the list with the arrow keys opens only where it stops
            await Task.Delay(100, cancellation.Token);
            resolved = await Task.Run(() => AssetPicking.Resolve(choice, request, preview.Context.PackageCache), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            resolved = new(null, null, $"It couldn't be opened: {e.FlattenException()}");
        }
        if (cancellation.IsCancellationRequested || closed) return;
        SelectedDetails = Details(choice, resolved.Export);
        SelectedProblem = resolved.Problem;
        CanUse = resolved.Problem is null;
        if (CanUse) usable = resolved.Reference;
        if (resolved.Export is not { } export)
        {
            PreviewNote = "It can't be previewed: it couldn't be found.";
            return;
        }
        switch (request.Kind)
        {
            case AssetKind.StaticMesh:
                preview.ShowMesh(export);
                HasPreview = true;
                break;
            case AssetKind.Material or AssetKind.DecalMaterial:
                previewed = export;
                preview.ShowMaterial(export, shape);
                HasPreview = true;
                break;
            default:
                PreviewNote = "The Level Editor doesn't draw particle systems, so there's no preview.";
                break;
        }
    }

    //its class, what the database records about it, and where else it is
    private string Details(AssetChoice choice, ExportEntry export)
    {
        var lines = new List<string>();
        string className = export?.ClassName ?? choice.Entry?.ClassName ?? choice.Indexed?.ClassName;
        string line = string.Join(", ", new[] { className, choice.Indexed?.Details }.Where(s => s is not null));
        if (line.Length > 0) lines.Add(line);
        string fileName = System.IO.Path.GetFileName(request.Destination.FilePath);
        lines.Add(choice.Entry switch
        {
            ImportEntry => $"Imported by {fileName}",
            not null => $"In {fileName}",
            _ => $"From {choice.Indexed.Source.File.FileName} ({choice.SourceLabel})"
        });
        if (choice.Entry is null) lines.Add($"Using it adds nothing to {fileName} until a commit, which can't copy it in yet: that comes next.");
        if (choice.Indexed is { } indexed)
        {
            //the file being edited may be a game file the database lists
            var others = indexed.Copies.Where(c => !string.Equals(c.File.FilePath, request.Destination.FilePath, StringComparison.OrdinalIgnoreCase)
                && (choice.Entry is not null || c.File != indexed.Source.File)).ToList();
            var elsewhere = others.Select(c => c.File.Label).Distinct().ToList();
            int files = others.Count;
            if (files > 0)
            {
                string where = elsewhere.Count is > 0 and <= 4 ? $" ({string.Join(", ", elsewhere)})" : "";
                lines.Add($"Also in {files:N0} other file{(files == 1 ? "" : "s")}{where}");
            }
        }
        return string.Join("\n", lines);
    }

    private void Use()
    {
        if (!CanUse || usable is null) return;
        chosen = usable;
        DialogResult = true;
    }

    private void Use_Click(object sender, RoutedEventArgs e) => Use();

    private void ChoiceList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Use();

    //a folder's selection leaves the asset shown
    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is AssetChoice choice) Selected = choice;
    }

    //on a folder, it opens or closes it
    private void FolderTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FolderTree.SelectedItem is AssetChoice) Use();
    }

    //from the search box, down goes into the list, or the tree
    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || VisibleChoices.Count == 0) return;
        ApplySearch();
        if (VisibleChoices.Count == 0) return;
        if (showFolders)
        {
            FolderTree.Focus();
            e.Handled = true;
            return;
        }
        Selected ??= VisibleChoices[0];
        ChoiceList.UpdateLayout();
        (ChoiceList.ItemContainerGenerator.ContainerFromItem(Selected) as System.Windows.UIElement ?? ChoiceList).Focus();
        e.Handled = true;
    }

    private void OpenAssetDatabase_Click(object sender, RoutedEventArgs e) => OpenAssetDatabase();

    private void OpenAssetDatabase() => new AssetDatabaseWindow(request.Destination.Game).Show();

    protected override void OnClosing(CancelEventArgs e)
    {
        closed = true;
        searchTimer.Stop();
        selectionCancellation?.Cancel();
        //before the viewport disposes itself and its device, which it does as the window closes
        preview.Dispose();
        base.OnClosing(e);
    }
}
