using System;
using System.Numerics;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Gammtek.Extensions;
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
    public readonly struct Rotator(int pitch, int yaw, int roll)
    {
        public readonly int Pitch = pitch;
        public readonly int Yaw = yaw;
        public readonly int Roll = roll;

        public void Deconstruct(out int pitch, out int yaw, out int roll)
        {
            pitch = Pitch;
            yaw = Yaw;
            roll = Roll;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct LinearColor(float r, float g, float b, float a)
    {
        public float R = r;
        public float G = g;
        public float B = b;
        public float A = a;

        public LinearColor(float all) : this(all, all, all, all){}

        public void Deconstruct(out float r, out float g, out float b, out float a)
        {
            r = R;
            g = G;
            b = B;
            a = A;
        }

        public override string ToString()
        {
            return $"R:{R}, G:{G}, B:{B}, A:{A}";
        }

        public static explicit operator Vector4(LinearColor color)
        {
            return new Vector4(color.R, color.G, color.B, color.A);
        }

        public static explicit operator LinearColor(Vector4 vec)
        {
            return new LinearColor(vec.X, vec.Y, vec.Z, vec.W);
        }

        public static LinearColor Black => new(0, 0, 0, 1);
        public static LinearColor White => new(1, 1, 1, 1);
    }

// -1 to 1 converted to 0-255
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PackedNormal(byte x, byte y, byte z, byte w)
    {
        public readonly byte X = x;
        public readonly byte Y = y;
        public readonly byte Z = z;
        public readonly byte W = w;

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
    public readonly struct Influences(byte a, byte b, byte c, byte d)
    {
        public byte this[int i] =>
            i switch
            {
                0 => a,
                1 => b,
                2 => c,
                3 => d,
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

    public partial class SerializingContainer
    {
        public void Serialize(ref Vector3 vec)
        {
            if (ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[12];
                if (IsLoading)
                {
                    ms.Read(span);
                    vec = MemoryMarshal.Read<Vector3>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in vec);
                    ms.Writer.Write(span);
                }
            }
            else
            {
                if (IsLoading)
                {
                    vec = new Vector3(ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat());
                }
                else
                {
                    ms.Writer.WriteFloat(vec.X);
                    ms.Writer.WriteFloat(vec.Y);
                    ms.Writer.WriteFloat(vec.Z);
                }
            }
        }
        public void Serialize(ref Plane plane)
        {
            if (ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[16];
                if (IsLoading)
                {
                    ms.Read(span);
                    plane = MemoryMarshal.Read<Plane>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in plane);
                    ms.Writer.Write(span);
                }
            }
            else
            {
                if (IsLoading)
                {
                    plane = new Plane(ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat());
                }
                else
                {
                    ms.Writer.WriteFloat(plane.Normal.X);
                    ms.Writer.WriteFloat(plane.Normal.Y);
                    ms.Writer.WriteFloat(plane.Normal.Z);
                    ms.Writer.WriteFloat(plane.D);
                }
            }
        }
        public void Serialize(ref Rotator rot)
        {
            if (ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[12];
                if (IsLoading)
                {
                    ms.Read(span);
                    rot = MemoryMarshal.Read<Rotator>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in rot);
                    ms.Writer.Write(span);
                }
            }
            else
            {
                if (IsLoading)
                {
                    rot = new Rotator(ms.ReadInt32(), ms.ReadInt32(), ms.ReadInt32());
                }
                else
                {
                    ms.Writer.WriteInt32(rot.Pitch);
                    ms.Writer.WriteInt32(rot.Yaw);
                    ms.Writer.WriteInt32(rot.Roll);
                }
            }
        }
        public void Serialize(ref Quaternion quat)
        {
            if (ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[16];
                if (IsLoading)
                {
                    ms.Read(span);
                    quat = MemoryMarshal.Read<Quaternion>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in quat);
                    ms.Writer.Write(span);
                }
            }
            else
            {
                if (IsLoading)
                {
                    quat = new Quaternion(ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat());
                }
                else
                {
                    ms.Writer.WriteFloat(quat.X);
                    ms.Writer.WriteFloat(quat.Y);
                    ms.Writer.WriteFloat(quat.Z);
                    ms.Writer.WriteFloat(quat.W);
                }
            }
        }
        public void Serialize(ref LinearColor lColor)
        {
            if (ms.Endian.IsNative)
            {
                if (IsLoading)
                {
                    ms.Read(lColor.AsBytes());
                }
                else
                {
                    ms.Writer.Write(lColor.AsBytes());
                }
            }
            else
            {
                if (IsLoading)
                {
                    lColor = new LinearColor(ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat());
                }
                else
                {
                    ms.Writer.WriteFloat(lColor.R);
                    ms.Writer.WriteFloat(lColor.G);
                    ms.Writer.WriteFloat(lColor.B);
                    ms.Writer.WriteFloat(lColor.A);
                }
            }
        }
        public void Serialize(ref Vector2 vec)
        {
            if (ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[8];
                if (IsLoading)
                {
                    ms.Read(span);
                    vec = MemoryMarshal.Read<Vector2>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in vec);
                    ms.Writer.Write(span);
                }
            }
            else
            {
                if (IsLoading)
                {
                    vec = new Vector2(ms.ReadFloat(), ms.ReadFloat());
                }
                else
                {
                    ms.Writer.WriteFloat(vec.X);
                    ms.Writer.WriteFloat(vec.Y);
                }
            }
        }
        public void Serialize(ref Vector2DHalf vec)
        {
            if (ms.Endian.IsNative)
            {
                Span<byte> span = stackalloc byte[4];
                if (IsLoading)
                {
                    ms.Read(span);
                    vec = MemoryMarshal.Read<Vector2DHalf>(span);
                }
                else
                {
                    MemoryMarshal.Write(span, in vec);
                    ms.Writer.Write(span);
                }
            }
            else
            {
                if (IsLoading)
                {
                    vec = new Vector2DHalf(ms.ReadUInt16(), ms.ReadUInt16());
                }
                else
                {
                    ms.Writer.WriteUInt16(vec.Xbits);
                    ms.Writer.WriteUInt16(vec.Ybits);
                }
            }
        }
        public void Serialize(ref PackedNormal norm)
        {
            Span<byte> span = stackalloc byte[4];
            if (IsLoading)
            {
                ms.Read(span);
                norm = MemoryMarshal.Read<PackedNormal>(span);
            }
            else
            {
                MemoryMarshal.Write(span, in norm);
                ms.Writer.Write(span);
            }
        }
        public void Serialize(ref Influences influences)
        {
            Span<byte> span = stackalloc byte[4];
            if (IsLoading)
            {
                ms.Read(span);
                influences = MemoryMarshal.Read<Influences>(span);
            }
            else
            {
                MemoryMarshal.Write(span, in influences);
                ms.Writer.Write(span);
            }
        }
        public void Serialize(ref BoxSphereBounds bounds)
        {
            if (IsLoading)
            {
                bounds = new BoxSphereBounds();
            }
            Serialize(ref bounds.Origin);
            Serialize(ref bounds.BoxExtent);
            Serialize(ref bounds.SphereRadius);
        }
        public void Serialize(ref Box box)
        {
            if (IsLoading)
            {
                box = new Box();
            }
            Serialize(ref box.Min);
            Serialize(ref box.Max);
            Serialize(ref box.IsValid);
        }
        public void Serialize(ref Sphere sphere)
        {
            if (IsLoading)
            {
                sphere = new Sphere();
            }
            Serialize(ref sphere.Center);
            Serialize(ref sphere.W);
        }
    }
}