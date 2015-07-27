using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace ME3Explorer.UnrealHelper
{
    public class UMath
    {
        public struct Rotator
        {
            public int Yaw;
            public int Pitch;
            public int Roll;

            public static Rotator operator +(Rotator a, Rotator b)
            {
                Rotator t = a;
                t.Yaw += b.Yaw;
                t.Pitch += b.Pitch;
                t.Roll += b.Roll;
                return t;
            }
        }

        public Rotator VectorToRotator(Vector3 v)
        {
            Rotator r = new Rotator();
            float f2 = 65536f / 360f;
            r.Yaw = (int)(v.Y * f2);
            r.Pitch = (int)(v.X * f2);
            r.Roll = (int)(v.Z  * f2);
            r.Yaw = r.Yaw % (65536 / 2);
            r.Pitch = r.Pitch % (65536 / 2);
            r.Roll = r.Roll % (65536 / 2);
            return r;
        }
        public Vector3 RotatorToVector(Rotator r)
        {
            Vector3 v = new Vector3();
            float f = (3.1415f * 2f) / 65536f;
            v.X = r.Yaw * f;
            v.Y = r.Pitch * f;
            v.Z = r.Roll * f;
            return v;
        }
        public Rotator IntVectorToRotator(Vector3 v)
        {
            Rotator r = new Rotator();
            r.Yaw = (int)(v.X);
            r.Pitch = (int)(v.Y);
            r.Roll = (int)(v.Z);
            return r;
        }
        public Rotator PropToRotator(byte[] buff)
        {
            Rotator r = new Rotator();
            r.Pitch = -BitConverter.ToInt32(buff, 8);
            r.Roll = BitConverter.ToInt32(buff, 12);
            r.Yaw = -BitConverter.ToInt32(buff, 16);
            return r;
        }
    }
}
