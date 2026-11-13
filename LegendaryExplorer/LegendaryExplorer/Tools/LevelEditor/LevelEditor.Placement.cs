using System;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Placing new actors (<see cref="ActorCreation"/>): one class at a time, in a chosen level, where a click meets the scene</summary>
public partial class LevelEditor
{
    /// <summary>A class in the Place Actors list</summary>
    public sealed class PlacementOption(PlaceableClass placeable) : NotifyPropertyChangedBase
    {
        public PlaceableClass Placeable { get; } = placeable;
        public string ClassName => Placeable.ClassName;
        public PlacementCategory Category => Placeable.Category;
        public string Description => Placeable.Description;
        public string AssetHint => Placeable.Asset is { } kind ? $"Asks for a {AssetPicking.NameOf(kind)} first" : null;

        private bool isPlacing;
        /// <summary>Whether it's the class a viewport click places now</summary>
        public bool IsPlacing
        {
            get => isPlacing;
            set => SetProperty(ref isPlacing, value);
        }
    }

    /// <summary>
    /// The classes with a recipe, grouped by category and filtered by <see cref="PlacementSearch"/>. Only the file is checked before one is chosen
    /// (<see cref="PlacementRefusal"/>): the class is found (which may load the game's package that defines it) when one is placed
    /// </summary>
    public ListCollectionView PlacementOptions { get; } = MakePlacementOptions();

    private static ListCollectionView MakePlacementOptions()
    {
        var view = new ListCollectionView(ActorCreation.PlaceableClasses.Select(placeable => new PlacementOption(placeable)).ToList());
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PlacementOption.Category)));
        return view;
    }

    private string placementSearch;
    /// <summary>Text the listed classes' names, descriptions or categories must contain</summary>
    public string PlacementSearch
    {
        get => placementSearch;
        set
        {
            if (!SetProperty(ref placementSearch, value)) return;
            string search = value?.Trim();
            PlacementOptions.Filter = string.IsNullOrEmpty(search) ? null : item => item is PlacementOption option
                && (option.ClassName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || option.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || option.Category.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
    }

    private string placementRefusal;
    /// <summary>Why nothing can be placed in <see cref="PlacementFile"/> now, or null</summary>
    public string PlacementRefusal
    {
        get => placementRefusal;
        private set
        {
            if (SetProperty(ref placementRefusal, value)) OnPropertyChanged(nameof(CanPlace));
        }
    }

    /// <summary>Whether a class can be chosen to place in <see cref="PlacementFile"/></summary>
    public bool CanPlace => placementRefusal is null;

    private OpenLevelFile placementFile;
    /// <summary>The level new actors go in. Chosen explicitly: which of several loaded levels the selection is in says nothing about where a new actor belongs</summary>
    public OpenLevelFile PlacementFile
    {
        get => placementFile;
        set
        {
            if (!SetProperty(ref placementFile, value)) return;
            EndPlacement();
            UpdatePlacementOptions();
        }
    }

    private string placementStatus;
    public string PlacementStatus
    {
        get => placementStatus;
        private set => SetProperty(ref placementStatus, value);
    }

    private PlacementOption placing;
    //the asset chosen for the actor being placed, for a class that needs one
    private AssetReference placingAsset;

    private ICommand togglePlacementCommand;
    /// <summary>Starts placing an option's class, or stops if it's the one being placed</summary>
    public ICommand TogglePlacementCommand => togglePlacementCommand ??= new RelayCommand(parameter =>
    {
        var option = (PlacementOption)parameter;
        if (option.IsPlacing) EndPlacement();
        else BeginPlacement(option);
    });

    /// <summary>
    /// Keeps <see cref="PlacementFile"/> one of the open files (the last writable one, unless one was chosen), and the options current: call when
    /// what they're refused for changes (the open files, a file's read-only state). A placement the file now refuses is ended
    /// </summary>
    internal void UpdatePlacementOptions()
    {
        if (placementFile is not null && !OpenFiles.Contains(placementFile))
        {
            EndPlacement();
            placementFile = null;
        }
        if (placementFile is null)
        {
            placementFile = OpenFiles.LastOrDefault(f => !f.IsReadOnly);
            OnPropertyChanged(nameof(PlacementFile));
        }
        PlacementRefusal = ActorCreation.FileRefusal(placementFile);
        if (PlacementRefusal is not null) EndPlacement();
    }

    private void BeginPlacement(PlacementOption option)
    {
        string className = option.ClassName;
        if (!CanMutateScene)
        {
            PlacementStatus = "Wait for the current operation to finish, then place again.";
            return;
        }
        if (ActorCreation.PlacementRefusal(placementFile, className, RenderContext.PackageCache) is { } refusal)
        {
            PlacementStatus = refusal;
            UpdatePlacementOptions();
            return;
        }
        AssetReference asset = null;
        if (ActorCreation.AssetKindOf(className) is { } assetKind)
        {
            asset = AssetPickerWindow.Pick(this, declinedAssetDatabasePrompts, new AssetPickRequest($"The new {className}'s {AssetPicking.NameOf(assetKind)}", placementFile.Package, assetKind));
            //(the scene may have become busy, or the file read-only, while the dialog was open)
            if (asset is null || !CanMutateScene || ActorCreation.FileRefusal(placementFile) is not null) return;
        }
        CancelViewportDrag();
        EndPlacement();
        EndSculpting();
        placing = option;
        option.IsPlacing = true;
        placingAsset = asset;
        RenderContext.PlaceAt = PlaceAt;
        SceneViewer.Cursor = Cursors.Cross;
        string what = asset is null ? className : $"{className} of {asset.Name}";
        PlacementStatus = $"Click where the {what} goes in {placementFile.FileName}: on a surface, or anywhere else to place it in front of the camera. Esc cancels.";
    }

    /// <summary>
    /// A viewport click while placing: the new actor goes where UE3's editor would put it (<see cref="LevelEditorRenderContext.PlacementAt(int, int)"/>),
    /// snapped to the grid if its class's default bEdShouldSnap asks to be, as UE3's Constraints.Snap does
    /// </summary>
    private void PlaceAt(int x, int y)
    {
        if (placing is null) return;
        if (!CanMutateScene)
        {
            EndPlacement();
            PlacementStatus = "Placing was cancelled: wait for the current operation to finish, then place again.";
            return;
        }
        string className = placing.ClassName;
        AssetReference asset = placingAsset;
        EndPlacement();
        //from a viewport click: finding where it goes reads the level's geometry and the class, which mustn't escape the mouse handler if they fail
        try
        {
            var widget = RenderContext.TransformWidget;
            var point = RenderContext.PlacementAt(x, y);
            UndoHistory.Push(ActorCreation.Place(this, placementFile, className, point.Location, widget.TranslateSnapEnabled ? widget.TranslateSnapValue : 0, asset,
                point.Normal));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            MessageBox.Show(this, error.Message, "Place actor");
        }
    }

    private void EndPlacement()
    {
        if (placing is null) return;
        placing.IsPlacing = false;
        placing = null;
        placingAsset = null;
        RenderContext.PlaceAt = null;
        SceneViewer.Cursor = null;
        PlacementStatus = null;
    }
}
