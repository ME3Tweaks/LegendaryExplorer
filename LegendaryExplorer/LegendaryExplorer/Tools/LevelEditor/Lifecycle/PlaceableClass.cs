using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using FontAwesome5;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using ClassFlags = LegendaryExplorerCore.Unreal.UnrealFlags.EClassFlags;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>A group of the classes that can be placed, as the Place Actors list shows them</summary>
public sealed record PlacementCategory(string Name, EFontAwesomeIcon Icon)
{
    public static readonly PlacementCategory StaticMeshes = new("Static meshes", EFontAwesomeIcon.Solid_Cube);
    public static readonly PlacementCategory Dynamics = new("Dynamics", EFontAwesomeIcon.Solid_DoorOpen);
    public static readonly PlacementCategory Lights = new("Lights", EFontAwesomeIcon.Solid_Lightbulb);
    public static readonly PlacementCategory Volumes = new("Volumes", EFontAwesomeIcon.Solid_VectorSquare);
    public static readonly PlacementCategory Effects = new("Effects", EFontAwesomeIcon.Solid_Magic);
    public static readonly PlacementCategory Splines = new("Splines", EFontAwesomeIcon.Solid_BezierCurve);
    public static readonly PlacementCategory Markers = new("Markers", EFontAwesomeIcon.Solid_MapMarkerAlt);
}

/// <summary>A class that can be placed (<see cref="ActorCreation.PlaceableClasses"/>)</summary>
/// <param name="Description">What it is, in a line</param>
/// <param name="Asset">The kind of asset chosen before placing one, or null if it needs none</param>
public sealed record PlaceableClass(string ClassName, PlacementCategory Category, string Description, AssetKind? Asset);
