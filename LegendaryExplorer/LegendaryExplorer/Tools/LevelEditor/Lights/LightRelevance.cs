using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A light that's relevant to the selected mesh, for the details panel
/// </summary>
/// <param name="LightActor">The light's proxy, to select it. Null if it has none</param>
public sealed record LightRelevance(string LightName, string LevelName, string Status, bool IsAffecting, ActorProxy LightActor);
