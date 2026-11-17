using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

public class PawnProxy : ActorProxy
{
    public SkeletalMeshComponentProxy Mesh;
    public PawnProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref Mesh);
    }

    public override void SetAnimation(AnimSequence animSequence, float pos)
    {
        Mesh?.SetAnimation(animSequence, pos);
    }
}

public class BioPawnProxy : PawnProxy
{
    public SkeletalMeshComponentProxy HeadMesh;
    public SkeletalMeshComponentProxy m_oHairMesh;
    public SkeletalMeshComponentProxy m_oHeadGearMesh;
    public SkeletalMeshComponentProxy m_oVisorMesh;
    public SkeletalMeshComponentProxy m_oFacePlateMesh;
    public List<SkeletalMeshComponentProxy> m_aoAccessories = [];

    public BioPawnProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref HeadMesh, actorExport.Game.IsGame1() ? "m_oHeadMesh" : nameof(HeadMesh));
        ApplyMorphFace(HeadMesh);
        AddComponent(context.RenderContext, ref m_oHairMesh);
        AddComponent(context.RenderContext, ref m_oHeadGearMesh);
        AddComponent(context.RenderContext, ref m_oVisorMesh);
        AddComponent(context.RenderContext, ref m_oFacePlateMesh);
        AddComponentArray(context.RenderContext, ref m_aoAccessories);
    }

    public override void SetAnimation(AnimSequence animSequence, float pos)
    {
        base.SetAnimation(animSequence, pos);
        HeadMesh?.SetAnimation(animSequence, pos);
        m_oHairMesh?.SetAnimation(animSequence, pos);
        m_oHeadGearMesh?.SetAnimation(animSequence, pos);
        m_oVisorMesh?.SetAnimation(animSequence, pos);
        m_oFacePlateMesh?.SetAnimation(animSequence, pos);
        foreach (var accessory in m_aoAccessories)
        {
            accessory?.SetAnimation(animSequence, pos);
        }
    }
}
