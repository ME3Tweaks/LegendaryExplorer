using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class AnimSequence : ObjectBinary
    {
        public static readonly AnimationCompressionFormat[] ValidRotationCompressionFormats = { AnimationCompressionFormat.ACF_None, AnimationCompressionFormat.ACF_Float96NoW, AnimationCompressionFormat.ACF_BioFixed48, AnimationCompressionFormat.ACF_Fixed48NoW };

        public List<AnimTrack> RawAnimationData;
        public byte[] CompressedAnimationData;

        //for parsing the CompressedAnimationData

        public List<string> Bones;
        public NameReference Name;
        public int NumFrames;
        public float RateScale;
        public float SequenceLength;
        private MEGame compressedDataSource;
        private int[] TrackOffsets;
        private AnimationCompressionFormat rotCompression;
        private AnimationCompressionFormat posCompression;
        private AnimationKeyFormat keyEncoding;

        protected override void Serialize(SerializingContainer sc)
        {
            if (sc.Game.IsGame2())
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                sc.SerializeFileOffset();
            }

            if (sc.Game == MEGame.UDK)
            {
                if (sc.IsSaving && RawAnimationData is null)
                {
                    DecompressAnimationData();
                }
                sc.Serialize(ref RawAnimationData, sc.Serialize);
            }

            if (sc.IsLoading)
            {
                compressedDataSource = sc.Game;
                NumFrames = Export.GetProperty<IntProperty>("NumFrames")?.Value ?? 0;
                RateScale = Export.GetProperty<FloatProperty>("RateScale")?.Value ?? 1f;
                SequenceLength = Export.GetProperty<FloatProperty>("SequenceLength")?.Value ?? 0;
                Name = Export.GetProperty<NameProperty>("SequenceName")?.Value.Instanced ?? Export.ObjectName.Instanced;
                TrackOffsets = Export.GetProperty<ArrayProperty<IntProperty>>("CompressedTrackOffsets").Select(i => i.Value).ToArray();
                if (compressedDataSource == MEGame.UDK)
                {
                    Bones = ((ExportEntry)Export.Parent)?.GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames")?.Select(np => np.Value.Instanced).ToList();
                }
                else
                {
                    var animsetData = Export.GetProperty<ObjectProperty>("m_pBioAnimSetData");
                    //In ME2, BioAnimSetData can sometimes be in a different package. 
                    Bones = animsetData != null && Export.FileRef.IsUExport(animsetData.Value)
                        ? Export.FileRef.GetUExport(animsetData.Value)?.GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames")?.Select(np => np.Value.Instanced).ToList()
                        : null;
                }

                Bones ??= Enumerable.Repeat("???", TrackOffsets.Length / 4).ToList();
                Enum.TryParse(Export.GetProperty<EnumProperty>("KeyEncodingFormat")?.Value.Name, out keyEncoding);
                Enum.TryParse(Export.GetProperty<EnumProperty>("RotationCompressionFormat")?.Value.Name, out rotCompression);
                Enum.TryParse(Export.GetProperty<EnumProperty>("TranslationCompressionFormat")?.Value.Name, out posCompression);
            }

            sc.Serialize(ref CompressedAnimationData);
        }

        public static AnimSequence Create()
        {
            return new()
            {
                RawAnimationData = [],
                CompressedAnimationData = [],
                Bones = [],
                Name = new NameReference("None"),
                RateScale = 1,
                TrackOffsets = [],
            };
        }

        public void UpdateProps(PropertyCollection props, MEGame newGame, AnimationCompressionFormat newRotationCompression = AnimationCompressionFormat.ACF_Float96NoW, bool forceUpdate = false)
        {
            if (forceUpdate || compressedDataSource == MEGame.Unknown || (newGame != compressedDataSource && !(newGame != MEGame.UDK && compressedDataSource != MEGame.UDK)))
            {
                CompressAnimationData(newGame, newRotationCompression);
                props.RemoveNamedProperty("KeyEncodingFormat");
                props.RemoveNamedProperty("TranslationCompressionFormat");
                props.AddOrReplaceProp(new EnumProperty(rotCompression.ToString(), nameof(AnimationCompressionFormat), newGame, "RotationCompressionFormat"));
                props.AddOrReplaceProp(new ArrayProperty<IntProperty>(TrackOffsets.Select(i => new IntProperty(i)), "CompressedTrackOffsets"));
                props.AddOrReplaceProp(new IntProperty(NumFrames, "NumFrames"));
                props.AddOrReplaceProp(new FloatProperty(RateScale, "RateScale"));
                props.AddOrReplaceProp(new FloatProperty(SequenceLength, "SequenceLength"));
                props.AddOrReplaceProp(new NameProperty(Name, "SequenceName"));
            }
        }

        public void DecompressAnimationData()
        {
            if (CompressedAnimationData == null)
            {
                // 12/24/2024 - Nothing to decompress - Mgamerz
                return;
            }
            var ms = new MemoryStream(CompressedAnimationData);
            RawAnimationData = new List<AnimTrack>();

            for (int i = 0; i < Bones.Count; i++)
            {
                int posOff = TrackOffsets[i * 4];
                int numPosKeys = TrackOffsets[i * 4 + 1];
                int rotOff = TrackOffsets[i * 4 + 2];
                int numRotKeys = TrackOffsets[i * 4 + 3];

                var track = new AnimTrack
                {
                    Positions = new List<Vector3>(numPosKeys),
                    Rotations = new List<Quaternion>(numRotKeys)
                };

                if (numPosKeys > 0)
                {
                    ms.JumpTo(posOff);

                    AnimationCompressionFormat compressionFormat = posCompression;

                    if (numPosKeys == 1)
                    {
                        compressionFormat = AnimationCompressionFormat.ACF_None;
                    }

                    for (int j = 0; j < numPosKeys; j++)
                    {
                        switch (compressionFormat)
                        {
                            case AnimationCompressionFormat.ACF_None:
                            case AnimationCompressionFormat.ACF_Float96NoW:
                                track.Positions.Add(new Vector3(ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat()));
                                break;
                            case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                            case AnimationCompressionFormat.ACF_Fixed48NoW:
                            case AnimationCompressionFormat.ACF_Fixed32NoW:
                            case AnimationCompressionFormat.ACF_Float32NoW:
                            case AnimationCompressionFormat.ACF_BioFixed48:
                                throw new NotImplementedException($"Translation keys in format {compressionFormat} cannot be read yet!");
                        }
                    }

                    if (keyEncoding == AnimationKeyFormat.AKF_VariableKeyLerp && numPosKeys > 1)
                    {
                        ms.JumpTo(ms.Position.Align(4));

                        var keyTimes = new List<int>(numPosKeys);
                        for (int j = 0; j < numPosKeys; j++)
                        {
                            keyTimes.Add(NumFrames > 0xFF ? ms.ReadUInt16() : ms.ReadByte());
                        }
                        //RawAnimationData should have either 1 key, or the same number of keys as frames.
                        //Lerp any missing keys
                        List<Vector3> tempPositions = track.Positions;
                        track.Positions = new List<Vector3>(NumFrames)
                        {
                            tempPositions[0]
                        };
                        for (int frameIdx = 1, keyIdx = 1; frameIdx < NumFrames; keyIdx++, frameIdx++)
                        {
                            if (keyIdx >= keyTimes.Count)
                            {
                                track.Positions.Add(track.Positions[frameIdx - 1]);
                            }
                            else if (keyTimes[keyIdx] == frameIdx)
                            {
                                track.Positions.Add(tempPositions[keyIdx]);
                            }
                            else
                            {
                                int nextFrame = keyTimes[keyIdx];
                                int prevFrame = frameIdx - 1;
                                for (int j = frameIdx; j < nextFrame; j++)
                                {
                                    float amount = (float)(j - prevFrame) / (nextFrame - prevFrame);
                                    track.Positions.Add(Vector3.Lerp(track.Positions[prevFrame], tempPositions[keyIdx], amount));
                                }
                                track.Positions.Add(tempPositions[keyIdx]);
                                frameIdx = nextFrame;
                            }
                        }
                    }
                }

                if (numRotKeys > 0)
                {
                    ms.JumpTo(rotOff);

                    AnimationCompressionFormat compressionFormat = rotCompression;

                    if (numRotKeys == 1)
                    {
                        compressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
                    }
                    else if (compressedDataSource != MEGame.UDK)
                    {
                        ms.Skip(12 * 2); //skip mins and ranges
                    }

                    for (int j = 0; j < numRotKeys; j++)
                    {
                        switch (compressionFormat)
                        {
                            case AnimationCompressionFormat.ACF_None:
                                track.Rotations.Add(new Quaternion(ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat()));
                                break;
                            case AnimationCompressionFormat.ACF_Float96NoW:
                            {
                                float x = ms.ReadFloat();
                                float y = ms.ReadFloat();
                                float z = ms.ReadFloat();
                                track.Rotations.Add(new Quaternion(x, y, z, ReconstructQuaternionComponent(x, y, z)));
                                break;
                            }
                            case AnimationCompressionFormat.ACF_BioFixed48:
                            {
                                ushort a = ms.ReadUInt16();
                                ushort b = ms.ReadUInt16();
                                ushort c = ms.ReadUInt16();
                                track.Rotations.Add(DecompressBioFixed48(a, b, c));
                                break;
                            }
                            case AnimationCompressionFormat.ACF_Fixed48NoW:
                            {
                                ushort a = ms.ReadUInt16();
                                ushort b = ms.ReadUInt16();
                                ushort c = ms.ReadUInt16();
                                track.Rotations.Add(DecompressFixed48NoW(a,b,c));
                                break;
                            }
                            case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                            case AnimationCompressionFormat.ACF_Fixed32NoW:
                            case AnimationCompressionFormat.ACF_Float32NoW:
                                throw new NotImplementedException($"Rotation keys in format {compressionFormat} cannot be read yet!");
                        }
                    }

                    if (keyEncoding == AnimationKeyFormat.AKF_VariableKeyLerp && numRotKeys > 1)
                    {
                        ms.JumpTo(ms.Position.Align(4));

                        var keyTimes = new List<int>(numRotKeys);
                        for (int j = 0; j < numRotKeys; j++)
                        {
                            keyTimes.Add(NumFrames > 0xFF ? ms.ReadUInt16() : ms.ReadByte());
                        }
                        //RawAnimationData should have either 1 key, or the same number of keys as frames.
                        //Slerp any missing keys
                        List<Quaternion> tempRotations = track.Rotations;
                        track.Rotations = new List<Quaternion>(NumFrames)
                        {
                            tempRotations[0]
                        };
                        for (int frameIdx = 1, keyIdx = 1; frameIdx < NumFrames; keyIdx++, frameIdx++)
                        {
                            if (keyIdx >= keyTimes.Count)
                            {
                                track.Rotations.Add(track.Rotations[frameIdx - 1]);
                            }
                            else if (keyTimes[keyIdx] == frameIdx)
                            {
                                track.Rotations.Add(tempRotations[keyIdx]);
                            }
                            else
                            {
                                int nextFrame = keyTimes[keyIdx];
                                int prevFrame = frameIdx - 1;
                                for (int j = frameIdx; j < nextFrame; j++)
                                {
                                    float amount = (float)(j - prevFrame) / (nextFrame - prevFrame);
                                    track.Rotations.Add(Quaternion.Slerp(track.Rotations[prevFrame], tempRotations[keyIdx], amount));
                                }
                                track.Rotations.Add(tempRotations[keyIdx]);
                                frameIdx = nextFrame;
                            }
                        }
                    }
                }

                RawAnimationData.Add(track);
            }
        }

        private void CompressAnimationData(MEGame game, AnimationCompressionFormat newRotationCompression)
        {
            /* SirCxyrtyx 8/12/24: Always decompress, do not use pre-existing RawAnimationData if from a upk.
             * In same cases, the RawAnimationData is wrong for unknown reasons ¯\_(ツ)_/¯
             * The compressed data should be regarded as the source of truth
             */
            DecompressAnimationData();

            keyEncoding = AnimationKeyFormat.AKF_ConstantKeyLerp;
            posCompression = AnimationCompressionFormat.ACF_None;
            rotCompression = newRotationCompression;
            TrackOffsets = new int[Bones.Count * 4];
            using var ms = MemoryManager.GetMemoryStream();

            for (int i = 0; i < Bones.Count; i++)
            {
                AnimTrack track = RawAnimationData[i];

                TrackOffsets[i * 4] = (int)ms.Position;
                int numPosKeys = track.Positions.Count;
                TrackOffsets[i * 4 + 1] = numPosKeys;

                if (numPosKeys > 0)
                {
                    AnimationCompressionFormat compressionFormat = posCompression;

                    if (numPosKeys == 1)
                    {
                        compressionFormat = AnimationCompressionFormat.ACF_None;
                    }

                    for (int j = 0; j < numPosKeys; j++)
                    {
                        switch (compressionFormat)
                        {
                            case AnimationCompressionFormat.ACF_None:
                            case AnimationCompressionFormat.ACF_Float96NoW:
                                ms.WriteFloat(track.Positions[j].X);
                                ms.WriteFloat(track.Positions[j].Y);
                                ms.WriteFloat(track.Positions[j].Z);
                                break;
                            default:
                                throw new NotImplementedException($"Translation keys in format {compressionFormat} cannot be written yet!");
                        }
                    }
                    PadTo4();
                }

                TrackOffsets[i * 4 + 2] = (int)ms.Position;
                int numRotKeys = track.Rotations.Count;
                TrackOffsets[i * 4 + 3] = numRotKeys;

                if (numRotKeys > 0)
                {
                    AnimationCompressionFormat compressionFormat = rotCompression;

                    if (numRotKeys == 1)
                    {
                        compressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
                    }
                    else if (game != MEGame.UDK)
                    {
                        float xMin, yMin, zMin, xMax, yMax, zMax;
                        xMin = yMin = zMin = float.MaxValue;
                        xMax = yMax = zMax = float.MinValue;

                        foreach (Quaternion quat in track.Rotations)
                        {
                            xMin = Math.Min(xMin, quat.X);
                            yMin = Math.Min(yMin, quat.Y);
                            zMin = Math.Min(zMin, quat.Z);
                            xMax = Math.Max(xMax, quat.X);
                            yMax = Math.Max(yMax, quat.Y);
                            zMax = Math.Max(zMax, quat.Z);
                        }

                        ms.WriteFloat(xMin);
                        ms.WriteFloat(yMin);
                        ms.WriteFloat(zMin);
                        ms.WriteFloat(xMax - xMin);
                        ms.WriteFloat(yMax - yMin);
                        ms.WriteFloat(zMax - zMin);
                    }

                    for (int j = 0; j < numRotKeys; j++)
                    {
                        Quaternion rot = track.Rotations[j];
                        // ensure that the quaternion is normalized before we compress it. reconstructing the uncompressed version depends on
                        // the quaternion being normalized
                        rot = Quaternion.Normalize(rot);
                        switch (compressionFormat)
                        {
                            case AnimationCompressionFormat.ACF_None:
                                ms.WriteFloat(rot.X);
                                ms.WriteFloat(rot.Y);
                                ms.WriteFloat(rot.Z);
                                ms.WriteFloat(rot.W);
                                break;
                            case AnimationCompressionFormat.ACF_Float96NoW:
                                // reverse this so when we reconstruct w on decompression, the signs are correct
                                if (rot.W < 0)
                                {
                                    rot = -rot;
                                }
                                ms.WriteFloat(rot.X);
                                ms.WriteFloat(rot.Y);
                                ms.WriteFloat(rot.Z);
                                break;
                            case AnimationCompressionFormat.ACF_BioFixed48:
                            {
                                CompressBioFixed48(rot, out var a, out var b, out var c);
                                ms.WriteUInt16(a);
                                ms.WriteUInt16(b);
                                ms.WriteUInt16(c);
                            }
                                break;
                            case AnimationCompressionFormat.ACF_Fixed48NoW:
                            {
                                CompressFixed48NoW(rot, out var a, out var b, out var c);
                                ms.WriteUInt16(a);
                                ms.WriteUInt16(b);
                                ms.WriteUInt16(c);
                                break;
                            }
                            case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                            case AnimationCompressionFormat.ACF_Fixed32NoW:
                            case AnimationCompressionFormat.ACF_Float32NoW:
                            default:
                                throw new NotImplementedException($"Rotation keys in format {compressionFormat} cannot be written yet!");
                        }
                    }
                    PadTo4();
                }
            }

            CompressedAnimationData = ms.ToArray();
            compressedDataSource = game;

            void PadTo4()
            {
                var numAlignBytes = ms.Position.Align(4) - ms.Position;
                for (int j = 0; j < numAlignBytes; j++)
                {
                    ms.WriteByte(0x55);
                }
            }
        }

        public static Quaternion DecompressFixed48NoW(ushort a, ushort b, ushort c)
        {
            const float scale = 32767.0f;
            const ushort shift = 32767;
            float x = (a - shift) / scale;
            float y = (b - shift) / scale;
            float z = (c - shift) / scale;
            return new Quaternion(x, y, z, ReconstructQuaternionComponent(x, y, z));
        }

        private static void CompressFixed48NoW(Quaternion rot, out ushort a, out ushort b, out ushort c)
        {
            // we need to make sure W is positive before we compress so that when we decompress and get a positive w, our value is correct
            if (rot.W < 0)
            {
                rot = -rot;
            }
            const float scale = 32767.0f;
            const ushort shift = 32767;
            a = (ushort)(rot.X * scale + shift).Clamp(0, ushort.MaxValue);
            b = (ushort)(rot.Y * scale + shift).Clamp(0, ushort.MaxValue);
            c = (ushort)(rot.Z * scale + shift).Clamp(0, ushort.MaxValue);
        }

        public static Quaternion DecompressBioFixed48(ushort a, ushort b, ushort c)
        {
            const float shift = 0.70710678118f;
            const float scale = 1.41421356237f;
            const float precisionMult = 32767.0f;
            float x = (a & 0x7FFF) / precisionMult * scale - shift;
            float y = (b & 0x7FFF) / precisionMult * scale - shift;
            float z = (c & 0x7FFF) / precisionMult * scale - shift;
            float w = ReconstructQuaternionComponent(x, y, z);
            int wPos = ((a >> 14) & 2) | ((b >> 15) & 1);
            return wPos switch
            {
                0 => new Quaternion(w, x, y, z),
                1 => new Quaternion(x, w, y, z),
                2 => new Quaternion(x, y, w, z),
                _ => new Quaternion(x, y, z, w)
            };
        }

        private static void CompressBioFixed48(Quaternion rot, out ushort a, out ushort b, out ushort c)
        {
            const float shift = 0.70710678118f;
            const float scale = 1.41421356237f;
            const float precisionMult = 32767.0f;
            //smallest three compression: https://gafferongames.com/post/snapshot_compression/
            //omit the largest component, and store its index
            int wPos = 0;
            float max = 0f;
            float[] rotArr = { rot.X, rot.Y, rot.Z, rot.W };
            for (int k = 0; k < 4; k++)
            {
                if (Math.Abs(rotArr[k]) > max)
                {
                    max = Math.Abs(rotArr[k]);
                    wPos = k;
                }
            }
            // if the largest component is negative, we need to flip the signs of all four (which is an equivalent quaternion)
            // so that when we reconstruct a positive fourth component, it is correct
            if (rotArr[wPos] < 0)
            {
                rot = -rot;
            }

            //remap the smallest three components to the lower 15 bits of a ushort
            static ushort compress(float f) => (ushort)((f + shift) / scale * precisionMult).Clamp(0, 0x7FFF);

            switch (wPos)
            {
                case 0:
                    a = compress(rot.Y);
                    b = compress(rot.Z);
                    c = compress(rot.W);
                    break;
                case 1:
                    a = compress(rot.X);
                    b = compress(rot.Z);
                    c = compress(rot.W);
                    break;
                case 2:
                    a = compress(rot.X);
                    b = compress(rot.Y);
                    c = compress(rot.W);
                    break;
                default:
                    a = compress(rot.X);
                    b = compress(rot.Y);
                    c = compress(rot.Z);
                    break;
            }

            //stuff the 2 bit index of the omitted component into the high bits of a and b
            a |= (ushort)((wPos & 2) << 14);
            b |= (ushort)((wPos & 1) << 15);
        }

        public PropertyCollection CompressAnimationDataAndUpdateProperties()
        {
            var props = Export.GetProperties();
            UpdateProps(props, Export.Game, rotCompression, true);
            return props;
        }

        // there are various ways to compress a quaternion; they all involve reconstructing one of the four components while decompressing
        // this will always return a value >= 0, so make sure the component you drop before compressing is not negative
        // you can do this by negating the entire quaternion, which gives an equivalent quaternion
        public static float ReconstructQuaternionComponent(float x, float y, float z)
        {
            float wSquared = 1.0f - (x * x + y * y + z * z);
            return (float)(wSquared > 0f ? Math.Sqrt(wSquared) : 0f);
        }
    }

    public class AnimTrack
    {
        public List<Vector3> Positions;
        public List<Quaternion> Rotations;
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref AnimTrack track)
        {
            if (IsLoading)
            {
                track = new AnimTrack();
            }

            int vector3Size = 12;
            Serialize(ref vector3Size);
            Serialize(ref track.Positions, Serialize);
            int quatSize = 16;
            Serialize(ref quatSize);
            Serialize(ref track.Rotations, Serialize);
        }
    }
}