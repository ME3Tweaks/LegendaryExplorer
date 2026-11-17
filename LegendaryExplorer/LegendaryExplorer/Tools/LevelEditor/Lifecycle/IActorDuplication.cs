using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A copy of an actor, being moved before it's made (an Alt+drag) or placed at once (Duplicate). Move <see cref="Copy"/>; then complete or cancel it
/// </summary>
public interface IActorDuplication
{
    /// <summary>The draft copy: drawn, selected and movable until the duplicate completes or is cancelled</summary>
    ActorProxy Copy { get; }

    /// <summary>Takes on where the copy has been moved (a spline point's connection follows it)</summary>
    void Follow();

    /// <summary>
    /// Finishes it as one undo step for the caller to record. A copy left exactly where its source is isn't a duplicate: it's cancelled, and there's no
    /// step. If it fails, it's cancelled, and the error is thrown
    /// </summary>
    IUndoAction Complete();

    /// <summary>Puts everything back as it was, and discards the copy</summary>
    void Cancel();
}
