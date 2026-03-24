using CommunityToolkit.Diagnostics;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace LegendaryExplorerCore.Unreal.Animation;

public class FaceFxPlayer : AnimPlayer
{
    private const float FACEFX_EPSILON = 1.192092896e-07f;
    private static bool IsFaceFXZero(float f) => MathF.Abs(f) <= FACEFX_EPSILON;

    public override bool HasAnimation => FxActor is not null && Line is not null;

    private float length = 0;
    public override float Duration => length;

    private float startTime = 0;
    public override float StartTime => startTime;

    public override float EndTime => startTime + length;

    public FaceFXAsset FxActor
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            RebuildLookups();
        }
    }

    public FaceFXAnimSet AnimSet
    {
        get;
        set;
    }

    private FaceFXLine Line;

    private CaseInsensitiveDictionary<int> FaceGraphNodeLookup = [];
    private FaceFXBoneListEntry[] SkelMeshRefSkeletonToFaceFxBoneLookup;

    public FaceFxPlayer(SkeletalMesh skeletalMesh) : base(skeletalMesh)
    {
        SkelMeshRefSkeletonToFaceFxBoneLookup = new FaceFXBoneListEntry[skeletalMesh.RefSkeleton.Length];
    }

    public void SetFaceFXLine(FaceFXLine line)
    {
        if (line is null)
        {
            Line = null;
            length = 0;
            return;
        }
        bool inAnimSet = AnimSet != null && AnimSet.Lines.Contains(line);
        bool inActor = AnimSet is null && FxActor != null && FxActor.Lines.Contains(line);
        if (!inAnimSet && !inActor)
        {
            ThrowHelper.ThrowArgumentException("Line is not in AnimSet or FxActor");
        }
        Line = line;
        startTime = Line.Points.Min(p => p.time);
        length = line.Points.Max(p => p.time) - startTime;
        CurrentTime = startTime;
    }

    public override void SetCurrentTime(float time)
    {
        if (FxActor is null || AnimSet is null || Line is null)
        {
            CurrentTime = 0;
            return;
        }
        CurrentTime = time;
    }

    public override Matrix4x4[] ComputeSkinningMatrices()
    {
        if (FxActor is null || Line is null) return null;

        EvalFaceFxGraph();

        int numBones = _bones.Length;
        for (int i = 0; i < numBones; i++)
        {
            Matrix4x4 localTransform;
            var bone = _bones[i];

            if (SkelMeshRefSkeletonToFaceFxBoneLookup[i] is FaceFXBoneListEntry fxBoneListEntry)
            {
                var pos = fxBoneListEntry.RefBone.Position;
                var rot = fxBoneListEntry.RefBone.Rotation;
                var refBoneRot = rot;

                foreach (var link in fxBoneListEntry.Links)
                {
                    float value = FxActor.CompiledFaceGraph[link.GraphIndex].FinalValue;
                    if (!IsFaceFXZero(value))
                    {
                        pos += link.OptimizedBone.Position * value;
                        rot *= fxBoneListEntry.RefBoneInverseRot * Quaternion.Slerp(refBoneRot, link.OptimizedBone.Rotation, value);
                    }
                }
                rot = Quaternion.Normalize(rot);

                pos = pos with { Y = -pos.Y };
                rot = rot with { Y = -rot.Y };

                localTransform = Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
            }
            else
            {
                localTransform = Matrix4x4.CreateFromQuaternion(bone.Orientation) * Matrix4x4.CreateTranslation(bone.Position);
            }

            if (bone.ParentIndex >= 0 && bone.ParentIndex < i)
            {
                _boneComponentSpace[i] = localTransform * _boneComponentSpace[bone.ParentIndex];
            }
            else
            {
                _boneComponentSpace[i] = localTransform;
            }

            _skinningMatrices[i] = _inverseBindPose[i] * _boneComponentSpace[i];
        }

        return _skinningMatrices;
    }

    private void EvalFaceFxGraph()
    {
        if (Line.AnimationNames.Count != Line.NumKeys.Count)
        {
            ThrowHelper.ThrowInvalidDataException("FaceFxLine is in an invalid state!");
        }
        float time = CurrentTime;
        var nodes = FxActor.CompiledFaceGraph;
        int firstKey = 0;

        var namesList = AnimSet?.Names ?? FxActor.Names;
        //evaluate all curves
        for (int i = 0; i < Line.AnimationNames.Count; i++)
        {
            string lineName = namesList[Line.AnimationNames[i]];
            int numKeys = Line.NumKeys[i];
            //If the node is not in the FaceFxAsset, FaceFx ignores it
            if (FaceGraphNodeLookup.TryGetValue(lineName, out var nodeIndex))
            {
                nodes[nodeIndex].TrackValue = EvaluateAnim(time, CollectionsMarshal.AsSpan(Line.Points).Slice(firstKey, numKeys));
            }
            firstKey += numKeys;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.NodeType is FxNodeType.CurrentTime or FxNodeType.Delta)
            {
                ThrowHelper.ThrowInvalidDataException("Unexpected FaceFxNode type! Please inform the Legendary Explorer developers");
            }
            float value = node.InputLinks.Count is 0 ? 0f : node.InputOperation switch
            {
                FxInputOperation.Sum => 0f,
                FxInputOperation.Multiply => 1f,
                FxInputOperation.Max => float.MinValue,
                FxInputOperation.Min => float.MaxValue,
                _ => throw new InvalidDataException("Malformed FaceFxNode! Please inform the Legendary Explorer developers"),
            };
            float correction = 0;
            foreach (var link in node.InputLinks)
            {
                var inputNode = nodes[link.NodeIndex];

                float linkVal = 0f;
                float inputVal = inputNode.FinalValue;
                List<float> parms = link.Parameters;
                int numParms = parms.Count;
                switch (link.LinkFunction)
                {
                    case FxLinkFunction.Corrective:
                        float correctedValueRight = inputNode.FinalValue * inputNode.MaxValReciprocal;
                        float correctedValueLeft = -inputNode.FinalValue * inputNode.MinValReciprocal;
                        float correctedValue = MathF.Max(correctedValueRight, correctedValueLeft);
                        correction = MathF.Min(1f, correction + (correctedValue * (numParms is 0 ? 0f : parms[0])));
                        break;
                    case FxLinkFunction.Null:
                        linkVal = 0f;
                        break;
                    case FxLinkFunction.Linear:
                        linkVal = numParms switch
                        {
                            2 => inputVal * parms[0] + parms[1],
                            1 => inputVal * parms[0],
                            _ => inputVal
                        };
                        break;
                    case FxLinkFunction.Quadratic:
                        linkVal = MathF.CopySign(inputVal * inputVal, inputVal);
                        if (numParms is 1)
                        {
                            linkVal *= parms[0];
                        }
                        break;
                    case FxLinkFunction.Cubic:
                        linkVal = inputVal * inputVal * inputVal;
                        if (numParms is 1)
                        {
                            linkVal *= parms[0];
                        }
                        break;
                    case FxLinkFunction.SquareRoot:
                        if (inputVal != 0f)
                        {
                            linkVal = MathF.CopySign(MathF.Sqrt(MathF.Abs(inputVal)), inputVal);
                            if (parms.Count is 1)
                            {
                                linkVal *= parms[0];
                            }
                        }
                        break;
                    case FxLinkFunction.Negate:
                        linkVal = -inputVal;
                        break;
                    case FxLinkFunction.Inverse:
                        if (!IsFaceFXZero(inputVal))
                        {
                            linkVal = 1 / inputVal;
                        }
                        break;
                    case FxLinkFunction.OneClamp:
                        linkVal = inputVal <= 1f ? 1f : 1 / inputVal;
                        break;
                    case FxLinkFunction.Constant:
                        linkVal = numParms is 0 ? 1f : parms[0];
                        break;
                    case FxLinkFunction.ClampedLinear:
                        if (numParms is 4)
                        {
                            if (parms[3] > 0 && inputVal > parms[1] || parms[3] <= 0 && inputVal < parms[1])
                            {
                                linkVal = inputVal * parms[0] + (-1f * parms[0] * parms[1] + parms[2]);
                            }
                            else
                            {
                                linkVal = parms[2];
                            }
                        }
                        else if (numParms is 1)
                        {
                            linkVal = inputVal > 0 ? inputVal * parms[0] : 0;
                        }
                        else
                        {
                            linkVal = inputVal;
                        }
                        break;
                    case FxLinkFunction.Invalid:
                    default:
                        ThrowHelper.ThrowInvalidDataException("Malformed FaceFxNode! Please inform the Legendary Explorer developers");
                        break;
                }

                if (link.LinkFunction is not FxLinkFunction.Corrective)
                {
                    switch (node.InputOperation)
                    {
                        case FxInputOperation.Sum:
                            value += linkVal;
                            break;
                        case FxInputOperation.Multiply:
                            value *= linkVal;
                            break;
                        case FxInputOperation.Max:
                            value = MathF.Max(value, linkVal);
                            break;
                        case FxInputOperation.Min:
                            value = MathF.Min(value, linkVal);
                            break;
                        case FxInputOperation.Invalid:
                        default:
                            ThrowHelper.ThrowInvalidDataException("Malformed FaceFxNode! Please inform the Legendary Explorer developers");
                            break;
                    }
                }
            }

            value += node.TrackValue;
            node.TrackValue = 0;

            value *= (1f - correction);

            node.FinalValue = float.Clamp(value, node.MinVal, node.MaxVal);
        }


        float EvaluateAnim(float time, ReadOnlySpan<FaceFXControlPoint> keys)
        {
            if (keys.Length is 0)
            {
                return 0;
            }
            if (keys.Length is 1 || time <= keys[0].time)
            {
                return keys[0].weight;
            }
            if (time >= keys[^1].time)
            {
                return keys[^1].weight;
            }
            for (int i = keys.Length - 2; i >= 0; i--)
            {
                if (keys[i].time <= time)
                {
                    var first = keys[i];
                    var second = keys[i + 1];

                    //Hermite interpolation. FaceFx supports linear interpolation too, but that's never used in any of the ME games

                    float time1 = first.time;
                    float time2 = second.time;
                    float delta = time2 - time1;
                    float parametricTime = (time - time1) / delta;

                    float w1 = first.weight;
                    float w2 = second.weight;
                    float s1 = first.leaveTangent * delta;
                    float s2 = second.inTangent * delta;

                    return parametricTime * (parametricTime * (parametricTime * (2.0f * w1 - 2.0f * w2 + s1 + s2) + (-3.0f * w1 + 3.0f * w2 - 2.0f * s1 - s2)) + s1) + w1;
                }
            }
            return 0;
        }
    }

    private void RebuildLookups()
    {
        FaceGraphNodeLookup.Clear();
        if (FxActor is null)
        {
            return;
        }
        for (int i = 0; i < FxActor.CompiledFaceGraph.Count; i++)
        {
            FxNode node = FxActor.CompiledFaceGraph[i];
            node.TrackValue = 0;
            node.FinalValue = 0;
            FaceGraphNodeLookup[FxActor.Names[node.Name]] = i;
        }
        CaseInsensitiveDictionary<FaceFXBoneListEntry> tempDict = new(FxActor.RefBones.Count);
        foreach (var fxBone in FxActor.RefBones)
        {
            if ((uint)fxBone.RefBone.BoneName < FxActor.Names.Count)
            {
                tempDict.Add(FxActor.Names[fxBone.RefBone.BoneName], fxBone);
            }
        }
        for (int i = 0; i < _bones.Length; i++)
        {
            if (tempDict.TryGetValue(_bones[i].Name.Instanced, out var fxBone))
            {
                SkelMeshRefSkeletonToFaceFxBoneLookup[i] = fxBone;
            }
            else
            {
                SkelMeshRefSkeletonToFaceFxBoneLookup[i] = null;
            }
        }
    }
}
