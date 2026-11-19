using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>What a level scan looks for, in the order the Problems tab lists them</summary>
public enum LevelCheck
{
    ChangedSinceBake, UnbakedMeshes, SharedLightGuids, DecalReceivers, DecalWithoutReceivers, DecalMaterial, ActorList, TrashReferences, MissingAssets,
    VolumeCollision
}

/// <summary>A problem breaks something in game; a warning is something that works, but not as the level was made to</summary>
public enum ProblemSeverity { Problem, Warning }

/// <summary>
/// Something <see cref="LevelScan"/> found in <see cref="Package"/>: what it's about (<see cref="Subject"/>), and what it does in game. Each is its own
/// (reference equality), so that two that read the same are still two rows of the Problems tab
/// </summary>
public sealed class LevelProblem(LevelCheck check, ProblemSeverity severity, IMEPackage package, string subject, string message)
{
    public LevelCheck Check { get; } = check;
    public ProblemSeverity Severity { get; } = severity;
    public IMEPackage Package { get; } = package;
    public string Subject { get; } = subject;
    public string Message { get; } = message;

    /// <summary>The actor it's about, as it was shown when the scan ran. Null if it's about another object</summary>
    public ActorProxy Actor { get; init; }

    /// <summary>The actor's export, to find its view by once that view is rebuilt. Null for a new actor (a draft)</summary>
    public ExportEntry Export { get; init; }

    public string FileName => Path.GetFileName(Package.FilePath);
    public bool IsWarning => Severity is ProblemSeverity.Warning;
    public string CheckTitle => LevelScan.Title(Check);
}
