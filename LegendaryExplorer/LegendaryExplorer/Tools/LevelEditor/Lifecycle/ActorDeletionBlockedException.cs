using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>An export whose references block a deletion</summary>
public sealed class BlockingReferrer(ExportEntry export, IReadOnlyList<string> references)
{
    public ExportEntry Export { get; } = export;
    /// <summary>Where it refers to what</summary>
    public IReadOnlyList<string> References { get; } = references;
    public string Name { get; } = $"#{export.UIndex} {export.InstancedFullPath} ({export.ClassName})";

    /// <summary>
    /// Whether it's a Kismet object, which can be opened in the Sequence Editor. That's also where a Matinee's references are: its tracks don't refer
    /// to actors, but its SeqAct_Interp binds a group to the Kismet variables on the link named after it. Anything else opens in the Package Editor
    /// </summary>
    public bool OpensInSequenceEditor { get; } = export.IsA("SequenceObject");

    /// <summary>Whether it's been trashed since, which takes its references with it</summary>
    public bool IsGone => Export.IsTrash();
}

/// <summary>Thrown for a deletion that's blocked, with what blocks it in a form that can be acted on</summary>
public sealed class ActorDeletionBlockedException(ActorDeletionPlan plan) : InvalidOperationException($"Cannot delete {plan.Actor.ObjectName.Instanced}:\n{ActorDeletionPlan.Listed(plan.Blockers)}")
{
    /// <summary>The exports whose references block it</summary>
    public IReadOnlyList<BlockingReferrer> Referrers { get; } = plan.GetBlockingReferrers();
    /// <summary>What else blocks it</summary>
    public IReadOnlyList<string> OtherBlockers { get; } = plan.OtherBlockers;
    public string ActorName { get; } = plan.Actor.ObjectName.Instanced;
}
