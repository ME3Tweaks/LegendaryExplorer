using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.ME3Enums;
using SharpDX;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class AnimSequence : ObjectBinary
    {
        public byte[] AnimationData; //todo: actually parse animation data
        public List<AnimTrack> Tracks;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game == MEGame.ME2)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                int offset = sc.FileOffset + 4;
                sc.Serialize(ref offset);
            }
            sc.Serialize(ref AnimationData, SCExt.Serialize);
            return;
            int startOffset = (int)sc.ms.Position;

            var TrackOffsets = Export.GetProperty<ArrayProperty<IntProperty>>("CompressedTrackOffsets");
            var animsetData = Export.GetProperty<ObjectProperty>("m_pBioAnimSetData");
            var boneList = Export.FileRef.GetUExport(animsetData.Value).GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames");
            Enum.TryParse(Export.GetProperty<EnumProperty>("RotationCompressionFormat")?.Value.Name, out AnimationCompressionFormat rotCompression);
            Enum.TryParse(Export.GetProperty<EnumProperty>("TranslationCompressionFormat")?.Value.Name, out AnimationCompressionFormat posCompression);

            if (sc.IsLoading)
            {
                Tracks = new List<AnimTrack>(boneList.Count);
            }

            for (int i = 0; i < boneList.Count; i++)
            {
                if (sc.IsLoading)
                {
                    Tracks.Add(null);
                }
                int posOff = TrackOffsets[i * 4];
                int posKeys = TrackOffsets[i * 4 + 1];
                int rotOff = TrackOffsets[i * 4 + 2];
                int rotKeys = TrackOffsets[i * 4 + 3];

                if (posKeys > 0)
                {
                    sc.ms.JumpTo(startOffset + posOff);

                    AnimationCompressionFormat compressionFormat = posCompression;

                    if (posKeys == 1)
                    {
                        compressionFormat = AnimationCompressionFormat.ACF_None;
                    }

                    if (sc.IsLoading)
                    {
                        Tracks[i].Positions = new List<Vector3>(posKeys);
                    }
                    for (int j = 0; j < posKeys; j++)
                    {
                        if (sc.IsLoading)
                        {
                            Tracks[i].Positions.Add(Vector3.Zero);
                        }
                        Vector3 position = Tracks[i].Positions[j];
                        switch (compressionFormat)
                        {
                            case AnimationCompressionFormat.ACF_None:
                            case AnimationCompressionFormat.ACF_Float96NoW:
                                sc.Serialize(ref position);
                                break;
                            case AnimationCompressionFormat.ACF_Fixed48NoW:
                                const float scale = 128.0f / 32767.0f;
                                ushort unkConst = 32767;
                                if (sc.IsLoading)
                                {
                                    position = new Vector3((sc.ms.ReadUInt16() - unkConst) * scale, 
                                                           (sc.ms.ReadUInt16() - unkConst) * scale, 
                                                           (sc.ms.ReadUInt16() - unkConst) * scale);
                                }
                                else
                                {
                                    sc.ms.Writer.WriteUInt16((ushort)(position.X / scale + unkConst));
                                    sc.ms.Writer.WriteUInt16((ushort)(position.Y / scale + unkConst));
                                    sc.ms.Writer.WriteUInt16((ushort)(position.Y / scale + unkConst));
                                }
                                break;
                            case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                            case AnimationCompressionFormat.ACF_Fixed32NoW:
                            case AnimationCompressionFormat.ACF_Float32NoW:
                            case AnimationCompressionFormat.ACF_BioFixed48:
                                throw new NotImplementedException($"Translation keys in format {compressionFormat} cannot be read!");
                        }
                        Tracks[i].Positions[j] = position;
                    }
                }

                if (rotKeys > 0)
                {
                    sc.ms.JumpTo(startOffset + rotOff);

                    AnimationCompressionFormat compressionFormat = rotCompression;

                    if (rotKeys == 1)
                    {
                        compressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
                    }

                    if (sc.IsLoading)
                    {
                        Tracks[i].Rotations = new List<Quaternion>(rotKeys);
                    }
                    for (int j = 0; j < rotKeys; j++)
                    {

                        if (sc.IsLoading)
                        {
                            Tracks[i].Rotations.Add(Quaternion.Identity);
                        }

                        Quaternion rot = Tracks[i].Rotations[j];
                        switch (compressionFormat)
                        {
                            case AnimationCompressionFormat.ACF_None:
                                sc.Serialize(ref rot);
                                break;
                            case AnimationCompressionFormat.ACF_Float96NoW:
                                if (sc.IsLoading)
                                {
                                    float x = sc.ms.ReadFloat();
                                    float y = sc.ms.ReadFloat();
                                    float z = sc.ms.ReadFloat();
                                    rot = new Quaternion(x, y, z, getW(x, y, z));
                                }
                                else
                                {
                                    sc.ms.Writer.WriteFloat(rot.X);
                                    sc.ms.Writer.WriteFloat(rot.Y);
                                    sc.ms.Writer.WriteFloat(rot.Z);
                                }
                                break;
                            case AnimationCompressionFormat.ACF_BioFixed48:
                                const float shift = 0.70710678118f;
                                const float scale = 1.41421356237f;
                                const float precisionMult = 32767.0f;
                                if (sc.IsLoading)
                                {
                                    ushort a = sc.ms.ReadUInt16();
                                    ushort b = sc.ms.ReadUInt16();
                                    ushort c = sc.ms.ReadUInt16();
                                    float x = (a & 0x7FFF) / precisionMult * scale - shift;
                                    float y = (b & 0x7FFF) / precisionMult * scale - shift;
                                    float z = (c & 0x7FFF) / precisionMult * scale - shift;
                                    float w = getW(x, y, z);
                                    int wPos = ((a >> 14) & 2) | ((b >> 15) & 1);
                                    rot = wPos switch
                                    {
                                        0 => new Quaternion(w, x, y, z),
                                        1 => new Quaternion(x, w, y, z),
                                        2 => new Quaternion(x, y, w, z),
                                        _ => new Quaternion(x, y, z, w)
                                    };
                                }
                                else
                                {
                                    //smallest three compression: https://gafferongames.com/post/snapshot_compression/
                                    //omit the largest component, and store it's index
                                    int wPos = 0;
                                    float max = 0f;
                                    for (int k = 0; k < 4; k++)
                                    {
                                        if (Math.Abs(rot[k]) > max)
                                        {
                                            max = Math.Abs(rot[k]);
                                            wPos = k;
                                        }
                                    }
                                    //remap the smallest three components to the lower 15 bits of a ushort
                                    ushort a, b, c;

                                    ushort compressToUShort(float f)
                                    {
                                        return (ushort)((f + shift) / scale * precisionMult);
                                    }

                                    switch (wPos)
                                    {
                                        case 0:
                                            a = compressToUShort(rot.Y);
                                            b = compressToUShort(rot.Z);
                                            c = compressToUShort(rot.W);
                                            break;
                                        case 1:
                                            a = compressToUShort(rot.X);
                                            b = compressToUShort(rot.Z);
                                            c = compressToUShort(rot.W);
                                            break;
                                        case 2:
                                            a = compressToUShort(rot.X);
                                            b = compressToUShort(rot.Y);
                                            c = compressToUShort(rot.W);
                                            break;
                                        default:
                                            a = compressToUShort(rot.X);
                                            b = compressToUShort(rot.Y);
                                            c = compressToUShort(rot.Z);
                                            break;
                                    }

                                    //stuff the 2 bit index of the omitted component into the high bits of a and b
                                    a |= (ushort)((wPos & 2) << 14);
                                    b |= (ushort)((wPos & 1) << 15);

                                    sc.ms.Writer.WriteUInt16(a);
                                    sc.ms.Writer.WriteUInt16(b);
                                    sc.ms.Writer.WriteUInt16(c);
                                }
                                break;
                            case AnimationCompressionFormat.ACF_Fixed48NoW:
                            case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                            case AnimationCompressionFormat.ACF_Fixed32NoW:
                            case AnimationCompressionFormat.ACF_Float32NoW:
                                throw new NotImplementedException($"Translation keys in format {compressionFormat} cannot be read!");
                        }
                        Tracks[i].Rotations[j] = rot;
                    }

                    static float getW(float x, float y, float z)
                    {
                        float wSquared = 1.0f - (x * x + y * y + z * z);
                        return (float)(wSquared > 0 ? Math.Sqrt(wSquared) : 0);
                    }
                }
            }
        }
    }

    public class AnimTrack
    {
        public List<Vector3> Positions;
        public List<Quaternion> Rotations;
    }
}
namespace ME3Explorer
{
    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref AnimTrack track)
        {
            if (sc.IsLoading)
            {
                track = new AnimTrack();
            }
            
        }
    }
}
