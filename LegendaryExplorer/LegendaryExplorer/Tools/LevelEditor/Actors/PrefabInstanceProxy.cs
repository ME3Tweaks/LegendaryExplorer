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

/// <summary>
/// A placed prefab's record, as a marker: UE3's editor uses it to update a level's actors from the prefab. Its members are ordinary actors of the level (every
/// live one in LE1-LE3's levels is), which draw themselves. Its template isn't drawn: it lists what the level may no longer have, since a blank entry in the
/// instance's map is a member that was deleted. It can't be moved, since that wouldn't move its members
/// </summary>
public sealed class PrefabInstanceProxy(IActorEditorContext context, ExportEntry actorExport)
    : EditorActorProxy(context, actorExport, new(ActorKind.Prefab, "Prefabs", "A placed prefab's record. Its members are ordinary actors of the level."))
{
    public override bool CanEditTransform => false;
}
