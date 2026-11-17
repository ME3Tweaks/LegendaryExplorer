using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    public EditableDecal SelectedDecal => (selectedActor as DecalActorProxy)?.Decal;
    public bool IsDecalSelected => SelectedDecal is not null;
    public void OnDecalEdited(DecalActorProxy actor, DecalSettings before, DecalSettings after, string property)
    {
        if (!_isApplyingUndoRedo) UndoHistory.Push(new DecalSettingsAction(actor, before, after, property));
    }

    public SplineActorProxy SelectedSpline => selectedActor as SplineActorProxy;
    public bool IsSplineSelected => SelectedSpline is not null;
    public void OnSplineConnectionsEdited(IUndoAction action)
    {
        if (!_isApplyingUndoRedo) UndoHistory.Push(action);
        LightDiagnosticsStale = true;
    }
    private void SplineConnection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SplineConnectionItem item, Tag: string operation }) return;
        switch (operation)
        {
            case "Break": item.Break(); break;
            case "Reverse": item.Reverse(); break;
            case "Straight": item.Straighten(); break;
        }
    }
    private void SplineBreakAll_Click(object sender, RoutedEventArgs e) => SelectedSpline?.BreakAllConnections();
    private void SplineReverseAll_Click(object sender, RoutedEventArgs e) => SelectedSpline?.ReverseWholeSpline();
    private void SplineMesh_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpline is not { IsLoft: true } point) return;
        UsePick(new AssetPickRequest($"DeformMesh of {point.ActorName}", point.Package, AssetKind.StaticMesh), mesh =>
        {
            if (point.SetDeformMesh(mesh, point.DeformMeshMaterials) is null) return;
            //allowed, since its sections can be given overrides next
            var problems = point.SplineMeshMaterialProblems(mesh, [.. point.DeformMeshMaterials]).ToList();
            if (problems.Count > 0)
            {
                MessageBox.Show(this, $"In game, some of {mesh.Name}'s sections won't be drawn with their materials on a spline. " +
                    $"Give them DeformMeshMaterials overrides that can be:\n\n{string.Join("\n", problems)}", "DeformMesh");
            }
        });
    }

    //an override must be a material the game draws on a spline mesh (SetDeformMesh)
    private static AssetPickRequest SplineMaterialRequest(string title, SplineActorProxy point) =>
        new(title, point.Package, AssetKind.Material) { Problem = MaterialUsage.SplineMeshProblem };

    //the game draws a material it can't draw on a spline mesh with its default material instead
    private bool RefuseSplineMaterial(SplineActorProxy point, AssetReference material)
    {
        if (point.SplineMeshMaterialProblem(material) is not { } problem) return false;
        MessageBox.Show(this, problem, "DeformMeshMaterials");
        return true;
    }
    private void SplineClearMesh_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpline is { } point) point.SetDeformMesh(null, point.DeformMeshMaterials);
    }
    private void SplineMaterials_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpline is not { IsLoft: true } point) return;
        UsePick(SplineMaterialRequest($"Add a DeformMeshMaterials override to {point.ActorName}", point), material =>
        {
            if (!RefuseSplineMaterial(point, material)) point.SetDeformMesh(point.DeformMesh, point.DeformMeshMaterials.Append(material));
        });
    }
    private void SplineClearMaterials_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSpline is { } point) point.SetDeformMesh(point.DeformMesh, []);
    }
    private void SplineMaterialSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SplineMaterialSlot slot, Tag: string operation }) return;
        var values = slot.Point.DeformMeshMaterials.ToList();
        switch (operation)
        {
            case "Choose":
                UsePick(SplineMaterialRequest($"Material override for slot {slot.Index} of {slot.Point.ActorName}", slot.Point), material =>
                {
                    if (RefuseSplineMaterial(slot.Point, material)) return;
                    values[slot.Index] = material;
                    slot.Point.SetDeformMesh(slot.Point.DeformMesh, values);
                });
                return;
            case "Clear": values[slot.Index] = null; break;
            case "Remove": values.RemoveAt(slot.Index); break;
        }
        slot.Point.SetDeformMesh(slot.Point.DeformMesh, values);
    }
    public void OnSplineEdited(SplineActorProxy actor, SplineLoftSettings before, SplineLoftSettings after, string property)
    {
        //a handle drag is recorded once, when it ends
        if (!_isApplyingUndoRedo && !RenderContext.SplineGizmo.IsDragging) UndoHistory.Push(new SplineSettingsAction(actor, before, after, property));
    }
    private void OnSplineGizmoDragComplete(SplineActorProxy actor, SplineLoftSettings before, SplineLoftSettings after) =>
        UndoHistory.Push(new SplineSettingsAction(actor, before, after, "tangent"));

    /// <summary>
    /// The selected actor's static mesh components whose materials can be chosen (<see cref="StaticMeshComponentProxy.CanChooseMaterials"/>), as can
    /// every one's whose mesh can be (<see cref="StaticMeshComponentProxy.CanChooseMesh"/>)
    /// </summary>
    public ObservableCollectionExtended<StaticMeshComponentProxy> SelectedMeshChoices { get; } = [];

    private bool isMeshChoiceSelected;
    public bool IsMeshChoiceSelected
    {
        get => isMeshChoiceSelected;
        private set => SetProperty(ref isMeshChoiceSelected, value);
    }

    private void RefreshMeshChoices()
    {
        List<StaticMeshComponentProxy> meshes = selectedActor?.Components.OfType<StaticMeshComponentProxy>().Where(m => m.CanChooseMaterials).ToList() ?? [];
        SelectedMeshChoices.ReplaceAll(meshes);
        IsMeshChoiceSelected = meshes.Count > 0;
    }

    //Choose mesh: another static mesh for the component to draw until a commit writes it, as one undo step
    private void ChooseMesh_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: StaticMeshComponentProxy mesh } || MeshChangeRefused(mesh)) return;
        // Asked again once it's picked: the picker is modal, so a preview refresh may have begun meanwhile
        UsePick(new AssetPickRequest($"StaticMesh of {mesh.Actor.ActorName}", mesh.Actor.Package, AssetKind.StaticMesh), chosen =>
        {
            if (!MeshChangeRefused(mesh)) ChangeMesh(() => mesh.ChooseMesh(chosen));
        });
    }

    //Revert: the component draws its own mesh again
    private void RevertMesh_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: StaticMeshComponentProxy mesh } && !MeshChangeRefused(mesh)) ChangeMesh(mesh.RevertMesh);
    }

    private bool MeshChangeRefused(StaticMeshComponentProxy mesh) => MeshEditRefused(mesh.MeshChangeRefusal, "StaticMesh");

    //says why a mesh component's edit can't be made now, if it can't (refusal: the component's own reason), titled caption
    private bool MeshEditRefused(string refusal, string caption)
    {
        if (!CanMutateScene) refusal = "Wait for the current operation (such as a preview refresh) to finish, then try again.";
        if (refusal is not null) MessageBox.Show(this, refusal, caption);
        return refusal is not null;
    }

    private void ChangeMesh(Func<IUndoAction> change) => ApplyMeshEdit(change, "StaticMesh");

    //an edit of a mesh component, as one undo step; why it can't be made is shown, titled caption
    private void ApplyMeshEdit(Func<IUndoAction> change, string caption)
    {
        IUndoAction step;
        try { step = change(); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            MessageBox.Show(this, error.Message, caption);
            return;
        }
        if (step is not null) UndoHistory.Push(step);
    }

    //Choose: another material for a slot of the component's mesh, drawn until a commit writes it, as one undo step. Only one the game draws there
    private void ChooseMaterial_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: MeshMaterialSlot slot } || MaterialChangeRefused(slot.Mesh)) return;
        var mesh = slot.Mesh;
        var request = new AssetPickRequest($"Material for slot {slot.Index} of {mesh.Actor.ActorName}", mesh.Actor.Package, AssetKind.Material)
        {
            Problem = (material, cache) => MaterialUsage.StaticMeshProblem(material, cache, mesh.HasStaticLighting)
        };
        // Asked again once it's picked: the picker is modal, so a preview refresh may have begun meanwhile
        UsePick(request, chosen =>
        {
            if (!MaterialChangeRefused(mesh)) ChangeMaterials(() => mesh.ChooseMaterial(slot.Index, chosen));
        });
    }

    //Revert to default: the slot is drawn with the mesh's own material again
    private void ClearMaterial_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MeshMaterialSlot slot } && !MaterialChangeRefused(slot.Mesh)) ChangeMaterials(() => slot.Mesh.ClearMaterial(slot.Index));
    }

    //Delete: an override past the mesh's slots, which the game never reads
    private void DeleteExtraMaterial_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MeshMaterialSlot slot } && !MaterialChangeRefused(slot.Mesh)) ChangeMaterials(() => slot.Mesh.DeleteExtraMaterial(slot.Index));
    }

    //Revert materials: the component's own overrides again
    private void RevertMaterials_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: StaticMeshComponentProxy mesh } && !MaterialChangeRefused(mesh)) ChangeMaterials(mesh.RevertMaterials);
    }

    private bool MaterialChangeRefused(StaticMeshComponentProxy mesh) => MeshEditRefused(mesh.MaterialChangeRefusal, "Materials");

    private void ChangeMaterials(Func<IUndoAction> change) => ApplyMeshEdit(change, "Materials");

    //(by an edit or an undo step) the selected actor may draw another mesh, which other lights reach
    void IActorEditorContext.OnMeshChosen(StaticMeshComponentProxy mesh) => LightDiagnosticsStale = true;

    private void RefreshDecalFilter_Click(object sender, RoutedEventArgs e) => SelectedDecal?.RefreshFilterCandidates();
    private void ClearDecalFilter_Click(object sender, RoutedEventArgs e) => SelectedDecal?.ClearFilter();

    private void DecalMaterial_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDecal is not { IsEditable: true } decal) return;
        UsePick(new AssetPickRequest("DecalMaterial", decal.Package, AssetKind.DecalMaterial), material =>
        {
            if (decal.SetMaterial(material) is { } problem) MessageBox.Show(this, problem, "DecalMaterial");
        });
    }
}
