using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace LegendaryExplorerCore.Unreal.Animation;

public abstract class AnimPlayer
{
    protected readonly MeshBone[] _bones;
    protected Matrix4x4[] _inverseBindPose;
    protected Matrix4x4[] _skinningMatrices;
    protected Matrix4x4[] _boneComponentSpace;

    public Matrix4x4[] BoneComponentSpaceTransforms => _boneComponentSpace;

    public float CurrentTime { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsLooping { get; set; } = true;
    public float PlaybackSpeed { get; set; } = 1f;

    public abstract bool HasAnimation { get; }
    public abstract float Duration { get; }
    public abstract float StartTime { get; }
    public abstract float EndTime { get; }
    public abstract void SetCurrentTime(float time);

    public abstract Matrix4x4[] ComputeSkinningMatrices();

  public void AdvanceTime(float dt)
    {
        if (!HasAnimation || Duration <= 0) return;

        CurrentTime += dt * PlaybackSpeed;

        if (IsLooping)
        {
            while (CurrentTime >= EndTime) CurrentTime -= Duration;
            while (CurrentTime < StartTime) CurrentTime += Duration;
        }
        else
        {
            CurrentTime = Math.Clamp(CurrentTime, StartTime, EndTime);
        }
    }

    protected AnimPlayer(SkeletalMesh skeletalMesh)
    {
        _bones = skeletalMesh.RefSkeleton;
        int numBones = _bones.Length;
        _inverseBindPose = new Matrix4x4[numBones];
        _skinningMatrices = new Matrix4x4[numBones];
        _boneComponentSpace = new Matrix4x4[numBones];

        ComputeBindPose();
    }

    private void ComputeBindPose()
    {
        int numBones = _bones.Length;
        var bindPose = new Matrix4x4[numBones];
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
    }

    public void ApplyBonePositions(BonePosition[] bonePosition)
    {
        var boneNameToIndex = _bones.Select((b, i) => (b.Name.Instanced, i))
                                    .ToDictionary(x => x.Instanced, x => x.i, StringComparer.OrdinalIgnoreCase);
        foreach (var pos in bonePosition)
        {
            if (boneNameToIndex.TryGetValue(pos.Name, out int boneIndex))
            {
                _bones[boneIndex].Position = pos.Position;
            }
        }
        ComputeBindPose();
    }
}
