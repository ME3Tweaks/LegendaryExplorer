using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class LevelEditor
{
    private ViewportLightingMode _lightingMode = ViewportLightingMode.Level;
    public ViewportLightingMode LightingMode
    {
        get => _lightingMode;
        set
        {
            if (SetProperty(ref _lightingMode, value))
            {
                OnPropertyChanged(nameof(IsLevelLighting));
                OnPropertyChanged(nameof(IsPreviewLighting));
                OnPropertyChanged(nameof(IsNeutralLighting));
                OnPropertyChanged(nameof(IsUnlit));
                OnPropertyChanged(nameof(LightingMenuHeader));
                OnPropertyChanged(nameof(LightingRulesNote));
                LightDiagnosticsStale = true;
                if (value is ViewportLightingMode.Level or ViewportLightingMode.Preview)
                {
                    PrepareGameShaders();
                }
                else
                {
                    RenderContext.LightingMode = value;
                }
            }
        }
    }

    public LightAssignmentPolicy AssignmentPolicy
    {
        get => RenderContext.AssignmentPolicy;
        set
        {
            if (RenderContext.AssignmentPolicy == value) return;
            RenderContext.AssignmentPolicy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreserveAssignments));
            OnPropertyChanged(nameof(RecomputeAssignments));
            OnPropertyChanged(nameof(LightingRulesNote));
            LightDiagnosticsStale = true;
            if (IsPreviewLighting) PrepareGameShaders();
        }
    }
    public bool PreserveAssignments => AssignmentPolicy == LightAssignmentPolicy.PreserveExistingAssignments;
    public bool RecomputeAssignments => AssignmentPolicy == LightAssignmentPolicy.RecomputeAssignments;
    public ICommand SetAssignmentPolicyCommand { get; private set; }

    public bool IsLevelLighting => LightingMode == ViewportLightingMode.Level;
    public bool IsNeutralLighting => LightingMode == ViewportLightingMode.Neutral;
    public bool IsPreviewLighting => LightingMode == ViewportLightingMode.Preview;
    public bool IsUnlit => LightingMode == ViewportLightingMode.Unlit;
    public string LightingMenuHeader => LightingMode switch
    {
        ViewportLightingMode.Unlit => "Unlit ▾",
        ViewportLightingMode.Neutral => "Neutral lighting ▾",
        ViewportLightingMode.Preview => "Preview lighting ▾",
        _ => "Level lighting ▾"
    };

    public bool UseDynamicLighting
    {
        get => RenderContext.UseDynamicLighting;
        set
        {
            RenderContext.UseDynamicLighting = value;
            OnPropertyChanged();
        }
    }

    public bool UseLightMaps
    {
        get => RenderContext.UseLightMaps;
        set
        {
            RenderContext.UseLightMaps = value;
            OnPropertyChanged();
        }
    }

    public ICommand SetLightingModeCommand { get; private set; }

    public ObservableCollectionExtended<EditableActorLighting> SelectedActorLighting { get; } = [];
    private readonly HashSet<EditableActorLighting> PendingActorLightingEdits = [];

    public void OnActorLightingEdited(MeshComponentProxy mesh, ActorLightingSettings before, ActorLightingSettings after)
    {
        if (!_isApplyingUndoRedo) UndoHistory.Push(new ActorLightingAction(mesh.Lighting, before, after));
        PendingActorLightingEdits.Add(mesh.Lighting);
        LightDiagnosticsStale = true;
    }

    internal void EditLightEnvironment_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditScene || sender is not FrameworkElement { DataContext: EditableActorLighting { CanEditEnvironment: true } lighting }) return;
        ShowLightEnvironmentProperties(lighting.EditableEnvironment);
    }

    internal void CreateLightEnvironment_Click(object sender, RoutedEventArgs e)
    {
        if (!CanEditScene || sender is not Button { DataContext: EditableActorLighting { CanCreateEnvironment: true } lighting } button) return;
        try
        {
            var environment = lighting.CreateEnvironment(button.Tag as string == "Bio");
            foreach (var settings in SelectedActorLighting) settings.RefreshEnvironments();
            ShowLightEnvironmentProperties(environment);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not create the light environment: {ex.Message}", "Light environment");
        }
    }

    private void ShowLightEnvironmentProperties(ExportEntry environment)
    {
        if (environment is null) return;
        // Keep package read-only state and selection fixed while this editor can write properties.
        new ExportLoaderHostedWindow(new InterpreterExportLoader(), environment)
        {
            Owner = this,
            Title = $"Light environment properties — {environment.InstancedFullPath}"
        }.ShowDialog();
        foreach (var settings in SelectedActorLighting) settings.RefreshEnvironments();
        LightDiagnosticsStale = true;
    }

    private void ApplyActorLightingEdits()
    {
        if (PendingActorLightingEdits.Count == 0 || PreviewRefreshInProgress) return;
        using var load = RenderContext.BeginLoad();
        foreach (var lighting in PendingActorLightingEdits)
        {
            var mesh = lighting.Mesh;
            if (mesh.Actor is { IsDisposed: false, OwningFile: { } file } actor && actor.Components.Contains(mesh)
                && OpenFiles.Contains(file) && file.Actors.Contains(actor))
                mesh.RefreshPreview(mesh.Export.FileRef, new HashSet<int> { mesh.Export.UIndex }, force: true);
        }
        PendingActorLightingEdits.Clear();
        LightDiagnosticsStale = true;
    }

    private LightSettings? _preEditLightSettings;

    /// <summary>
    /// The selected light's editable settings. Null if the selection isn't a light
    /// </summary>
    public EditableLight SelectedLight => (selectedActor as ILightProxy)?.Light;
    public bool IsLightSelected => SelectedLight is not null;

    // Lights whose uncommitted edits haven't been rebuilt into the scene yet. Rebuilt once per frame, however many edits there were.
    private readonly HashSet<ILightProxy> PendingLightEdits = [];

    public void OnLightEdited(ILightProxy light)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => OnLightEdited(light));
            return;
        }
        PendingLightEdits.Add(light);
    }

    private void ApplyLightEdits()
    {
        if (PendingLightEdits.Count == 0 || PreviewRefreshInProgress) return;
        foreach (ILightProxy light in PendingLightEdits)
        {
            // Proxies replaced by a reload or closed with their file are skipped. One out of the scene that undo can bring back (a pending deletion) isn't
            if (light is ActorProxy { OwningFile: { } file, IsDisposed: false } actor && OpenFiles.Contains(file)
                && (file.Actors.Contains(actor) || file.RetainedActors.Contains(actor)))
            {
                file.UpdateLight(light, RenderContext);
            }
        }
        PendingLightEdits.Clear();
        LightDiagnosticsStale = true;
    }

    private void OnSelectedLightChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EditableLight.Settings) || sender is not EditableLight light) return;
        // Undo, redo and handle drags record their own history.
        if (!_isApplyingUndoRedo && !RenderContext.LightGizmo.IsDragging && _preEditLightSettings is { } before && before != light.Settings
            && selectedActor is not null)
        {
            UndoHistory.Push(new LightSettingsAction(light, before, light.Settings, selectedActor.ActorName));
        }
        _preEditLightSettings = light.Settings;
    }

    private void OnLightGizmoDragComplete(ILightProxy light, LightSettings before, LightSettings after)
    {
        if (light is not ActorProxy actor) return;
        UndoHistory.Push(new LightSettingsAction(light.Light, before, after, actor.ActorName));
        _preEditLightSettings = after;
        LightDiagnosticsStale = true;
    }

    // Uses the active viewport's rules: Level lighting includes the last build's records; the other modes ignore them, like Preview lighting.
    private bool IgnorePrecomputedLighting => LightingMode != ViewportLightingMode.Level;

    private static IEnumerable<MeshComponentProxy> MeshComponents(ActorProxy actor) => actor.Components.OfType<MeshComponentProxy>();

    private SceneLight SelectedSceneLight => selectedActor is ILightProxy proxy ? selectedActor.OwningFile?.Lighting?.Find(proxy.Light.Component) : null;

    private bool _showLightReach;
    /// <summary>
    /// Highlight the meshes the selected light is eligible to light
    /// </summary>
    public bool ShowLightReach
    {
        get => _showLightReach;
        set
        {
            if (SetProperty(ref _showLightReach, value)) LightDiagnosticsStale = true;
        }
    }

    /// <summary>
    /// Highlights the meshes the selected light is eligible to light, if <see cref="ShowLightReach"/>. Cheap enough to do every frame, so it follows drags
    /// </summary>
    private void UpdateLightHighlights()
    {
        RenderContext.HighlightedComponents.Clear();
        if (ShowLightReach && SelectedSceneLight is { } light)
        {
            FindLightReach(light, RenderContext.HighlightedComponents);
        }
    }

    /// <summary>
    /// Counts the meshes a light is eligible to light, directly and through light environments, optionally collecting them
    /// </summary>
    private (int Direct, int Environments) FindLightReach(SceneLight light, HashSet<PrimitiveComponentProxy> meshes)
    {
        int direct = 0, environments = 0;
        bool ignorePrecomputed = IgnorePrecomputedLighting;
        foreach (ActorProxy actor in Actors)
        {
            if (actor is ILightProxy) continue;
            foreach (MeshComponentProxy mesh in MeshComponents(actor))
            {
                if (mesh.GetLightEligibility(light, ignorePrecomputed) is { } eligibility && eligibility.IsAffecting())
                {
                    meshes?.Add(mesh);
                    if (eligibility is LightEligibility.LightEnvironment) environments++;
                    else direct++;
                }
            }
        }
        return (direct, environments);
    }

    private void RefreshSelectedLightReach()
    {
        if (selectedActor is not ILightProxy proxy)
        {
            SelectedLightReach = null;
            return;
        }
        SceneLight light = SelectedSceneLight;
        if (!proxy.Light.Settings.Enabled)
        {
            SelectedLightReach = "Disabled: lights nothing.";
        }
        else if (light is null)
        {
            SelectedLightReach = "Not in the preview (unsupported or unreadable settings).";
        }
        else
        {
            (int direct, int environments) = FindLightReach(light, null);
            SelectedLightReach = $"Eligible to light {direct} mesh{(direct == 1 ? "" : "es")} directly and {environments} through light environments"
                                 + (ShowLightReach ? " (highlighted)." : ".");
        }
    }

    private string _selectedLightReach;
    public string SelectedLightReach
    {
        get => _selectedLightReach;
        private set => SetProperty(ref _selectedLightReach, value);
    }

    public string LightingRulesNote => LightingMode switch
    {
        ViewportLightingMode.Preview => $"{AssignmentPolicy.Describe()}: shared preview/bake eligibility. Direct lighting only; occlusion is approximate and bounced light is absent." + (PreserveAssignments ? " Historical exclusions remain until you select Recompute assignments." : ""),
        ViewportLightingMode.Level => "Level rules: includes the last bake's light-map and irrelevance records. Bounced light comes only from the existing light-maps, so it doesn't follow edits.",
        _ => $"This mode does not show level lights. Eligibility uses {AssignmentPolicy.Describe()}."
    };

    private bool LightDiagnosticsStale;

    /// <summary>
    /// A level's bake occluders that the preview has no shadow geometry for. Whether it has BSP, and which actors are terrains, come from the level's own
    /// binaries, read again when its Level or Model export is updated (or, as a backstop, when its export count changes). The terrain sections that cast also depend on
    /// class defaults, so they're counted again after any update
    /// </summary>
    private sealed class UnshadowedGeometryEntry
    {
        public int ExportCount;
        //the exports they were read from
        public int LevelUIndex, ModelUIndex;
        public bool HasBsp;
        public ExportEntry[] TerrainActors = [];
        public int? TerrainSections;
    }

    private readonly Dictionary<IMEPackage, UnshadowedGeometryEntry> UnshadowedGeometry = [];

    /// <summary>
    /// The level's occluders in a Lightmass bake that the preview has no shadow geometry for: its BSP, and the terrain sections that take part in
    /// static lighting and cast shadows (as LightmassSceneBuilder exports them). Spline meshes aren't baked at all
    /// </summary>
    private Dictionary<string, int> UnshadowedGeometryCounts(IMEPackage package)
    {
        //what can't be read counts as none until it's read again: when the Level export (or for terrain sections, anything) is updated, or exports are added
        if (!UnshadowedGeometry.TryGetValue(package, out var entry) || entry.ExportCount != package.ExportCount)
        {
            UnshadowedGeometry[package] = entry = new UnshadowedGeometryEntry { ExportCount = package.ExportCount };
            try
            {
                if (package.Exports.FirstOrDefault(e => e.ClassName == "Level") is { } levelExport)
                {
                    //before it's parsed, so that one that can't be is read again when it's updated
                    entry.LevelUIndex = levelExport.UIndex;
                    var level = levelExport.GetBinaryData<Level>();
                    entry.ModelUIndex = level.Model;
                    entry.HasBsp = package.TryGetUExport(level.Model, out var modelExport) && modelExport.GetBinaryData<Model>().Nodes.Length > 0;
                    entry.TerrainActors = Lightmaps.Lightmass.LightmassSceneBuilder.TerrainActors(package, level);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Could not read {package.FileNameNoExtension}'s BSP and terrain actors: {e.Message}");
            }
        }
        if (entry.TerrainSections is null)
        {
            try
            {
                entry.TerrainSections = entry.TerrainActors.Length == 0 ? 0
                    : Lightmaps.Lightmass.LightmassSceneBuilder.ShadowCastingTerrainSections(entry.TerrainActors, RenderContext.PackageCache);
            }
            catch (Exception e)
            {
                entry.TerrainSections = 0;
                Debug.WriteLine($"Could not count {package.FileNameNoExtension}'s terrain sections: {e.Message}");
            }
        }
        var counts = new Dictionary<string, int>();
        if (entry.HasBsp) counts["level(s) of BSP"] = 1;
        if (entry.TerrainSections > 0) counts["terrain section(s)"] = entry.TerrainSections.Value;
        return counts;
    }
    private readonly Stopwatch LightDiagnosticsThrottle = Stopwatch.StartNew();

    public ObservableCollectionExtended<LightRelevance> SelectedMeshLights { get; } = [];
    public ObservableCollectionExtended<string> SelectedMeshNotes { get; } = [];

    private bool _isMeshSelected;
    public bool IsMeshSelected
    {
        get => _isMeshSelected;
        private set => SetProperty(ref _isMeshSelected, value);
    }

    /// <summary>
    /// Lists the lights relevant to the selected mesh: those eligible to light it, and those that reach it but are excluded, with the reason.
    /// Also notes where the mesh's appearance doesn't come from the level's lights
    /// </summary>
    private void RefreshLightingDiagnostics()
    {
        LightDiagnosticsStale = false;
        LightDiagnosticsThrottle.Restart();
        RefreshSelectedLightReach();
        RenderContext.HighlightedLights.Clear();
        var rows = new List<LightRelevance>();
        var notes = new List<string>();
        List<MeshComponentProxy> meshes = selectedActor is null or ILightProxy ? [] : MeshComponents(selectedActor).ToList();
        //the preview's limits only matter in Preview lighting, and only for a selected mesh
        if (IsPreviewLighting && meshes.Count > 0)
        {
            notes.AddRange(meshes.SelectMany(mesh => mesh.ShadowNotes).Distinct());
            var totals = new SortedDictionary<string, int>();
            foreach (var file in OpenFiles)
                foreach (var (className, count) in UnshadowedGeometryCounts(file.Package))
                    totals[className] = totals.GetValueOrDefault(className) + count;
            var unsupported = totals.Select(total => $"{total.Value} {total.Key}").ToArray();
            if (unsupported.Length > 0)
                notes.Add("Preview has no shadow geometry for BSP or terrain, which block light in a Lightmass bake: " + string.Join(", ", unsupported) + ".");
            if (PreserveAssignments && RenderContext.AssignmentHistory.Problems is { Count: > 0 } problems)
                notes.Add("Some source assignments could not be read; preservation is incomplete: " + string.Join("; ", problems.Take(3)));
            //meshes without static lighting (skeletal meshes, movable meshes) have no bake mapping to lack
            if (meshes.Any(mesh => mesh.ReceivesStaticLighting && !mesh.HasCompatibleBakeMapping))
                notes.Add("One or more receivers need a new bake mapping. The baker checks geometry and cooked shader support before allocating one; see its report for unsupported receivers.");
        }
        IsMeshSelected = meshes.Count > 0;
        var lighting = meshes.Select(mesh => mesh.Lighting).ToList();
        if (!SelectedActorLighting.SequenceEqual(lighting))
        {
            foreach (var settings in lighting) settings.RefreshEnvironments();
            SelectedActorLighting.ReplaceAll(lighting);
        }
        if (meshes.Count > 0)
        {
            bool ignorePrecomputed = IgnorePrecomputedLighting;
            var proxies = new Dictionary<ExportEntry, ActorProxy>();
            foreach (ActorProxy actor in Actors)
            {
                if (actor is ILightProxy proxy) proxies[proxy.Light.Component] = actor;
            }
            foreach (SceneLight light in RenderContext.GetLights(out _))
            {
                LightEligibility? best = null;
                foreach (MeshComponentProxy mesh in meshes)
                {
                    if (mesh.GetLightEligibility(light, ignorePrecomputed) is { } eligibility
                        && (best is null || eligibility.IsMoreRelevantThan(best.Value)))
                    {
                        best = eligibility;
                    }
                }
                // Lights that don't reach the mesh at all aren't worth listing.
                if (best is not { } result || !result.Reaches()) continue;
                proxies.TryGetValue(light.Export, out ActorProxy lightActor);
                string reason = result.Describe();
                if (ignorePrecomputed && result.IsAffecting() && !light.HasStaticLighting && !light.HasStaticShadowing)
                    reason += " (runtime only; no bake contribution)";
                rows.Add(new LightRelevance(lightActor?.DisplayText ?? light.Export.ObjectName.Instanced, light.LevelName, reason,
                    result.IsAffecting(), lightActor));
                if (result.IsAffecting()) RenderContext.HighlightedLights.Add(light.Export);
            }
            foreach (var group in meshes.Where(mesh => mesh.UsesUnsupportedLightEnvironment)
                         .GroupBy(mesh => (mesh.Export.Game, mesh.UnsupportedLightEnvironmentReason)))
            {
                notes.Add($"This {group.Key.Game} light environment isn't previewed: {group.Key.UnsupportedLightEnvironmentReason ?? "its mode isn't implemented"}. "
                          + "Lighting and shadows it would supply are missing; the mesh uses neutral lighting.");
            }
            if (meshes.Any(mesh => mesh.IsLitByLightEnvironment && mesh.Export.Game is MEGame.LE1 or MEGame.LE2))
            {
                notes.Add("LE1/LE2 light environments show their settled synthesis. Transition timing, Kismet-driven runtime changes and in-game image parity aren't verified.");
            }
            if (meshes.Any(mesh => mesh.LightEnvironmentSettings is { } s && (s.WrapLightingDirectMultiplier != 1 || s.WrapLightingIndirectMultiplier != 1)))
            {
                notes.Add("This light environment's wrap-lighting multipliers aren't applied; the viewport uses the game's stock wrap-lighting constants.");
            }
            if (meshes.All(mesh => !mesh.IsLitByLevelLights) && meshes.Any(mesh => !mesh.UsesUnsupportedLightEnvironment))
            {
                notes.Add(Game.IsLEGame()
                    ? "The level's lights don't light this mesh in the viewport (its lighting couldn't be read), so it's shown with neutral lighting."
                    : "Level lighting previews need a Legendary Edition game; this mesh is shown with neutral lighting.");
            }
            else if (meshes.Any(mesh => mesh.IsLitByLightEnvironment))
            {
                notes.Add("Lit by a light environment, which composites the lights below into a few lights. Whether each light is blocked from it isn't shown here.");
            }
            if (selectedActor?.OwningFile?.IsReadOnly == true)
                notes.Add("Context-only level: it contributes geometry and lights, but its mappings are not baked in the editor handoff.");
            foreach (var material in meshes.SelectMany(mesh => mesh.GameShaderMaterials).Distinct())
            {
                if (material.LoadedGameShaderError is { } error)
                {
                    notes.Add($"{material.InstancedFullPath} uses LEX's neutral shader: {error}");
                }
            }
        }
        rows.Sort((a, b) => a.IsAffecting != b.IsAffecting ? b.IsAffecting.CompareTo(a.IsAffecting) : string.Compare(a.LightName, b.LightName, StringComparison.OrdinalIgnoreCase));
        SelectedMeshLights.ReplaceAll(rows);
        SelectedMeshNotes.ReplaceAll(notes);
    }
}
