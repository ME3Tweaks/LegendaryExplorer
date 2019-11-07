using System;
using System.Diagnostics;
using SharpDX;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    [DebuggerDisplay("UIndex | {" + nameof(value) + "}")]
    public sealed class UIndex : IEquatable<UIndex>
    {
        public int value;

        public UIndex(int value)
        {
            this.value = value;
        }

        public static implicit operator int(UIndex uIndex) => uIndex.value;

        public static implicit operator UIndex(int uIndex) => new UIndex(uIndex);

        #region IEquatable

        public bool Equals(UIndex other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((UIndex)obj);
        }

        public override int GetHashCode()
        {
            return value;
        }

        public static bool operator ==(UIndex left, UIndex right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(UIndex left, UIndex right)
        {
            return !Equals(left, right);
        }

        #endregion
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

        public static implicit operator Vector2DHalf(Vector2 vec2D)
        {
            return new Vector2DHalf(vec2D.X, vec2D.Y);
        }

        public static implicit operator Vector2(Vector2DHalf vec2DHalf)
        {
            return new Vector2(vec2DHalf.X, vec2DHalf.Y);
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
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref UIndex uidx)
        {
            if (sc.IsLoading)
            {
                uidx = new UIndex(sc.ms.ReadInt32());
            }
            else
            {
                sc.ms.WriteInt32(uidx?.value ?? 0);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Vector3 vec)
        {
            if (sc.IsLoading)
            {
                vec = new Vector3(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.WriteFloat(vec.X);
                sc.ms.WriteFloat(vec.Y);
                sc.ms.WriteFloat(vec.Z);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Plane plane)
        {
            if (sc.IsLoading)
            {
                plane = new Plane(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.WriteFloat(plane.Normal.X);
                sc.ms.WriteFloat(plane.Normal.Y);
                sc.ms.WriteFloat(plane.Normal.Z);
                sc.ms.WriteFloat(plane.D);
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
        public static void Serialize(this SerializingContainer2 sc, ref Quaternion quat)
        {
            if (sc.IsLoading)
            {
                quat = new Quaternion(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.WriteFloat(quat.X);
                sc.ms.WriteFloat(quat.Y);
                sc.ms.WriteFloat(quat.Z);
                sc.ms.WriteFloat(quat.W);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Vector2 vec)
        {
            if (sc.IsLoading)
            {
                vec = new Vector2(sc.ms.ReadFloat(), sc.ms.ReadFloat());
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
    }
}