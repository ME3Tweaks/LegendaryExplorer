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

public class BioArtPlaceableProxy : ActorProxy
{
    public MeshComponentProxy PlaceableMesh;
    public MeshComponentProxy DestroyedMesh;
    public BioArtPlaceableProxy(IActorEditorContext context, ExportEntry actorExport) : base(context, actorExport)
    {
        AddComponent(context.RenderContext, ref PlaceableMesh);
        AddComponent(context.RenderContext, ref DestroyedMesh);
        if (PlaceableMesh is not null)
        {
            DestroyedMesh?.IsVisible = false;
        }
    }
}
