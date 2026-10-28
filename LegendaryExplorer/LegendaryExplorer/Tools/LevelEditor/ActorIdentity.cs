using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A logical actor of the editing session. It outlives the proxies that draw it (which can be disposed and rebuilt), and a draft actor has one
/// before it has an export. Never a UIndex or a hit ID
/// </summary>
public readonly record struct ActorId(Guid Value)
{
    public static ActorId New() => new(Guid.NewGuid());
    public bool IsEmpty => Value == Guid.Empty;
    public override string ToString() => IsEmpty ? "(none)" : Value.ToString("N")[..8];
}

/// <summary>
/// How an undo action refers to an actor: the view it was recorded against, and the actor's id, which merges compare. A view taken out of the scene
/// isn't disposed while a step can put it back, so the step is orphaned only once the view is disposed: its file closed or reloaded, or the
/// actor rebuilt from its export after another tool edited it
/// </summary>
public sealed class ActorRef(ActorProxy actor)
{
    public ActorId Id { get; } = actor.Id;

    /// <summary>The view the step edits. Throws once it's disposed</summary>
    public ActorProxy View => !actor.IsDisposed ? actor : throw new InvalidOperationException($"{actor.DisplayText} isn't in the scene.");

    public bool IsOrphaned => actor.IsDisposed;

    public bool RefersTo(ActorRef other) => other is not null && other.Id == Id;
}

/// <summary>
/// What the session knows about an actor apart from its view: which package it belongs to, its committed export if it has one,
/// and what kind of spline point it is. A record for a reference that resolves to no export keeps the stored UIndex, so writing it back changes nothing
/// </summary>
public sealed class ActorRecord
{
    public ActorId Id { get; }
    public IMEPackage Package { get; }
    /// <summary>The committed export, or null for a draft or an unresolved reference</summary>
    public ExportEntry Export { get; }
    /// <summary>For an unresolved reference: the UIndex that was stored (an import, or a missing export)</summary>
    public int StoredUIndex { get; }
    /// <summary>The actor's class, for a draft (an export's is its own)</summary>
    public IEntry Class { get; }
    public string Name { get; }
    public bool IsSpline { get; private set; }
    public bool IsLoft { get; private set; }

    internal void Invalidate()
    {
        //the edit may have changed its class
        if (Export is not null)
        {
            IsSpline = Export.IsA("SplineActor");
            IsLoft = Export.IsA("SplineLoftActor");
        }
    }

    public bool IsDraft => Export is null && StoredUIndex == 0;
    public bool IsUnresolved => StoredUIndex != 0;

    internal ActorRecord(ActorId id, ExportEntry export)
    {
        Id = id;
        Package = export.FileRef;
        Export = export;
        Class = export.Class;
        Name = export.ObjectName.Instanced;
        IsSpline = export.IsA("SplineActor");
        IsLoft = export.IsA("SplineLoftActor");
    }

    internal ActorRecord(ActorId id, IMEPackage package, int storedUIndex)
    {
        Id = id;
        Package = package;
        StoredUIndex = storedUIndex;
        Name = package.GetEntry(storedUIndex)?.InstancedFullPath ?? $"#{storedUIndex}";
    }

    internal ActorRecord(ActorId id, IMEPackage package, IEntry @class, string name, bool isSpline, bool isLoft)
    {
        Id = id;
        Package = package;
        Class = @class;
        Name = name;
        IsSpline = isSpline;
        IsLoft = isLoft;
    }
}

/// <summary>
/// The session's actors by <see cref="ActorId"/>. An export is given an id the first time anything refers to it, and keeps it for as long as that
/// export object exists, so a proxy rebuilt from it (after a preview refresh) is the same actor. A reopened package has new export objects, so new ids
/// </summary>
public sealed class ActorRegistry
{
    private readonly Dictionary<ActorId, ActorRecord> records = [];
    private readonly Dictionary<ExportEntry, ActorId> byExport = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(IMEPackage, int), ActorId> unresolved = [];

    public ActorRecord this[ActorId id] => records.GetValueOrDefault(id);

    /// <summary>The id of the actor an export is, given one if it has none yet</summary>
    public ActorId IdFor(ExportEntry export)
    {
        if (byExport.TryGetValue(export, out ActorId id)) return id;
        id = ActorId.New();
        byExport[export] = id;
        records[id] = new ActorRecord(id, export);
        return id;
    }

    /// <summary>
    /// The id for a stored object reference: the export's, or a record keeping the value if it resolves to no export. Empty for 0 (no object)
    /// </summary>
    public ActorId IdForStored(IMEPackage package, int uIndex)
    {
        if (uIndex == 0) return default;
        if (package.TryGetUExport(uIndex, out ExportEntry export)) return IdFor(export);
        if (unresolved.TryGetValue((package, uIndex), out ActorId id)) return id;
        id = ActorId.New();
        unresolved[(package, uIndex)] = id;
        records[id] = new ActorRecord(id, package, uIndex);
        return id;
    }

    /// <summary>A new actor that has no export: drawn and edited in the editor, and only written to its package by a commit</summary>
    public ActorRecord CreateDraft(IMEPackage package, IEntry @class, string name, bool isSpline, bool isLoft)
    {
        var record = new ActorRecord(ActorId.New(), package, @class, name, isSpline, isLoft);
        records[record.Id] = record;
        return record;
    }

    /// <summary>
    /// The UIndex a reference to the actor is stored as: its export's, or the stored value of an unresolved reference. 0 for no actor.
    /// A draft has none until it's committed
    /// </summary>
    public int StoredUIndex(ActorId id)
    {
        if (id.IsEmpty) return 0;
        ActorRecord record = this[id] ?? throw new InvalidOperationException($"Actor {id} isn't part of this session.");
        if (record.IsDraft) throw new InvalidOperationException($"{record.Name} hasn't been committed, so it has no export to refer to.");
        return record.Export?.UIndex ?? record.StoredUIndex;
    }

    /// <summary>The stored UIndex, or 0 for a draft or no actor</summary>
    public int StoredUIndexOrZero(ActorId id) => this[id] is { } record ? record.Export?.UIndex ?? record.StoredUIndex : 0;

    /// <summary>The actor's current name (its export's, or a draft's proposed one). Null for no actor or an unresolved reference</summary>
    public string NameOf(ActorId id) => this[id] is { IsUnresolved: false } record ? record.Export?.ObjectName.Instanced ?? record.Name : null;

    public bool Contains(ActorId id) => records.ContainsKey(id);

    /// <summary>
    /// The export changed outside the editor (another tool edited it) and is being rebuilt from it. It keeps its id, so references to it from other
    /// actors (spline connections and backlinks) still resolve; its class is read again. Earlier undo steps are orphaned with its old view
    /// </summary>
    public void Invalidate(ExportEntry export)
    {
        if (export is not null && byExport.TryGetValue(export, out ActorId id)) records[id].Invalidate();
    }

    /// <summary>Forgets a closed package's actors. Anything still holding their ids finds no record</summary>
    public void Forget(IMEPackage package)
    {
        foreach ((ActorId id, ActorRecord record) in records)
        {
            if (record.Package == package) records.Remove(id);
        }
        foreach (ExportEntry export in new List<ExportEntry>(byExport.Keys))
        {
            if (export.FileRef == package) byExport.Remove(export);
        }
        foreach ((IMEPackage pcc, int uIndex) in new List<(IMEPackage, int)>(unresolved.Keys))
        {
            if (pcc == package) unresolved.Remove((pcc, uIndex));
        }
    }
}
