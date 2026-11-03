using System;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Placing new actors (<see cref="ActorCreation"/>): one class at a time, in a chosen level, where a click meets the scene</summary>
public partial class LevelEditor
{
    public sealed record PlacementOption(string ClassName, string Refusal)
    {
        public string Description => Refusal ?? $"Place a {ClassName}: click the scene where it goes";
    }

    /// <summary>
    /// The classes with a recipe, and why none can be placed in <see cref="PlacementFile"/> now. Only the file is checked: the class is found (which may
    /// load the game's package that defines it) when one is placed
    /// </summary>
    public ObservableCollectionExtended<PlacementOption> PlacementOptions { get; } = [];

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

    private string placingClass;

    private ICommand beginPlacementCommand;
    public ICommand BeginPlacementCommand => beginPlacementCommand ??= new RelayCommand(
        parameter => BeginPlacement(((PlacementOption)parameter).ClassName),
        parameter => CanMutateScene && parameter is PlacementOption { Refusal: null });

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
        string refusal = ActorCreation.FileRefusal(placementFile);
        if (refusal is not null) EndPlacement();
        PlacementOptions.ReplaceAll(ActorCreation.RecipeClasses.Select(c => new PlacementOption(c, refusal)));
    }

    private void BeginPlacement(string className)
    {
        if (!CanMutateScene) return;
        if (ActorCreation.Refusal(placementFile, className, RenderContext.PackageCache) is { } refusal)
        {
            PlacementStatus = refusal;
            UpdatePlacementOptions();
            return;
        }
        CancelViewportDrag();
        placingClass = className;
        RenderContext.PlaceAt = PlaceAt;
        SceneViewer.Cursor = Cursors.Cross;
        PlacementStatus = $"Click where the {className} goes in {placementFile.FileName}: on a surface, or anywhere else to place it in front of the camera. Esc cancels.";
    }

    /// <summary>
    /// A viewport click while placing: the new actor goes where UE3's editor would put it (<see cref="LevelEditorRenderContext.PlacementLocation(int, int)"/>),
    /// snapped to the grid if its class's default bEdShouldSnap asks to be, as UE3's Constraints.Snap does
    /// </summary>
    private void PlaceAt(int x, int y)
    {
        if (placingClass is null) return;
        if (!CanMutateScene)
        {
            EndPlacement();
            PlacementStatus = "Placing was cancelled: wait for the current operation to finish, then place again.";
            return;
        }
        string className = placingClass;
        EndPlacement();
        //from a viewport click: finding where it goes reads the level's geometry and the class, which mustn't escape the mouse handler if they fail
        try
        {
            var widget = RenderContext.TransformWidget;
            UndoHistory.Push(ActorCreation.Place(this, placementFile, className, RenderContext.PlacementLocation(x, y),
                widget.TranslateSnapEnabled ? widget.TranslateSnapValue : 0));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            MessageBox.Show(this, error.Message, "Place actor");
        }
    }

    private void EndPlacement()
    {
        if (placingClass is null) return;
        placingClass = null;
        RenderContext.PlaceAt = null;
        SceneViewer.Cursor = null;
        PlacementStatus = null;
    }
}
