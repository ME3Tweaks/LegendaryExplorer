using LegendaryExplorer.Tools.LevelEditor;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector3>;
using InterpCurvePointVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<System.Numerics.Vector3>;
using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;
using InterpCurvePointFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurvePoint<float>;
using System.Numerics;

namespace LegendaryExplorer.Tools.InterpEditor;

public class SeqAct_Interp
{
    public ExportEntry Export;
    public InterpData InterpData;

    public Dictionary<NameReference, List<ExportEntry>> GroupActorVars = [];

    public SeqAct_Interp(ExportEntry export, InterpData interpData = null)
    {
        Export = export;
        InterpData = interpData;
        var props = export.GetCondensedProperties();
        var varLinks = KismetHelper.GetVariableLinks(props, export.FileRef);
        if (varLinks.Count > 0)
        {
            foreach (var varLinkInfo in varLinks)
            {
                if (varLinkInfo.LinkedNodes.Count > 0)
                {
                    if (varLinkInfo.LinkDesc.CaseInsensitiveEquals("Data"))
                    {
                        if (interpData is null && varLinkInfo.LinkedNodes[0] is ExportEntry interpDataExport)
                        {
                            InterpData = new InterpData(interpDataExport);
                        }
                        continue;
                    }
                    var linkedExports = varLinkInfo.LinkedNodes.SelectWhereNonNull(entry => entry as ExportEntry).ToList();
                    if (linkedExports.Count > 0)
                    {
                        GroupActorVars[NameReference.FromInstancedString(varLinkInfo.LinkDesc)] = linkedExports;
                    }
                }
            }
        }
        if (InterpData is null)
        {
            throw new InvalidOperationException("SeqAct_Interp does not have an InterpData link");
        }
        InterpData.Parent = this;
    }
}

public sealed partial class InterpData : IDisposable
{
    public SeqAct_Interp Parent;
    public LevelEditor.LevelEditor LevelEditor;

    public void AttachToLevelEditor(LevelEditor.LevelEditor levelEditor)
    {
        LevelEditor = levelEditor;
        foreach (var group in Groups)
        {
            group.PrepMatinee();
        }
    }

    public void Step(float newTime, float prevTime)
    {
        foreach (var group in Groups)
        {
            group.Step(newTime, prevTime);
        }
    }

    public void StopMatinee()
    {
        foreach (var group in Groups)
            group.StopMatinee();
    }

    public ActorProxy GetOrCreateTaggedActor(NameReference tag)
    {
        foreach (var actorProxy in LevelEditor.Actors)
        {
            if (actorProxy.Tag == tag)
            {
                return actorProxy;
            }
        }
        var actorExport = Export.FileRef.FindActorByTag(tag, true);
        if (actorExport != null)
        {
            GetOrCreateActor(actorExport);
        }
        return null;
    }

    public ActorProxy GetOrCreateActor(ExportEntry actorExport)
    {
        foreach (var actorProxy in LevelEditor.Actors)
        {
            if (actorProxy.Export == actorExport)
            {
                return actorProxy;
            }
        }
        ConditionalAddToAuxiliaryPackages(actorExport);
        var actor = ActorProxy.Create(LevelEditor, actorExport);
        LevelEditor.AddActor(actor);
        return actor;
    }

    private readonly DisposableCollection<IMEPackage> AuxiliaryPackages = [];
    private void ConditionalAddToAuxiliaryPackages(ExportEntry actorExport)
    {
        if (actorExport.FileRef != Export.FileRef && AuxiliaryPackages.All(pcc => pcc != actorExport.FileRef))
        {
            AuxiliaryPackages.Add(MEPackageHandler.OpenMEPackage(actorExport.FileRef));
        }
    }

    public void Dispose()
    {
        AuxiliaryPackages.Dispose();
    }
}


public partial class InterpGroup
{
    public InterpData Parent;

