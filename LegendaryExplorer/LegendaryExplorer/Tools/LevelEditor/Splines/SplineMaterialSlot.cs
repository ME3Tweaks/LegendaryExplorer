using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

public sealed record SplineMaterialSlot(SplineActorProxy Point, int Index, string Name);
