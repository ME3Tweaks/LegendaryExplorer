using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

public class SFXStuntActorProxy : ActorProxy
{
    public SkeletalMeshComponentProxy BodyMesh;
    public SkeletalMeshComponentProxy HeadMesh;
    public SkeletalMeshComponentProxy HairMesh;
    public SkeletalMeshComponentProxy HeadGearMesh;
    public SFXStuntActorProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref BodyMesh);
        ApplyDefaultBodyTranslation();
        AddComponent(context.RenderContext, ref HeadMesh);
        ApplyMorphFace(HeadMesh);
        AddComponent(context.RenderContext, ref HairMesh);
        AddComponent(context.RenderContext, ref HeadGearMesh);
    }

    private void ApplyDefaultBodyTranslation()
    {
        if (BodyMesh.Translation == Vector3.Zero)
        {
            //from the defaultproperties, which condenseproperties currently does not fetch
            BodyMesh.Translation = new Vector3(0, 0, -88);
        }
    }

    internal override void OnComponentReloaded(SkeletalMeshComponentProxy component)
    {
        if (component == BodyMesh) ApplyDefaultBodyTranslation();
        base.OnComponentReloaded(component);
    }

    public override void SetAnimation(AnimSequence animSequence, float pos)
    {
        BodyMesh?.SetAnimation(animSequence, pos);
        HeadMesh?.SetAnimation(animSequence, pos);
        HairMesh?.SetAnimation(animSequence, pos);
        HeadGearMesh?.SetAnimation(animSequence, pos);
    }
}