    //can technically be multiple, but that only matters for some track types
    private List<ActorProxy> _groupActors;
    public IEnumerable<ActorProxy> GroupActors => _groupActors ??= GroupActor is not null ? [GroupActor] : [];

    private ActorProxy _groupActor;
    public ActorProxy GroupActor => _groupActor;

    public bool HasResolvedActor => _groupActor is not null;

    private ActorProxy _manualGroupActor;
    public ActorProxy ManualGroupActor
    {
        get => _manualGroupActor;
        set
        {
            _manualGroupActor = value;
            _groupActor = value;
            _groupActors = null;
            OnPropertyChanged(nameof(HasResolvedActor));
        }
    }

    private void ResolveGroupActor()
    {
        _groupActors = null;
        _groupActor = null;
        if (_manualGroupActor is not null)
        {
            _groupActor = _manualGroupActor;
            OnPropertyChanged(nameof(HasResolvedActor));
            return;
        }
        var props = Export.GetCondensedProperties();
        ESFXFindByTagTypes findActorMode = props.GetProp<EnumProperty>("m_eSFXFindActorMode").GetEnumValOrDefault(ESFXFindByTagTypes.FindActorByTag);
        switch (findActorMode)
        {
            case ESFXFindByTagTypes.UseGroupActor:
                if (Parent.Parent is not null
                    && props.GetProp<NameProperty>("GroupName")?.Value is NameReference groupName
                    && groupName != NameReference.None && Parent.Parent.GroupActorVars.TryGetValue(groupName, out List<ExportEntry> groupActorVars))
                {
                    for (var i = 0; i < groupActorVars.Count; i++)
                    {
                        IEntry objVarValue = KismetHelper.ResolveObjectVarValue(groupActorVars[i], true);
                        if (objVarValue is ExportEntry actorExport && actorExport.IsA("Actor"))
                        {
                            var actor = Parent.GetOrCreateActor(actorExport);
                            if (_groupActor is null)
                            {
                                _groupActor = actor;
                            }
                            else
                            {
                                if (_groupActors == null)
                                {
                                    _groupActors = [];
                                    _groupActors.Add(_groupActor);
                                }
                                _groupActors.Add(actor);
                            }
                        }
                    }
                }
                return;
            case ESFXFindByTagTypes.FindActorByNode:
                //no idea how this one works, will have to see an example
                if (App.IsDebug && Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                return;
            case ESFXFindByTagTypes.FindActorByTag:
            default:
                if (props.GetProp<NameProperty>("m_nmSFXFindActor")?.Value is NameReference actorTag && actorTag != NameReference.None)
                {
                    _groupActor = Parent.GetOrCreateTaggedActor(actorTag);
                }
                //fallback, not sure if this is actually how the game behaves
                goto case ESFXFindByTagTypes.UseGroupActor;
        }
    }

    private Dictionary<NameReference, AnimSequence> GroupAnims;

    public void PrepMatinee()
    {
        ResolveGroupActor();
        foreach (var actor in GroupActors)
            actor.BeginMatineeControl();
        foreach (var track in Tracks)
        {
            track.PrepMatinee();
        }
    }

    public void Step(float newTime, float prevTime)
    {
        foreach (var track in Tracks)
        {
            track.ConditionalStep(newTime, prevTime);
        }
    }

    public void StopMatinee()
    {
        foreach (var actor in GroupActors)
            actor.EndMatineeControl();
    }

    public AnimSequence GetAnim(NameReference name)
    {
        if (GroupAnims is null)
        {
            GroupAnims = [];
            var animSetsProp = Export.GetProperty<ArrayProperty<ObjectProperty>>("GroupAnimSets");
            if (animSetsProp != null)
            {
                using var cache = new PackageCache();
                foreach (ExportEntry animSet in animSetsProp.ResolveToExports(Export.FileRef, cache))
                {
                    var animSeqsProp = animSet.GetProperty<ArrayProperty<ObjectProperty>>("Sequences");
                    if (animSeqsProp is not null)
                    {
                        foreach (ExportEntry animSeqExport in animSeqsProp.ResolveToExports(Export.FileRef, cache))
                        {
                            if (animSeqExport.GetProperty<NameProperty>("SequenceName")?.Value is NameReference seqName
                                && ObjectBinary.From(animSeqExport, cache) is AnimSequence animSeq)
                            {
                                GroupAnims[seqName] = animSeq;
                            }
                        }
                    }
                }
            }
        }
        GroupAnims.TryGetValue(name, out var anim);
        return anim;
    }
}

public partial class InterpTrack
{
    protected PropertyCollection Props;

