using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
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
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class LevelEditor
{
    //null, which ends the drag, if it can't be duplicated, or its copy can't be made
    private IActorDuplication BeginDuplicateDrag(ActorProxy actor)
    {
        string problem;
        try
        {
            if (DuplicateRefusal(actor) is not { } refusal)
            {
                var mark = RenderContext.MarkShaderLoads();
                var duplication = BeginDuplicate(actor);
                // The drag holds the preparation batch back until it ends, so the copy's shaders are loaded now, or it's drawn with LEX's until then.
                // Only for how it's drawn: the copy is made either way, and what fails is drawn with LEX's shader, as a batch's failures are
                try { RenderContext.LoadGameShadersNow(mark); }
                catch (AggregateException failures) { RecordGameShaderLoadFailures(failures); }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Debug.WriteLine($"Could not load the copy's game shaders: {error}");
                    StatusBar_LeftMostText.Text = $"The copy's game shaders couldn't be loaded, so it's drawn with LEX's until they are: {error.Message}";
                }
                return duplication;
            }
            problem = $"{actor.ActorName} can't be duplicated. {refusal}";
        }
        catch (Exception error) when (error is not OutOfMemoryException) { problem = error.Message; }
        //once the drag has ended, so the message doesn't arrive in the middle of its mouse input
        Dispatcher.InvokeAsync(() => MessageBox.Show(this, problem, "Duplicate"));
        return null;
    }

    private string DuplicateRefusal(ActorProxy actor) => CanMutateScene ? actor.DuplicateRefusal
        : "Wait for the current operation (such as saving or refreshing the preview) to finish, then try again.";

    /// <summary>A spline point's duplicate, which connects the copy to it, or any other actor's copy</summary>
    private static IActorDuplication BeginDuplicate(ActorProxy actor, Vector3 offset = default) =>
        actor is SplineActorProxy point ? point.BeginDuplicate(offset) : ActorCloning.BeginClone(actor, offset);

    /// <summary>
    /// The actor context menu's Duplicate: a copy one grid step from the actor, selected, as UE3's editor duplicates an actor (edactDuplicateSelected).
    /// CreateLocationOffset (UnEdAct.cpp) offsets it across the view, along X and Y in a perspective or top view, which are the Level Editor's. The grid's
    /// size, whether or not translations snap to it. The item is disabled for an actor that can't be duplicated, with the reason as its tooltip
    /// </summary>
    private void DuplicateActor(object parameter)
    {
        if ((parameter as ActorProxy ?? SelectedActor) is not { } actor) return;
        try
        {
            if (DuplicateRefusal(actor) is { } refusal) throw new InvalidOperationException($"{actor.ActorName} can't be duplicated. {refusal}");
            CancelViewportDrag();
            //offset before it's selected, so the offset isn't recorded as an edit of it. Completing it cancels it if it fails, or if it's left where the
            //actor is: far from the origin, a small step is lost to rounding
            if (BeginDuplicate(actor, new Vector3(TranslateSnapValue, TranslateSnapValue, 0)).Complete() is not { } step)
                throw new InvalidOperationException($"A grid step of {TranslateSnapValue} is too small to move a copy off {actor.ActorName} where it is. Make the grid larger, or Alt+drag it.");
            OnDuplicateDragComplete(step);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { MessageBox.Show(this, error.Message, "Duplicate"); }
    }

    //where the viewport's actor menu was opened, which its Paste puts a copy
    private Point viewportMenuPoint;

    /// <summary>
    /// Why Paste can't make a copy of the copied actor in the Place Actors tab's level (<see cref="PlacementFile"/>), as of when it was last asked for:
    /// when the actor menu opened (when it's shown) or an actor was copied, in any window. Ctrl+V finds it again when it's pressed (<see cref="PasteActor"/>).
    /// That the copied actor is another game's isn't a refusal here: Paste says so (<see cref="PasteActor"/>)
    /// </summary>
    public string PasteRefusal { get; private set; }

    /// <summary>
    /// Whether Paste could run (<see cref="PasteRefusal"/>), which the actor menu's Paste is enabled by: it makes a copy, or, for another game's copied
    /// actor, says it can't (<see cref="ActorClipboard.GameRefusal"/>)
    /// </summary>
    public bool CanPaste => PasteRefusal is null;

    //why Paste can't make a copy now, or null: what was copied passed Copy's checks, and its level never changes, so only where it goes is checked
    private string FindPasteRefusal() =>
        !CanMutateScene ? "Wait for the current operation (such as saving or refreshing the preview) to finish, then try again."
        : RenderContext.IsDragging ? "Finish moving first."
        : ActorClipboard.Copied is null ? "Copy an actor first (Ctrl+C), in this or another Level Editor window."
        : PlacementFile is not { } destination ? "Choose the level to paste it in, on the Place Actors tab."
        : ActorCreation.FileRefusal(destination) is { } refusal ? refusal
        : null;

    /// <summary>
    /// Copy (Ctrl+C, or the actor menu's): copies the actor as it's shown now, which Paste in any Level Editor window makes copies of (<see cref="ActorCapture"/>,
    /// <see cref="ActorClipboard"/>). What it was copied from can then be edited, deleted or closed. The status bar says what it didn't copy, or why it
    /// couldn't be copied
    /// </summary>
    /// <returns>Whether there was an actor to copy (or to say why it can't be)</returns>
    private bool CopyActor(object parameter)
    {
        if ((parameter as ActorProxy ?? SelectedActor) is not { } actor) return false;
        // (why the actor itself can't be copied is Make's to say)
        string refusal = !CanMutateScene ? "Wait for the current operation (such as saving or refreshing the preview) to finish, then try again."
            : RenderContext.IsDragging ? "Finish moving first."
            : null;
        if (refusal is not null)
        {
            StatusBar_LeftMostText.Text = $"{actor.ActorName} can't be copied. {refusal}";
            return true;
        }
        ActorCapture capture;
        try
        {
            // (its level's package is snapshotted, which takes a moment for a large one)
            Mouse.OverrideCursor = Cursors.Wait;
            capture = copiedActors.Make(actor);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            StatusBar_LeftMostText.Text = error.Message;
            return true;
        }
        finally { Mouse.OverrideCursor = null; }
        string replacing = null;
        // Every window's Paste is told, and each lets go of the copies it no longer needs, this one's included: ClipboardChanged
        try { copiedActors.Copy(capture); }
        catch (Exception error) when (error is not OutOfMemoryException) { replacing = $" The copy it replaces couldn't be let go of: {error.Message}"; }
        StatusBar_LeftMostText.Text = $"Copied {actor.ActorName}. Paste (Ctrl+V), in this or another Level Editor window, puts a copy of it in the level chosen in its Place Actors tab."
            + (PlacementFile is { } destination ? $" ({destination.FileName})" : "")
            + (capture.NotCopied.Count == 0 ? "" : $" Not copied: {string.Join("; ", capture.NotCopied)}.") + replacing;
        return true;
    }

    //another copy is the clipboard's, made in this window or another: Paste is told, and this window lets go of what it has of copies it no longer
    //needs, unless it's busy (its next release check does). Its errors are its own, said in its own status bar, whichever window copied
    private void ClipboardChanged()
    {
        NotifyPasteRefusal();
        if (!CanMutateScene) return;
        try { copiedActors.ReleaseUnused(OpenFiles); }
        catch (Exception error) when (error is not OutOfMemoryException) { StatusBar_LeftMostText.Text = error.Message; }
    }

    private void NotifyPasteRefusal()
    {
        PasteRefusal = FindPasteRefusal();
        OnPropertyChanged(nameof(PasteRefusal));
        OnPropertyChanged(nameof(CanPaste));
    }

    /// <summary>
    /// Lets go of what this window has of the copied actors (<see cref="CopiedActors"/>) that it no longer needs: not the clipboard's, nor the source of any
    /// of its levels' uncommitted pastes or undone ones (which redo would bring back). And disposes the chosen assets' packages (<see cref="AssetPackage"/>) that nothing refers to:
    /// not the actor being placed, nor any open level's actor (a draft, an undone one, or a pending edit), nor any undo step, nor a pick being put to use
    /// </summary>
    private void ReleaseUnused()
    {
        copiedActors.ReleaseUnused(OpenFiles);
        assetPackages.ReleaseUnused(AssetPackage.ReferencesIn(OpenFiles, UndoHistory).Append(placingAsset).Append(pickInUse));
    }

    //a pick being put to use, which isn't released before what it's for refers to it (UsePick, placement)
    private AssetReference pickInUse;

    //once a pick has been put to use: it's released if it wasn't used, with anything else nothing refers to
    private void ReleaseUnusedPick()
    {
        pickInUse = null;
        ReleaseUnused();
    }

    //the chosen assets of other files that are copied into packages of their own, which drafts and edits refer to until they're committed
    private readonly AssetPackages assetPackages;

    //an asset picker of the request, whose choice from another file is kept in a package of its own (KeepAsset)
    private AssetReference PickAsset(AssetPickRequest request) => AssetPickerWindow.Pick(this, declinedAssetDatabasePrompts, request,
        chosen => KeepAsset(chosen, request.Destination));

    //what a picked asset is kept as (AssetPackages.Keep)
    private AssetReference KeepAsset(AssetReference chosen, IMEPackage destination) => assetPackages.Keep(chosen, destination);

    /// <summary>
    /// For a pick's handler: <paramref name="use"/> uses the asset picked, if any, then the packages of picks that weren't used (refused, or cancelled) are
    /// released
    /// </summary>
    private void UsePick(AssetPickRequest request, Action<AssetReference> use)
    {
        try
        {
            if ((pickInUse = PickAsset(request)) is { } picked) use(picked);
        }
        finally { ReleaseUnusedPick(); }
    }

    //what this window has of the copied actors that pastes are made from (the clipboard's is shared by every window: ActorClipboard)
    private readonly CopiedActors copiedActors;

    //before the previews' caches are emptied, which the copied actors' views' meshes were made with
    private void ForgetCaptureViews() => copiedActors.ForgetViews();

    //as the window closes, once its levels have: lets go of what it has of the copied actors (the last window to close lets go of the copies themselves,
    //once it has closed: ActorClipboard.Join), and disposes every chosen asset's package
    private void ReleaseAll()
    {
        try { copiedActors.ReleaseAll(); }
        finally { assetPackages.ReleaseAll(); }
    }

    /// <summary>
    /// Paste (Ctrl+V, or the actor menu's): a copy of the copied actor (<see cref="ActorCapture"/>), selected, in the Place Actors tab's level
    /// (<see cref="PlacementFile"/>), as one undo step, made for that level as a copy in another level is (<see cref="ActorCloning"/>), whichever level it
    /// was copied from. It goes where the cursor meets the scene (as a click places an actor), as UE3's Paste Here puts copies at the click
    /// (UnrealEdSrv.cpp's EDIT PASTE TO=HERE); with the cursor away from the viewport, centred in front of the camera, far enough not to be around it.
    /// Why it can't is shown in the status bar, as Copy's is; that the copied actor is another game's, in a message box, as it can't be pasted into any
    /// of this window's levels
    /// </summary>
    /// <param name="menu">The menu Paste was chosen from: over the viewport, it goes where the menu was opened</param>
    /// <returns>True: it pasted, or said why it couldn't</returns>
    private bool PasteActor(ContextMenu menu)
    {
        if (FindPasteRefusal() is { } refusal)
        {
            StatusBar_LeftMostText.Text = $"Nothing was pasted. {refusal}";
            return true;
        }
        var (capture, destination) = (ActorClipboard.Copied, PlacementFile);
        if (ActorClipboard.GameRefusal(destination) is { } otherGame)
        {
            MessageBox.Show(this, otherGame, "Paste", MessageBoxButton.OK, MessageBoxImage.Warning);
            return true;
        }
        Point? cursor = menu is null ? CursorInViewport() : menu.PlacementTarget == SceneViewer ? viewportMenuPoint : null;
        IUndoAction step;
        string name;
        try
        {
            var view = copiedActors.ViewOf(capture);
            name = view.ActorName;
            Vector3 location;
            if (cursor is { } point) location = RenderContext.PlacementAt((int)point.X, (int)point.Y).Location;
            else
            {
                var camera = RenderContext.Camera;
                var bounds = view.GetBounds();
                location = camera.EyePosition + camera.CameraForward * (bounds.SphereRadius + 50) - (bounds.Origin - view.Location);
            }
            // (it cancels the copy if it fails)
            step = ActorCloning.BeginClone(view, location - view.Location, destination).Complete();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            StatusBar_LeftMostText.Text = $"Nothing was pasted. {error.Message}";
            return true;
        }
        OnDuplicateDragComplete(step);
        StatusBar_LeftMostText.Text = $"Pasted a copy of {name} into {destination.FileName}.";
        return true;
    }

    //the cursor's position in the viewport, if it's over it
    private Point? CursorInViewport()
    {
        if (!SceneViewer.IsMouseOver) return null;
        var point = Mouse.GetPosition(SceneViewer);
        return point.X >= 0 && point.Y >= 0 && point.X < SceneViewer.ActualWidth && point.Y < SceneViewer.ActualHeight ? point : null;
    }

    private void OnDuplicateDragComplete(IUndoAction step)
    {
        UndoHistory.Push(step);
        //the copy is selected, and its drag is part of the step
        _preEditSnapshot = selectedActor?.SnapshotTransform();
        LightDiagnosticsStale = true;
    }

    /// <summary>The games whose missing or out-of-date asset database an asset picker shouldn't ask about again while this editor is open</summary>
    private readonly HashSet<MEGame> declinedAssetDatabasePrompts = [];
}
