using System;
using System.Diagnostics;
using Gammtek.Conduit;
using SharpDX;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    [DebuggerDisplay("UIndex | {" + nameof(value) + "}")]
    public readonly struct UIndex : IEquatable<UIndex>
    {
        public readonly int value;

        public UIndex(int value)
        {
            this.value = value;
        }

        #region IEquatable

        public bool Equals(UIndex other)
        {
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            return obj is UIndex other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value;
        }

        public static bool operator ==(UIndex left, UIndex right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UIndex left, UIndex right)
        {
            return !left.Equals(right);
        }

        #endregion
    }

    public readonly struct Vector
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public readonly struct Vector2D
    {
        public readonly float X;
        public readonly float Y;

        public Vector2D(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

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
    }
    public readonly struct Rotator
    {
        public readonly int Pitch;
        public readonly int Yaw;
        public readonly int Roll;

        public Rotator(int pitch, int yaw, int roll)
        {
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
        }
    }

    public readonly struct Quat
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    // -1 to 1 converted to 0-255
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


        public static explicit operator PackedNormal(Vector vec)
        {
            return new PackedNormal((byte)((int)(vec.X * 127.5f + 128.0f)).Clamp(0, 255),
                                    (byte)((int)(vec.X * 127.5f + 128.0f)).Clamp(0, 255),
                                    (byte)((int)(vec.X * 127.5f + 128.0f)).Clamp(0, 255),
                                    128);
        }
    }

    public class Box
    {
        public Vector Min;
        public Vector Max;
        public byte IsValid;
    }

    public class BoxSphereBounds
    {
        public Vector Origin;
        public Vector BoxExtent;
        public float SphereRadius;
    }

    public class Sphere
    {
        public Vector Center;
        public float W;
    }

    public static class UnrealStructSCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref UIndex uidx)
        {
            if (sc.IsLoading)
            {
                uidx = new UIndex(sc.ms.ReadInt32());
            }
            else
            {
                sc.ms.WriteInt32(uidx.value);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Vector vec)
        {
            if (sc.IsLoading)
            {
                vec = new Vector(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.WriteFloat(vec.X);
                sc.ms.WriteFloat(vec.Y);
                sc.ms.WriteFloat(vec.Z);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Rotator rot)
        {
            if (sc.IsLoading)
            {
                rot = new Rotator(sc.ms.ReadInt32(), sc.ms.ReadInt32(), sc.ms.ReadInt32());
            }
            else
            {
                sc.ms.WriteInt32(rot.Pitch);
                sc.ms.WriteInt32(rot.Yaw);
                sc.ms.WriteInt32(rot.Roll);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Quat quat)
        {
            if (sc.IsLoading)
            {
                quat = new Quat(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.WriteFloat(quat.X);
                sc.ms.WriteFloat(quat.Y);
                sc.ms.WriteFloat(quat.Z);
                sc.ms.WriteFloat(quat.W);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Vector2D vec)
        {
            if (sc.IsLoading)
            {
                vec = new Vector2D(sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.WriteFloat(vec.X);
                sc.ms.WriteFloat(vec.Y);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Vector2DHalf vec)
        {
            if (sc.IsLoading)
            {
                vec = new Vector2DHalf(sc.ms.ReadUInt16(), sc.ms.ReadUInt16());
            }
            else
            {
                sc.ms.WriteUInt16(vec.Xbits);
                sc.ms.WriteUInt16(vec.Ybits);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref PackedNormal norm)
        {
            if (sc.IsLoading)
            {
                norm = new PackedNormal((byte)sc.ms.ReadByte(), (byte)sc.ms.ReadByte(), (byte)sc.ms.ReadByte(), (byte)sc.ms.ReadByte());
            }
            else
            {
                sc.ms.WriteByte(norm.X);
                sc.ms.WriteByte(norm.Y);
                sc.ms.WriteByte(norm.Z);
                sc.ms.WriteByte(norm.W);
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
        public static void Serialize(this SerializingContainer2 sc, ref Vector[] arr)
        {
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new Vector[count];
            }

            for (int i = 0; i < count; i++)
            {
                sc.Serialize(ref arr[i]);
            }

        }
        public static void Serialize(this SerializingContainer2 sc, ref UIndex[] arr)
        {
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new UIndex[count];
            }

            for (int i = 0; i < count; i++)
            {
                sc.Serialize(ref arr[i]);
            }

        }
    }
}