    //LE3/ME3 only
    private ETrackActiveCondition ActiveCondition;
    public bool IsDisabled;

    public void ConditionalStep(float newTime, float prevTime)
    {
        if (IsDisabled) return;
        Step(newTime, prevTime);
    }

    public virtual void Step(float newTime, float prevTime) { }

    public virtual void PrepMatinee()
    {
        Props = Export.GetCondensedProperties();
        ActiveCondition = Props.GetProp<EnumProperty>("ActiveCondition").GetEnumValOrDefault(ETrackActiveCondition.ETAC_Always);
    }
}

public partial class InterpTrackMove
{
    public InterpCurveVector PosTrack;
    public InterpCurveVector EulerTrack;
    public bool bUseQuatInterpolation;
    public EInterpTrackMoveFrame MoveFrame;
    public EInterpTrackMoveRotMode RotMode;

    public Vector3 InitialActorPosition;
    public Rotator InitialActorRotation;

    public override void Step(float newTime, float prevTime)
    {
        base.Step(newTime, prevTime);
        if (Group.GroupActor is not ActorProxy actor) return;

        var newPos = PosTrack.Eval(newTime, InitialActorPosition);

        Rotator newRot;
        if (bUseQuatInterpolation && EulerTrack.Points.Count >= 2)
        {
            // Find the two keys bracketing newTime
            var pts = EulerTrack.Points;
            int hi = 1;
            while (hi < pts.Count - 1 && pts[hi].InVal < newTime) hi++;
            var prev = pts[hi - 1];
            var next = pts[hi];

            Quaternion qPrev = Rotator.FromDegreesVector(prev.OutVal).ToQuaternion();
            Quaternion qNext = Rotator.FromDegreesVector(next.OutVal).ToQuaternion();

            // Ensure shortest-arc SLERP
            if (Quaternion.Dot(qPrev, qNext) < 0f) qNext = Quaternion.Negate(qNext);

            float t = next.InVal > prev.InVal
                ? Math.Clamp((newTime - prev.InVal) / (next.InVal - prev.InVal), 0f, 1f)
                : 0f;
            newRot = Rotator.FromQuaternion(Quaternion.Slerp(qPrev, qNext, t));
        }
        else
        {
            newRot = Rotator.FromDegreesVector(EulerTrack.Eval(newTime, InitialActorRotation.GetDegreesVector()));
        }

        actor.Location = newPos;
        actor.Rotation = newRot;
    }

    public override void PrepMatinee()
    {
        base.PrepMatinee();
        var emptyProp = new StructProperty("InterpCurveVector", false);
        PosTrack = InterpCurveVector.FromStructProperty(Props.GetProp<StructProperty>("PosTrack") ?? emptyProp, Export.Game);
        EulerTrack = InterpCurveVector.FromStructProperty(Props.GetProp<StructProperty>("EulerTrack") ?? emptyProp, Export.Game);

        bUseQuatInterpolation = Props.GetProp<BoolProperty>("bUseQuatInterpolation")?.Value ?? false;
        MoveFrame = Props.GetProp<EnumProperty>("MoveFrame").GetEnumValOrDefault(EInterpTrackMoveFrame.IMF_World);
        RotMode = Props.GetProp<EnumProperty>("RotMode").GetEnumValOrDefault(EInterpTrackMoveRotMode.IMR_Keyframed);

        //preload actor and capture its initial pose
        if (Group.GroupActor is ActorProxy actor)
        {
            InitialActorPosition = actor.Location;
            InitialActorRotation = actor.Rotation;
        }
    }
}

public partial class InterpTrackAnimControl
{
    private struct AnimKey
    {
        public NameReference AnimSeqName;
        public float StartTime;
        public float AnimStartOffset;
        public float AnimEndOffset;
        public float AnimPlayRate;
        public bool bLooping;
        public bool bReverse;
    }

