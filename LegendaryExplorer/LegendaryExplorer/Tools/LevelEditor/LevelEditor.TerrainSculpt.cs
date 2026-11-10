using System;
using System.Windows.Input;
using FontAwesome5;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>Sculpting terrains (<see cref="TerrainSculptMode"/>): one tool at a time, chosen in the Terrain tab</summary>
public partial class LevelEditor
{
    /// <summary>A tool in the Terrain tab</summary>
    public sealed class SculptOption(TerrainSculptTool tool, string name, string description, EFontAwesomeIcon icon) : NotifyPropertyChangedBase
    {
        public TerrainSculptTool Tool { get; } = tool;
        public string Name { get; } = name;
        public string Description { get; } = description;
        public EFontAwesomeIcon Icon { get; } = icon;

        private bool isActive;
        /// <summary>Whether it's the tool a drag on a terrain applies now</summary>
        public bool IsActive
        {
            get => isActive;
            set => SetProperty(ref isActive, value);
        }
    }

    public SculptOption[] SculptOptions { get; } =
    [
        new(TerrainSculptTool.Paint, "Raise / lower", "Raises the ground under the brush; Shift lowers it.", EFontAwesomeIcon.Solid_Mountain),
        new(TerrainSculptTool.Smooth, "Smooth", "Evens out bumps under the brush; Shift sharpens them.", EFontAwesomeIcon.Solid_Water),
        new(TerrainSculptTool.Flatten, "Flatten", "Levels the ground under the brush to the height where the stroke began.", EFontAwesomeIcon.Solid_GripLines),
        new(TerrainSculptTool.Holes, "Holes", "Cuts holes in the patches under the brush; Shift fills them.", EFontAwesomeIcon.Solid_DotCircle),
    ];

    private readonly TerrainSculptMode sculptMode = new();
    private const string WaitToSculpt = "Wait for the current operation to finish, then sculpt again.";
    private SculptOption sculpting;

    //(the Terrain tab's boxes keep these at 0 or more)

    /// <inheritdoc cref="TerrainSculptMode.Radius"/>
    public float SculptRadius
    {
        get => sculptMode.Radius;
        set
        {
            if (sculptMode.Radius == value) return;
            sculptMode.Radius = value;
            OnPropertyChanged();
        }
    }

    /// <inheritdoc cref="TerrainSculptMode.Falloff"/>
    public float SculptFalloff
    {
        get => sculptMode.Falloff;
        set
        {
            if (sculptMode.Falloff == value) return;
            sculptMode.Falloff = value;
            OnPropertyChanged();
        }
    }

    /// <inheritdoc cref="TerrainSculptMode.Strength"/>
    public float SculptStrength
    {
        get => sculptMode.Strength;
        set
        {
            if (sculptMode.Strength == value) return;
            sculptMode.Strength = value;
            OnPropertyChanged();
        }
    }

    private string sculptStatus;
    public string SculptStatus
    {
        get => sculptStatus;
        private set => SetProperty(ref sculptStatus, value);
    }

    private ICommand toggleSculptCommand;
    /// <summary>Starts sculpting with a tool, or stops if it's the one in use</summary>
    public ICommand ToggleSculptCommand => toggleSculptCommand ??= new RelayCommand(parameter =>
    {
        var option = (SculptOption)parameter;
        if (option.IsActive) EndSculpting();
        else BeginSculpting(option);
    });

    private void InitTerrainSculpting()
    {
        sculptMode.Refusal = terrain => !CanMutateScene ? WaitToSculpt
            : terrain.OwningFile is not { IsReadOnly: false } ? $"{terrain.DisplayText} is in a read-only file, so it can't be sculpted."
            : null;
        sculptMode.OnRefused = refusal => SculptStatus = refusal;
        sculptMode.OnStrokeComplete = action =>
        {
            UndoHistory.Push(action);
            //(back to what to do, which stays true through undo and commit, from any refusal before it)
            if (sculpting is not null) SculptStatus = SculptInstructions(sculpting);
        };
    }

    private void BeginSculpting(SculptOption option)
    {
        if (!CanMutateScene)
        {
            SculptStatus = WaitToSculpt;
            return;
        }
        CancelViewportDrag();
        EndPlacement();
        EndSculpting();
        sculpting = option;
        option.IsActive = true;
        sculptMode.Tool = option.Tool;
        RenderContext.TerrainSculpt = sculptMode;
        SceneViewer.Cursor = Cursors.Cross;
        SculptStatus = SculptInstructions(option);
    }

    private static string SculptInstructions(SculptOption option) =>
        $"{option.Name}: drag on a terrain. {option.Description} Dragging anywhere else moves the camera. Esc stops.";

    private void EndSculpting()
    {
        if (sculpting is null) return;
        CancelViewportDrag();
        sculpting.IsActive = false;
        sculpting = null;
        RenderContext.TerrainSculpt = null;
        SceneViewer.Cursor = null;
        SculptStatus = null;
    }
}
