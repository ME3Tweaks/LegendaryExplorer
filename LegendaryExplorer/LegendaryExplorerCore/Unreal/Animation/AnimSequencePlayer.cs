using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorerCore.Unreal.Animation;

/// <summary>
/// Pure math class that handles skeleton bind-pose computation, animation track mapping,
/// and per-frame skinning matrix computation. No rendering dependency.
/// </summary>
/// <remarks>
/// Builds bind-pose transforms from a SkeletalMesh's RefSkeleton.
/// </remarks>
public class AnimSequencePlayer : AnimPlayer
{
    // Animation state
    private AnimSequence _animSequence;
    private int[] _skelToAnimMap; // skeleton bone index -> animTrack[i] or -1 if no track
    private bool _animRotationOnly = true; // animSequence => animSetData.bAnimRotationOnly; default true; if true, bones will ignore position tracks, only using the rotation
    private HashSet<string> _useTranslationBones = []; // animSequence => animSetData.UseTranslationBoneNames; these bones will use the positions from the animation even if _animRotationOnly in true
    private HashSet<string> _forceMeshTranslationBoneNames = []; // animSequence => animSetData.ForceMeshTranslationBoneNames; these bones will use the position from the mesh even if _animRotationOnly is false

    public AnimSequencePlayer(SkeletalMesh skeletalMesh) : base(skeletalMesh)
    {
        _skelToAnimMap = new int[_bones.Length];
    }

    public NameReference AnimName => _animSequence?.Name ?? "None";
    public int TotalFrames => _animSequence?.NumFrames ?? 0;
    public override float Duration => _animSequence?.SequenceLength ?? 0f;

    public override float StartTime => 0;

    public override float EndTime => Duration;

    public override bool HasAnimation => _animSequence != null;

    public int CurrentFrame
    {
        get
        {
            if (_animSequence == null || TotalFrames <= 1) return 0;
            float frameRate = (TotalFrames - 1) / Duration;
            return Math.Clamp((int)(CurrentTime * frameRate), 0, TotalFrames - 1);
        }
        set
        {
            if (_animSequence == null || TotalFrames <= 1)
            {
                CurrentTime = 0;
                return;
            }
            float frameRate = (TotalFrames - 1) / Duration;
            CurrentTime = Math.Clamp(value / frameRate, 0, Duration);
        }
    }

    public int BoneCount => _bones?.Length ?? 0;

    /// <summary>
    /// Maps animation bone names to skeleton bone indices and prepares for playback.
    /// </summary>
    public void SetAnimation(AnimSequence animSequence)
    {
        _animSequence = animSequence;
        CurrentTime = 0;
        _skelToAnimMap.AsSpan().Fill(-1);

        if (animSequence == null || _bones == null)
        {
            // Reset to bind pose
            if (_skinningMatrices != null)
            {
                for (int i = 0; i < _skinningMatrices.Length; i++)
                {
                    _skinningMatrices[i] = Matrix4x4.Identity;
                }
            }
            return;
        }

        animSequence.DecompressAnimationData();
        if (animSequence.RawAnimationData is null)
        {
            throw new InvalidOperationException("AnimSequence has no animation data!");
        }

        // Build name -> skeleton index map
        var nameToIndex = new Dictionary<string, int>(_bones.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _bones.Length; i++)
        {
            nameToIndex[_bones[i].Name.Instanced] = i;
        }

        // Build reverse lookup: skeleton bone index -> anim track index
        for (int i = 0; i < animSequence.Bones.Count; i++)
        {
            if (nameToIndex.TryGetValue(animSequence.Bones[i], out int skelIdx))
            {
                _skelToAnimMap[skelIdx] = i;
            }
        }

        // look up the animData from the AnimSequence, look up the UseTranslationBoneNames property, save it
        var animSetData = GetAnimSetData(animSequence);
        _animRotationOnly = animSetData?.GetProperty<BoolProperty>("bAnimRotationOnly")?.Value ?? true;
        _useTranslationBones = [.. animSetData?.GetProperty<ArrayProperty<NameProperty>>("UseTranslationBoneNames")?.Select(np => np.Value.Instanced) ?? []];
        _forceMeshTranslationBoneNames = [.. animSetData?.GetProperty<ArrayProperty<NameProperty>>("ForceMeshTranslationBoneNames")?.Select(np => np.Value.Instanced) ?? []];
    }

