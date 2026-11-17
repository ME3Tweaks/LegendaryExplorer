using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>The selected spline point's values, for the details panel. Only the tangent applies to a plain spline point</summary>
public sealed class EditableSplinePoint(SplineActorProxy owner) : NotifyPropertyChangedBase
{
    public bool IsEditable => !owner.IsReadOnly;
    public bool IsLoft => owner.IsLoft;
    private SplineLoftSettings S => owner.Settings;
    private void Set(SplineLoftSettings value, string property) => owner.ApplySettings(value, property);

    public float TangentX { get => S.Tangent.X; set { if (float.IsFinite(value)) Set(S with { Tangent = S.Tangent with { X = value } }, "tangent"); } }
    public float TangentY { get => S.Tangent.Y; set { if (float.IsFinite(value)) Set(S with { Tangent = S.Tangent with { Y = value } }, "tangent"); } }
    public float TangentZ { get => S.Tangent.Z; set { if (float.IsFinite(value)) Set(S with { Tangent = S.Tangent with { Z = value } }, "tangent"); } }
    //the world tangent as the gizmo's grabbers change it: a length, and a heading and elevation in degrees
    private Vector3 WorldTangent => owner.Node.WorldTangent;
    public float TangentLength
    {
        get => WorldTangent.Length();
        set
        {
            if (!float.IsFinite(value) || value < 0) return;
            Vector3 tangent = WorldTangent;
            // As with the gizmo, a point without a tangent starts along its own X axis.
            if (tangent == Vector3.Zero) tangent = Vector3.TransformNormal(Vector3.UnitX, owner.Node.LocalToWorld);
            float length = tangent.Length();
            if (length > 0) owner.SetWorldTangent(tangent * (value / length), "tangent");
        }
    }
    public float TangentYaw
    {
        get => MathF.Atan2(WorldTangent.Y, WorldTangent.X) * 180 / MathF.PI;
        set { if (float.IsFinite(value)) SetTangentDirection(value, TangentPitch); }
    }
    public float TangentPitch
    {
        get => MathF.Atan2(WorldTangent.Z, new Vector2(WorldTangent.X, WorldTangent.Y).Length()) * 180 / MathF.PI;
        set { if (float.IsFinite(value)) SetTangentDirection(TangentYaw, Math.Clamp(value, -90, 90)); }
    }

    private void SetTangentDirection(float yaw, float pitch)
    {
        (float sinYaw, float cosYaw) = MathF.SinCos(yaw * MathF.PI / 180);
        (float sinPitch, float cosPitch) = MathF.SinCos(pitch * MathF.PI / 180);
        owner.SetWorldTangent(new Vector3(cosPitch * cosYaw, cosPitch * sinYaw, sinPitch) * TangentLength, "tangent");
    }

    public float Roll { get => S.Roll; set { if (float.IsFinite(value)) Set(S with { Roll = value }, nameof(Roll)); } }
    public float ScaleX { get => S.Scale.X; set { if (float.IsFinite(value)) Set(S with { Scale = S.Scale with { X = value } }, nameof(ScaleX)); } }
    public float ScaleY { get => S.Scale.Y; set { if (float.IsFinite(value)) Set(S with { Scale = S.Scale with { Y = value } }, nameof(ScaleY)); } }
    public float OffsetX { get => S.Offset.X; set { if (float.IsFinite(value)) Set(S with { Offset = S.Offset with { X = value } }, "offset"); } }
    public float OffsetY { get => S.Offset.Y; set { if (float.IsFinite(value)) Set(S with { Offset = S.Offset with { Y = value } }, "offset"); } }
    public float WorldXDirX { get => S.WorldXDir.X; set { if (float.IsFinite(value)) Set(S with { WorldXDir = S.WorldXDir with { X = value } }, "WorldXDir"); } }
    public float WorldXDirY { get => S.WorldXDir.Y; set { if (float.IsFinite(value)) Set(S with { WorldXDir = S.WorldXDir with { Y = value } }, "WorldXDir"); } }
    public float WorldXDirZ { get => S.WorldXDir.Z; set { if (float.IsFinite(value)) Set(S with { WorldXDir = S.WorldXDir with { Z = value } }, "WorldXDir"); } }
    public bool SmoothInterpRollAndScale { get => S.SmoothInterpRollAndScale; set => Set(S with { SmoothInterpRollAndScale = value }, "interpolation"); }

    internal void NotifySettings()
    {
        foreach (string name in new[] { nameof(TangentX), nameof(TangentY), nameof(TangentZ), nameof(TangentLength), nameof(TangentYaw), nameof(TangentPitch), nameof(Roll), nameof(ScaleX), nameof(ScaleY), nameof(OffsetX),
                     nameof(OffsetY), nameof(WorldXDirX), nameof(WorldXDirY), nameof(WorldXDirZ), nameof(SmoothInterpRollAndScale), nameof(IsEditable) })
            OnPropertyChanged(name);
    }

    /// <summary>Turning or scaling the point turns or scales its world tangent</summary>
    internal void NotifyWorldTangent()
    {
        foreach (string name in new[] { nameof(TangentLength), nameof(TangentYaw), nameof(TangentPitch) }) OnPropertyChanged(name);
    }
}

public sealed class SplineSettingsAction : IMergeableUndoAction
{
    private readonly ActorRef actor;
    private readonly SplineLoftSettings before;
    private readonly string property;
    private SplineLoftSettings latest;
    private DateTime time = DateTime.UtcNow;

    public SplineSettingsAction(SplineActorProxy actor, SplineLoftSettings before, SplineLoftSettings after, string property)
    {
        this.actor = new ActorRef(actor);
        this.before = before;
        this.property = property;
        latest = after;
    }

    private SplineActorProxy Point => (SplineActorProxy)actor.View;
    public string Description => $"Edit spline {property}";
    public IEnumerable<AssetReference> Assets => [];
    public bool IsOrphaned => actor.IsOrphaned;
    public IReadOnlyCollection<ActorId> Subjects => [actor.Id];
    public void Undo() => Point.RestoreSettings(before);
    public void Redo() => Point.RestoreSettings(latest);
    public bool TryMerge(IUndoAction next)
    {
        if (next is not SplineSettingsAction action || !action.actor.RefersTo(actor) || action.property != property
            || action.time - time > TimeSpan.FromMilliseconds(500)) return false;
        latest = action.latest; time = action.time; return true;
    }
}
