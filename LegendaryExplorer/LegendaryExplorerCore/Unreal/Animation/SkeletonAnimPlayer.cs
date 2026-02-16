using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace LegendaryExplorerCore.Unreal.Animation;

/// <summary>
/// Pure math class that handles skeleton bind-pose computation, animation track mapping,
/// and per-frame skinning matrix computation. No rendering dependency.
/// </summary>
public class SkeletonAnimPlayer
{
    private MeshBone[] _bones;
    private Matrix4x4[] _inverseBindPose;
    private Matrix4x4[] _skinningMatrices;

    // Animation state
    private AnimSequence _animSequence;
    private int[] _animToSkelMap; // animTrack[i] -> skeleton bone index
    private int[] _skelToAnimMap; // skeleton bone index -> animTrack[i] or -1 if no track
    private float _currentTime;

    public NameReference AnimName => _animSequence?.Name ?? "None";
    public int TotalFrames => _animSequence?.NumFrames ?? 0;
    public float Duration => _animSequence?.SequenceLength ?? 0f;
    public bool IsPlaying { get; set; }
    public bool IsLooping { get; set; } = true;
    public float PlaybackSpeed { get; set; } = 1f;

    public bool HasAnimation => _animSequence != null;

    public int CurrentFrame
    {
        get
        {
            if (_animSequence == null || TotalFrames <= 1) return 0;
            float frameRate = (TotalFrames - 1) / Duration;
            return Math.Clamp((int)(_currentTime * frameRate), 0, TotalFrames - 1);
        }
        set
        {
            if (_animSequence == null || TotalFrames <= 1)
            {
                _currentTime = 0;
                return;
            }
            float frameRate = (TotalFrames - 1) / Duration;
            _currentTime = Math.Clamp(value / frameRate, 0, Duration);
        }
    }

    public int BoneCount => _bones?.Length ?? 0;

    /// <summary>
    /// Builds bind-pose transforms from a SkeletalMesh's RefSkeleton.
    /// </summary>
    public void SetSkeleton(SkeletalMesh skeletalMesh)
    {
        _bones = skeletalMesh.RefSkeleton;
        int numBones = _bones.Length;
        var bindPose = new Matrix4x4[numBones];
        _inverseBindPose = new Matrix4x4[numBones];
        _skinningMatrices = new Matrix4x4[numBones];

        for (int i = 0; i < numBones; i++)
        {
            var bone = _bones[i];
            // bone.Orientation is used directly (not conjugated) as the bind pose convention.
            // Animation rotations from tracks are conjugated at sample time to match this convention.
            // (UE3 stores animation rotations conjugated relative to RefSkeleton orientations.)
            var localTransform = Matrix4x4.CreateFromQuaternion(bone.Orientation) * Matrix4x4.CreateTranslation(bone.Position);

            if (bone.ParentIndex >= 0 && bone.ParentIndex < i)
            {
                bindPose[i] = localTransform * bindPose[bone.ParentIndex];
            }
            else
            {
                bindPose[i] = localTransform;
            }

            Matrix4x4.Invert(bindPose[i], out _inverseBindPose[i]);
            _skinningMatrices[i] = Matrix4x4.Identity;
        }

        // Reset animation state
        _animSequence = null;
        _animToSkelMap = null;
        _skelToAnimMap = null;
        _currentTime = 0;
    }

    /// <summary>
    /// Maps animation bone names to skeleton bone indices and prepares for playback.
    /// </summary>
    public void SetAnimation(AnimSequence animSequence)
    {
        _animSequence = animSequence;
        _currentTime = 0;

        if (animSequence == null || _bones == null)
        {
            _animToSkelMap = null;
            _skelToAnimMap = null;
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
        _skelToAnimMap = new int[_bones.Length];
        _skelToAnimMap.AsSpan().Fill(-1);

        // Map each animation track to a skeleton bone
        _animToSkelMap = new int[animSequence.Bones.Count];
        for (int i = 0; i < _animToSkelMap.Length; i++)
        {
            if (nameToIndex.TryGetValue(animSequence.Bones[i], out int skelIdx))
            {
                _animToSkelMap[i] = skelIdx;
                _skelToAnimMap[skelIdx] = i;
            }
            else
            {
                _animToSkelMap[i] = -1; // no match
            }
        }
    }

    /// <summary>
    /// Advances playback time by the given delta in seconds.
    /// </summary>
    public void AdvanceTime(float dt)
    {
        if (_animSequence == null || Duration <= 0) return;

        _currentTime += dt * PlaybackSpeed;

        if (IsLooping)
        {
            while (_currentTime >= Duration) _currentTime -= Duration;
            while (_currentTime < 0) _currentTime += Duration;
        }
        else
        {
            _currentTime = Math.Clamp(_currentTime, 0, Duration);
        }
    }

    public void SetCurrentTime(float time)
    {
        if (_animSequence == null || Duration <= 0)
        {
            _currentTime = 0;
            return;
        }
        _currentTime = Math.Clamp(time, 0, Duration);
    }

    /// <summary>
    /// Computes skinning matrices for the current frame.
    /// Returns the array of skinning matrices (InverseBindPose * AnimatedComponentSpace).
    /// </summary>
    public Matrix4x4[] ComputeSkinningMatrices()
    {
        if (_bones == null || _skinningMatrices == null) return null;

        int numBones = _bones.Length;
        var animatedCS = new Matrix4x4[numBones];

        float frame = 0;
        if (TotalFrames > 1 && Duration > 0)
        {
            float frameRate = (TotalFrames - 1) / Duration;
            frame = _currentTime * frameRate;
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

                // Use animation position if track has keys, otherwise fall back to bind pose position.
                // Many bones only have rotation animation, so position tracks are often empty.
                var pos = (track.Positions is { Count: > 0 })
                    ? SamplePosition(track, frame)
                    : bone.Position;

                // UE3 stores animation rotations conjugated relative to RefSkeleton bone orientations.
                // So animRot = Conjugate(bone.Orientation) for the same rotation.
                // We conjugate the sampled animation rotation so it matches the bind pose convention
                // (which uses bone.Orientation directly). This ensures InvBindPose * AnimatedCS = Identity
                // at rest pose. Fallback uses bone.Orientation directly (already in bind pose convention).
                var rot = (track.Rotations is { Count: > 0 })
                    ? Quaternion.Conjugate(SampleRotation(track, frame))
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
                animatedCS[i] = localTransform * animatedCS[bone.ParentIndex];
            }
            else
            {
                animatedCS[i] = localTransform;
            }

            _skinningMatrices[i] = _inverseBindPose[i] * animatedCS[i];
        }

        return _skinningMatrices;
    }

    private static Vector3 SamplePosition(AnimTrack track, float frame)
    {
        if (track.Positions == null || track.Positions.Count == 0)
            return Vector3.Zero;
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