    private static ExportEntry GetAnimSetData(AnimSequence animSequence)
    {
        var animDataEntry = animSequence?.Export.GetProperty<ObjectProperty>("m_pBioAnimSetData").ResolveToEntry(animSequence.Export.FileRef);
        return animDataEntry is ExportEntry ? animDataEntry as ExportEntry : EntryImporter.ResolveImport(animDataEntry as ImportEntry, new PackageCache());
    }

    private bool ShouldBoneUsePositionTrack(string boneName)
    {
        // anything in this list should always use the mesh position rather than the animation position
        if (_forceMeshTranslationBoneNames.Contains(boneName))
        {
            return false;
        }
        // if animRotationOnly is set (it almost always will be)
        if (_animRotationOnly)
        {
            // then return false unless it is in the list of UseTranslationBoneNames
            return _useTranslationBones.Contains(boneName);
        }
        // otherwise, return true
        return true;
    }

    public override void SetCurrentTime(float time)
    {
        if (_animSequence == null || Duration <= 0)
        {
            CurrentTime = 0;
            return;
        }
        CurrentTime = Math.Clamp(time, 0, Duration);
    }

    /// <summary>
    /// Computes skinning matrices for the current frame.
    /// Returns the array of skinning matrices (InverseBindPose * AnimatedComponentSpace).
    /// </summary>
    public override Matrix4x4[] ComputeSkinningMatrices()
    {
        if (_bones == null || _skinningMatrices == null) return null;

        int numBones = _bones.Length;

        float frame = 0;
        if (TotalFrames > 1 && Duration > 0)
        {
            float frameRate = (TotalFrames - 1) / Duration;
            frame = CurrentTime * frameRate;
            frame = Math.Clamp(frame, 0, TotalFrames - 1);
        }

        for (int i = 0; i < numBones; i++)
        {
            Matrix4x4 localTransform;
            int trackIdx = _skelToAnimMap[i];
            var bone = _bones[i];

            if (trackIdx >= 0 && _animSequence?.RawAnimationData != null && trackIdx < _animSequence.RawAnimationData.Count)
            {
                var track = _animSequence.RawAnimationData[trackIdx];

                var pos = ShouldBoneUsePositionTrack(bone.Name)
                    ? SamplePosition(track, frame, bone.Position)
                    : bone.Position;

                // UE3 stores animation rotations conjugated relative to RefSkeleton bone orientations.
                // So animRot = Conjugate(bone.Orientation) for the same rotation.
                // We conjugate the sampled animation rotation so it matches the bind pose convention
                // (which uses bone.Orientation directly). This ensures InvBindPose * AnimatedCS = Identity
                // at rest pose. Fallback uses bone.Orientation directly (already in bind pose convention).
                var rot = (track.Rotations is { Count: > 0 })
                    ? -Quaternion.Conjugate(SampleRotation(track, frame))
                    : bone.Orientation;

                localTransform = Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
            }
            else
            {
                // No animation track for this bone at all - use bind pose local transform
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

    private static Vector3 SamplePosition(AnimTrack track, float frame, Vector3 bonePosition)
    {
        // Use animation position if track has keys, otherwise fall back to bind pose position.
        // Many bones only have rotation animation, so position tracks are often empty.
        if (track.Positions == null || track.Positions.Count == 0)
            return bonePosition;
        if (track.Positions.Count == 1)
            return track.Positions[0];

        int frameIdx = Math.Clamp((int)frame, 0, track.Positions.Count - 1);
        var first = track.Positions[frameIdx];
        float lerpAmount = frame - frameIdx;
        if (lerpAmount is > 0 and < 1 && track.Positions.Count > frameIdx + 1)
        {
            return Vector3.Lerp(first, track.Positions[frameIdx + 1], lerpAmount);
        }
        return first;
    }

    private static Quaternion SampleRotation(AnimTrack track, float frame)
    {
        if (track.Rotations == null || track.Rotations.Count == 0)
            return Quaternion.Identity;
        if (track.Rotations.Count == 1)
            return track.Rotations[0];

        int frameIdx = Math.Clamp((int)frame, 0, track.Rotations.Count - 1);
        var first = track.Rotations[frameIdx];
        float lerpAmount = frame - frameIdx;
        if (lerpAmount is > 0 and < 1 && track.Rotations.Count > frameIdx + 1)
        {
            return Quaternion.Lerp(first, track.Rotations[frameIdx + 1], lerpAmount);
        }
        return first;
    }
}
