using System;
using System.Numerics;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Vector2DHalf
    {
        public readonly ushort Xbits;
        public readonly ushort Ybits;

        public float X => Xbits.AsFloat16();
        public float Y => Ybits.AsFloat16();

        public Vector2DHalf(ushort x, ushort y)
        {
            Xbits = x;
            Ybits = y;
        }

        public Vector2DHalf(float x, float y)
        {
            Xbits = x.ToFloat16bits();
            Ybits = y.ToFloat16bits();
        }

        public static implicit operator Vector2DHalf(Vector2 vec2D)
        {
            return new Vector2DHalf(vec2D.X, vec2D.Y);
        }

        public static implicit operator Vector2(Vector2DHalf vec2DHalf)
        {
            return new Vector2(vec2DHalf.X, vec2DHalf.Y);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Rotator
    {
        public readonly int Pitch;
        public readonly int Yaw;
        public readonly int Roll;

        public void Deconstruct(out int pitch, out int yaw, out int roll)
        {
            pitch = Pitch;
            yaw = Yaw;
            roll = Roll;
        }

        public Rotator(int pitch, int yaw, int roll)
        {
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
        }

        public static Rotator FromDirectionVector(Vector3 dirVec)
        {
            float x = dirVec.X;
            float y = dirVec.Y;
            float z = dirVec.Z;
            var pitch = MathF.Atan2(z, MathF.Sqrt(MathF.Pow(x, 2) + MathF.Pow(y, 2)));
            var yaw = MathF.Atan2(y, x);
            return new Rotator(pitch.RadiansToUnrealRotationUnits(), yaw.RadiansToUnrealRotationUnits(), 0);
        }

        public Vector3 GetDirectionalVector()
        {
            var cp = MathF.Cos(Pitch.UnrealRotationUnitsToRadians());
            var cy = MathF.Cos(Yaw.UnrealRotationUnitsToRadians());
            var sp = MathF.Sin(Pitch.UnrealRotationUnitsToRadians());
            var sy = MathF.Sin(Yaw.UnrealRotationUnitsToRadians());
            return new Vector3((cp * cy), (cp * sy), sp);
        }

        public bool IsZero => Pitch == 0 && Yaw == 0 && Roll == 0; 
        public override string ToString()
        {
            return $"Pitch:{Pitch} Yaw:{Yaw} Roll:{Roll}";
        }
    }

// -1 to 1 converted to 0-255
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PackedNormal
    {
        public readonly byte X;
        public readonly byte Y;
        public readonly byte Z;
        public readonly byte W;

        public PackedNormal(byte x, byte y, byte z, byte w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static explicit operator Vector3(PackedNormal packedNormal)
        {
            return new Vector3(packedNormal.X / 127.5f - 1, packedNormal.Y / 127.5f - 1, packedNormal.Z / 127.5f - 1);
        }

        public static explicit operator Vector4(PackedNormal packedNormal)
        {
            return new Vector4(packedNormal.X / 127.5f - 1, packedNormal.Y / 127.5f - 1, packedNormal.Z / 127.5f - 1, packedNormal.W / 127.5f - 1);
        }

        public static explicit operator PackedNormal(Vector3 vec)
        {
            return new PackedNormal(vec.X.PackToByte(),
                vec.Y.PackToByte(),
                vec.Z.PackToByte(),
                128);
        }

        public static explicit operator PackedNormal(Vector4 vec)
        {
            return new PackedNormal(vec.X.PackToByte(),
                vec.Y.PackToByte(),
                vec.Z.PackToByte(),
                vec.W.PackToByte());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Influences
    {
        private readonly byte _0;
        private readonly byte _1;
        private readonly byte _2;
        private readonly byte _3;

        public Influences(byte a, byte b, byte c, byte d)
        {
            _0 = a;
            _1 = b;
            _2 = c;
            _3 = d;
        }

        public byte this[int i] =>
            i switch
            {
                0 => _0,
                1 => _1,
                2 => _2,
                3 => _3,
                _ => throw new IndexOutOfRangeException()
            };
    }

    public class Box
    {
        public Vector3 Min;
        public Vector3 Max;
        public byte IsValid;

        public void Add(Vector3 vec)
        {
            if (IsValid > 0)
            {
                Min.X = Math.Min(Min.X, vec.X);
                Min.Y = Math.Min(Min.Y, vec.Y);
                Min.Z = Math.Min(Min.Z, vec.Z);

                Max.X = Math.Max(Max.X, vec.X);
                Max.Y = Math.Max(Max.Y, vec.Y);
                Max.Z = Math.Max(Max.Z, vec.Z);
            }
            else
            {
                Max = Min = vec;
                IsValid = 1;
            }
        }
    }

    public class BoxSphereBounds
    {
        public Vector3 Origin;
        public Vector3 BoxExtent;
        public float SphereRadius;
    }

    public class Sphere
    {
        public Vector3 Center;
        public float W;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref Vector3 vec)
        {
            if (sc.ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[12];
                if (sc.IsLoading)
                {
                    sc.ms.Read(span);
                    vec = MemoryMarshal.Read<Vector3>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in vec);
                    sc.ms.Writer.Write(span);
                }
            }
            else
            {
                if (sc.IsLoading)
                {
                    vec = new Vector3(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
                }
                else
                {
                    sc.ms.Writer.WriteFloat(vec.X);
                    sc.ms.Writer.WriteFloat(vec.Y);
                    sc.ms.Writer.WriteFloat(vec.Z);
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Plane plane)
        {
            if (sc.ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[16];
                if (sc.IsLoading)
                {
                    sc.ms.Read(span);
                    plane = MemoryMarshal.Read<Plane>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in plane);
                    sc.ms.Writer.Write(span);
                }
            }
            else
            {
                if (sc.IsLoading)
                {
                    plane = new Plane(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
                }
                else
                {
                    sc.ms.Writer.WriteFloat(plane.Normal.X);
                    sc.ms.Writer.WriteFloat(plane.Normal.Y);
                    sc.ms.Writer.WriteFloat(plane.Normal.Z);
                    sc.ms.Writer.WriteFloat(plane.D);
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Rotator rot)
        {
            if (sc.ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[12];
                if (sc.IsLoading)
                {
                    sc.ms.Read(span);
                    rot = MemoryMarshal.Read<Rotator>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in rot);
                    sc.ms.Writer.Write(span);
                }
            }
            else
            {
                if (sc.IsLoading)
                {
                    rot = new Rotator(sc.ms.ReadInt32(), sc.ms.ReadInt32(), sc.ms.ReadInt32());
                }
                else
                {
                    sc.ms.Writer.WriteInt32(rot.Pitch);
                    sc.ms.Writer.WriteInt32(rot.Yaw);
                    sc.ms.Writer.WriteInt32(rot.Roll);
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Quaternion quat)
        {
            if (sc.ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[16];
                if (sc.IsLoading)
                {
                    sc.ms.Read(span);
                    quat = MemoryMarshal.Read<Quaternion>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in quat);
                    sc.ms.Writer.Write(span);
                }
            }
            else
            {
                if (sc.IsLoading)
                {
                    quat = new Quaternion(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
                }
                else
                {
                    sc.ms.Writer.WriteFloat(quat.X);
                    sc.ms.Writer.WriteFloat(quat.Y);
                    sc.ms.Writer.WriteFloat(quat.Z);
                    sc.ms.Writer.WriteFloat(quat.W);
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Vector2 vec)
        {
            if (sc.ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[8];
                if (sc.IsLoading)
                {
                    sc.ms.Read(span);
                    vec = MemoryMarshal.Read<Vector2>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in vec);
                    sc.ms.Writer.Write(span);
                }
            }
            else
            {
                if (sc.IsLoading)
                {
                    vec = new Vector2(sc.ms.ReadFloat(), sc.ms.ReadFloat());
                }
                else
                {
                    sc.ms.Writer.WriteFloat(vec.X);
                    sc.ms.Writer.WriteFloat(vec.Y);
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Vector2DHalf vec)
        {
            if (sc.ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[4];
                if (sc.IsLoading)
                {
                    sc.ms.Read(span);
                    vec = MemoryMarshal.Read<Vector2DHalf>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in vec);
                    sc.ms.Writer.Write(span);
                }
            }
            else
            {
                if (sc.IsLoading)
                {
                    vec = new Vector2DHalf(sc.ms.ReadUInt16(), sc.ms.ReadUInt16());
                }
                else
                {
                    sc.ms.Writer.WriteUInt16(vec.Xbits);
                    sc.ms.Writer.WriteUInt16(vec.Ybits);
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref PackedNormal norm)
        {
            Span<byte> span = stackalloc byte[4];
            if (sc.IsLoading)
            {
                sc.ms.Read(span);
                norm = MemoryMarshal.Read<PackedNormal>(span);
            }
            else
            {
                MemoryMarshal.Write(span, in norm);
                sc.ms.Writer.Write(span);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Influences influences)
        {
            Span<byte> span = stackalloc byte[4];
            if (sc.IsLoading)
            {
                sc.ms.Read(span);
                influences = MemoryMarshal.Read<Influences>(span);
            }
            else
            {
                MemoryMarshal.Write(span, in influences);
                sc.ms.Writer.Write(span);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref BoxSphereBounds bounds)
        {
            if (sc.IsLoading)
            {
                bounds = new BoxSphereBounds();
            }
            sc.Serialize(ref bounds.Origin);
            sc.Serialize(ref bounds.BoxExtent);
            sc.Serialize(ref bounds.SphereRadius);
        }
        public static void Serialize(this SerializingContainer2 sc, ref Box box)
        {
            if (sc.IsLoading)
            {
                box = new Box();
            }
            sc.Serialize(ref box.Min);
            sc.Serialize(ref box.Max);
            sc.Serialize(ref box.IsValid);
        }
        public static void Serialize(this SerializingContainer2 sc, ref Sphere sphere)
        {
            if (sc.IsLoading)
            {
                sphere = new Sphere();
            }
            sc.Serialize(ref sphere.Center);
            sc.Serialize(ref sphere.W);
        }
    }
}