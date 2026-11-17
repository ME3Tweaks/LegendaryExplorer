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

/// <summary>A directed connection in the single selected point's details panel.</summary>
public sealed class SplineConnectionItem(SplineActorProxy source, SplineActorProxy target, string targetName)
{
    public SplineActorProxy Source { get; } = source;
    public SplineActorProxy Target { get; } = target;
    public string Name => targetName ?? "Missing destination";
    public bool IsEditable => Target is not null && !Source.IsReadOnly && !Target.IsReadOnly;
    public bool CanReverse => IsEditable && Target.CanConnectTo(Source);
    public void Break() => Source.BreakConnection(Target);
    public void Reverse() => Source.ConnectOrFlip(Target);
    public void Straighten() => Source.StraightTangents(Target);
}
