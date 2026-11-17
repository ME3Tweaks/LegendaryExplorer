using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.Unreal.PhysX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

public class SFXDroppedPickupProxy : ActorProxy
{
    public SkeletalMeshComponentProxy PickupMesh;
    public SFXDroppedPickupProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref PickupMesh);
    }
}

public class SFXDroppedAmmoProxy : ActorProxy
{
    public StaticMeshComponentProxy AmmoMesh;
    public SFXDroppedAmmoProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref AmmoMesh);
    }
}

public class SFXDroppedGrenadeProxy : ActorProxy
{
    public StaticMeshComponentProxy GrenadeMesh;
    public SFXDroppedGrenadeProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref GrenadeMesh);
    }
}