    private List<AnimKey> AnimKeys;
    private float FirstAnimStart;

    public override void Step(float newTime, float prevTime)
    {
        base.Step(newTime, prevTime);
        if (AnimKeys.Count is 0) return;
        if (Group.GroupActor is not ActorProxy actor) return;

        if (newTime < FirstAnimStart)
        {
            //UE3 behavior: prior to the first key, Actors under AnimControl are set to the initial pose of the first anim
            newTime = FirstAnimStart;
        }
        AnimKey current = AnimKeys[0];
        for (int i = 1; i < AnimKeys.Count; i++)
        {
            if (AnimKeys[i].StartTime > newTime)
            {
                break;
            }
            current = AnimKeys[i];
        }
        AnimSequence anim = Group.GetAnim(current.AnimSeqName);
        if (anim is null)
        {
            //broken interp, nothing we can do.
            actor.SetAnimation(null, 0);
            return;
        }
        float animLength = anim.SequenceLength - current.AnimStartOffset + current.AnimEndOffset;
        float animEnd = anim.SequenceLength + current.AnimEndOffset;
        float animTime = (newTime - current.StartTime) * current.AnimPlayRate;
        float animPos;
        if (animLength <= 0)
        {
            animPos = current.AnimStartOffset;
        }
        else if (current.bReverse)
        {
            animPos = animEnd - animTime;
            if (animPos < current.AnimStartOffset)
            {
                if (current.bLooping)
                {
                    animPos = animEnd - (animTime % animLength);
                }
                else
                {
                    //set to start
                    animPos = current.AnimStartOffset;
                }
            }
        }
        else
        {
            animPos = current.AnimStartOffset + animTime;
            if (animPos > animEnd)
            {
                if (current.bLooping)
                {
                    animPos = current.AnimStartOffset + (animTime % animLength);
                }
                else
                {
                    //set to end
                    animPos = animEnd;
                }
            }
        }
        //shouldn't be outside these values, but just in case...
        animPos = Math.Clamp(animPos, 0, anim.SequenceLength);

        actor.SetAnimation(anim, animPos);
    }

    public override void PrepMatinee()
    {
        base.PrepMatinee();
        AnimKeys = [];
        if (Props.GetProp<ArrayProperty<StructProperty>>("AnimSeqs") is { } animKeys)
        {
            //preload anims
            Group.GetAnim(NameReference.None);

            foreach (StructProperty animKeyProp in animKeys)
            {
                AnimKeys.Add(new AnimKey
                {
                    AnimSeqName = animKeyProp.GetProp<NameProperty>("AnimSeqName")?.Value ?? "None",
                    StartTime = animKeyProp.GetProp<FloatProperty>("StartTime")?.Value ?? 0,
                    AnimStartOffset = animKeyProp.GetProp<FloatProperty>("AnimStartOffset")?.Value ?? 0,
                    AnimEndOffset = animKeyProp.GetProp<FloatProperty>("AnimEndOffset")?.Value ?? 0,
                    AnimPlayRate = animKeyProp.GetProp<FloatProperty>("AnimPlayRate")?.Value ?? 1,
                    bLooping = animKeyProp.GetProp<BoolProperty>("bLooping")?.Value ?? false,
                    bReverse = animKeyProp.GetProp<BoolProperty>("bReverse")?.Value ?? false,
                });
            }
            if (AnimKeys.Count > 0)
            {
                FirstAnimStart = AnimKeys[0].StartTime;
            }
        }
    }
}