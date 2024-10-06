using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal
{
    public class PSA
    {
        public List<PSABone> Bones;
        public List<PSAAnimInfo> Infos;
        public List<PSAAnimKeys> Keys;

        private const int version = 1999801;

        protected void Serialize(SerializingContainer2 sc)
        {
            var mainHeader = new ChunkHeader
            {
                ChunkID = "ANIMHEAD",
                Version = version
            };
            sc.Serialize(ref mainHeader);

            var boneHeader = new ChunkHeader
            {
                ChunkID = "BONENAMES",
                Version = version,
                DataSize = 0x78,
                DataCount = Bones?.Count ?? 0
            };
            sc.Serialize(ref boneHeader);
            sc.Serialize(ref Bones, boneHeader.DataCount, SCExt.Serialize);

            var infoHeader = new ChunkHeader
            {
                ChunkID = "ANIMINFO",
                Version = version,
                DataSize = 0xa8,
                DataCount = Infos?.Count ?? 0
            };
            sc.Serialize(ref infoHeader);
            sc.Serialize(ref Infos, infoHeader.DataCount, SCExt.Serialize);

            var keyHeader = new ChunkHeader
            {
                ChunkID = "ANIMKEYS",
                Version = version,
                DataSize = 0x20,
                DataCount = Keys?.Count ?? 0
            };
            sc.Serialize(ref keyHeader);
            sc.Serialize(ref Keys, keyHeader.DataCount, SCExt.Serialize);
        }

        public class ChunkHeader
        {
            public string ChunkID; //serialized to 20 bytes long
            public int Version; //1999801 or 2003321
            public int DataSize;
            public int DataCount;
        }

        public class PSABone
        {
            public string Name;
            public uint Flags;
            public int NumChildren;
            public int ParentIndex;
            public Quaternion Rotation;
            public Vector3 Position;
            public float Length;
            public Vector3 Size;
        }

        public class PSAAnimInfo
        {
            public string Name;
            public string Group;
            public int TotalBones;
            public int RootInclude;
            public int KeyCompressionStyle;
            public int KeyQuotum;
            public float KeyReduction;
            public float TrackTime;
            public float AnimRate;
            public int StartBone;
            public int FirstRawFrame;
            public int NumRawFrames;
        }

        public class PSAAnimKeys
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Time;
        }

        public static PSA CreateFrom(AnimSequence animSeq) => CreateFrom(new List<AnimSequence>{ animSeq });

        //All Animsequences MUST have the same BoneLists!
        public static PSA CreateFrom(List<AnimSequence> animSeqs)
        {
            if (animSeqs == null)
            {
                throw new ArgumentNullException(nameof(animSeqs));
            }

            if (animSeqs.Count == 0)
            {
                throw new ArgumentException("No AnimSequences!", nameof(animSeqs));
            }
            var psa = new PSA
            {
                Bones = new List<PSABone>(),
                Infos = new List<PSAAnimInfo>(),
                Keys = new List<PSAAnimKeys>()
            };

            int numBones = animSeqs[0].Bones.Count;
            for (int i = 0; i < numBones; i++)
            {
                psa.Bones.Add(new PSABone
                {
                    Name = animSeqs[0].Bones[i],
                    ParentIndex = i == 0 ? -1 : 0
                });
            }

            int frameCount = 0;
            foreach (AnimSequence animSeq in animSeqs)
            {
                int numFrames = animSeq.NumFrames;
                psa.Infos.Add(new PSAAnimInfo
                {
                    Name = animSeq.Name.Instanced,
                    Group = "None",
                    TotalBones = numBones,
                    KeyQuotum = numBones * numFrames,
                    TrackTime = numFrames,
                    AnimRate = animSeq.NumFrames / animSeq.SequenceLength * animSeq.RateScale,
                    FirstRawFrame = frameCount,
                    NumRawFrames = numFrames
                });
                frameCount += numFrames;

                /* SirCxyrtyx 8/12/24: Always decompress, do not use pre-existing RawAnimationData if from a upk.
                 * In same cases, the RawAnimationData is wrong for unknown reasons ¯\_(ツ)_/¯
                 * The compressed data should be regarded as the source of truth
                 */
                animSeq.DecompressAnimationData();

                for (int frameIdx = 0; frameIdx < numFrames; frameIdx++)
                {
                    for (int boneIdx = 0; boneIdx < numBones; boneIdx++)
                    {
                        AnimTrack animTrack = animSeq.RawAnimationData[boneIdx];
                        Vector3 posVec = animTrack.Positions.Count > frameIdx ? animTrack.Positions[frameIdx] : animTrack.Positions[^1];
                        Quaternion rotQuat = animTrack.Rotations.Count > frameIdx ? animTrack.Rotations[frameIdx] : animTrack.Rotations[^1];
                        rotQuat = new Quaternion(rotQuat.X * -1, rotQuat.Y, rotQuat.Z * -1, rotQuat.W);
                        posVec = posVec with { Y = posVec.Y * -1 };
                        psa.Keys.Add(new PSAAnimKeys
                        {
                            Position = posVec,
                            Rotation = rotQuat,
                            Time = 1
                        });
                    }
                }
            }

            return psa;
        }

        public List<AnimSequence> GetAnimSequences()
        {
            var animSeqs = new List<AnimSequence>();

            List<string> boneNames = Bones.Select(b => b.Name).ToList();

            int boneCount = boneNames.Count;
            foreach (PSAAnimInfo info in Infos)
            {
                var seq = new AnimSequence
                {
                    Bones = boneNames,
                    Name = NameReference.FromInstancedString(info.Name),
                    NumFrames = info.NumRawFrames,
                    SequenceLength = info.TrackTime / info.AnimRate,
                    RateScale = 1,
                    RawAnimationData = new List<AnimTrack>()
                };

                for (int boneIdx = 0; boneIdx < boneCount; boneIdx++)
                {
                    var track = new AnimTrack
                    {
                        Positions = new List<Vector3>(),
                        Rotations = new List<Quaternion>()
                    };

                    for (int frameIdx = 0; frameIdx < seq.NumFrames; frameIdx++)
                    {
                        int srcIdx = ((info.FirstRawFrame + frameIdx) * boneCount) + boneIdx;
                        Vector3 posVec = Keys[srcIdx].Position;
                        Quaternion rotQuat = Keys[srcIdx].Rotation;
                        track.Positions.Add(posVec with { Y = posVec.Y * -1 });
                        track.Rotations.Add(new Quaternion(rotQuat.X * -1, rotQuat.Y, rotQuat.Z * -1, rotQuat.W));
                    }

                    //if all keys are identical, replace with a single key
                    if (track.Positions.Count > 1)
                    {
                        var firstKey = track.Positions[0];
                        if (track.Positions.TrueForAll(key => key == firstKey))
                        {
                            track.Positions.Clear();
                            track.Positions.Add(firstKey);
                        }
                    }
                    if (track.Rotations.Count > 1)
                    {
                        var firstKey = track.Rotations[0];
                        if (track.Rotations.TrueForAll(key => key == firstKey))
                        {
                            track.Rotations.Clear();
                            track.Rotations.Add(firstKey);
                        }
                    }

                    seq.RawAnimationData.Add(track);
                }

                animSeqs.Add(seq);
            }

            return animSeqs;
        }

        public void ToFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            Serialize(new SerializingContainer2(fs, null));
        }

        public static PSA FromFile(string filePath)
        {
            var psa = new PSA();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            psa.Serialize(new SerializingContainer2(fs, null, true));
            return psa;
        }
    }
}

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref PSA.ChunkHeader h)
        {
            if (sc.IsLoading)
            {
                h = new PSA.ChunkHeader();
            }

            sc.SerializeFixedSizeString(ref h.ChunkID, 20);
            sc.Serialize(ref h.Version);
            sc.Serialize(ref h.DataSize);
            sc.Serialize(ref h.DataCount);
        }

        public static void Serialize(this SerializingContainer2 sc, ref PSA.PSABone b)
        {
            if (sc.IsLoading)
            {
                b = new PSA.PSABone();
            }

            sc.SerializeFixedSizeString(ref b.Name, 64);
            sc.Serialize(ref b.Flags);
            sc.Serialize(ref b.NumChildren);
            sc.Serialize(ref b.ParentIndex);
            sc.Serialize(ref b.Rotation);
            sc.Serialize(ref b.Position);
            sc.Serialize(ref b.Length);
            sc.Serialize(ref b.Size);
        }

        public static void Serialize(this SerializingContainer2 sc, ref PSA.PSAAnimInfo a)
        {
            if (sc.IsLoading)
            {
                a = new PSA.PSAAnimInfo();
            }

            sc.SerializeFixedSizeString(ref a.Name, 64);
            sc.SerializeFixedSizeString(ref a.Group, 64);
            sc.Serialize(ref a.TotalBones);
            sc.Serialize(ref a.RootInclude);
            sc.Serialize(ref a.KeyCompressionStyle);
            sc.Serialize(ref a.KeyQuotum);
            sc.Serialize(ref a.KeyReduction);
            sc.Serialize(ref a.TrackTime);
            sc.Serialize(ref a.AnimRate);
            sc.Serialize(ref a.StartBone);
            sc.Serialize(ref a.FirstRawFrame);
            sc.Serialize(ref a.NumRawFrames);
        }

        public static void Serialize(this SerializingContainer2 sc, ref PSA.PSAAnimKeys k)
        {
            if (sc.IsLoading)
            {
                k = new PSA.PSAAnimKeys();
            }
            sc.Serialize(ref k.Position);
            sc.Serialize(ref k.Rotation);
            sc.Serialize(ref k.Time);
        }

        public static void SerializeFixedSizeString(this SerializingContainer2 sc, ref string s, int length)
        {
            if (sc.IsLoading)
            {
                var pos = sc.ms.Position;
                s = sc.ms.ReadStringASCIINull();

                sc.ms.JumpTo(pos + length);
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    if (i < s.Length)
                        sc.ms.Writer.WriteByte((byte)s[i]);
                    else
                        sc.ms.Writer.WriteByte(0);
                }
            }
        }
    }
}