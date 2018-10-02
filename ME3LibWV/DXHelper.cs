using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using SharpDX;
using ME3LibWV.UnrealClasses;

namespace ME3LibWV
{
    public static class DXHelper
    {
        public static Vector3 RotatorToDX(Vector3 v)
        {
            Vector3 r = v;
            r.X = (int)r.X % 65536;
            r.Y = (int)r.Y % 65536;
            r.Z = (int)r.Z % 65536;
            float f = (3.1415f * 2f) / 65536f;
            r.X = v.Z * f;//z
            r.Y = v.X * f;//x
            r.Z = v.Y * f;//y
            return r;
        }
        public static Vector3 DxToRotator(Vector3 v)
        {
            Vector3 r = new Vector3();
            float f = 65536f / (3.1415f * 2f);
            r.X = -v.X * f;
            r.Y = v.Z * f;
            r.Z = -v.Y * f;
            r.X = (int)r.X % 65536;
            r.Y = (int)r.Y % 65536;
            r.Z = (int)r.Z % 65536;
            return r;
        }
        public static byte[] Vector3ToBuff(Vector3 v)
        {
            MemoryStream m = new MemoryStream();
            m.Write(BitConverter.GetBytes(v.X), 0, 4);
            m.Write(BitConverter.GetBytes(v.Y), 0, 4);
            m.Write(BitConverter.GetBytes(v.Z), 0, 4);
            return m.ToArray();
        }
        public static byte[] RotatorToBuff(Vector3 v)
        {
            MemoryStream m = new MemoryStream();
            m.Write(BitConverter.GetBytes((int)v.X), 0, 4);
            m.Write(BitConverter.GetBytes((int)v.Y), 0, 4);
            m.Write(BitConverter.GetBytes((int)v.Z), 0, 4);
            return m.ToArray();
        }
        public static float HalfToFloat(UInt16 val)
        {
            UInt16 u = val;
            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            exp = exp + (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            byte[] buff = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(buff, 0);
        }
        public static UInt16 FloatToHalf(float f)
        {
            byte[] bytes = BitConverter.GetBytes((double)f);
            ulong bits = BitConverter.ToUInt64(bytes, 0);
            ulong exponent = bits & 0x7ff0000000000000L;
            ulong mantissa = bits & 0x000fffffffffffffL;
            ulong sign = bits & 0x8000000000000000L;
            int placement = (int)((exponent >> 52) - 1023);
            if (placement > 15 || placement < -14)
                return 0;
            UInt16 exponentBits = (UInt16)((15 + placement) << 10);
            UInt16 mantissaBits = (UInt16)(mantissa >> 42);
            UInt16 signBits = (UInt16)(sign >> 48);
            return (UInt16)(exponentBits | mantissaBits | signBits);
        }
        public static bool RayIntersectTriangle(Vector3 rayPosition, Vector3 rayDirection, Vector3 tri0, Vector3 tri1, Vector3 tri2, out float pickDistance)
        {
            pickDistance = -1f;
            Vector3 edge1 = tri1 - tri0;
            Vector3 edge2 = tri2 - tri0;
            Vector3 pvec = Vector3.Cross(rayDirection, edge2);
            float det = Vector3.Dot(edge1, pvec);
            if (det < 0.0001f)
                return false;
            Vector3 tvec = rayPosition - tri0;
            float barycentricU = Vector3.Dot(tvec, pvec);
            if (barycentricU < 0.0f || barycentricU > det)
                return false;
            Vector3 qvec = Vector3.Cross(tvec, edge1);
            float barycentricV = Vector3.Dot(rayDirection, qvec);
            if (barycentricV < 0.0f || barycentricU + barycentricV > det)
                return false;
            pickDistance = Vector3.Dot(edge2, qvec);
            float fInvDet = 1.0f / det;
            pickDistance *= fInvDet;
            return true;
        }

    }
}
