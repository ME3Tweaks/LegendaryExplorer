using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal.BinaryConverters;
using SharpDX;

namespace ME3Explorer.Unreal
{
    public static class CommonStructs
    {
        public static StructProperty Vector(Vector3 vec, NameReference? name = null) => Vector(vec.X, vec.Y, vec.Z, name);
        public static StructProperty Vector(float x, float y, float z, NameReference? name = null)
        {
            return new StructProperty("Vector", new PropertyCollection
            {
                new FloatProperty(x, "X"),
                new FloatProperty(y, "Y"),
                new FloatProperty(z, "Z")
            }, name, true);
        }

        public static Vector3 GetVector(StructProperty vecProp) =>
            new Vector3(vecProp.GetProp<FloatProperty>("X"), vecProp.GetProp<FloatProperty>("Y"), vecProp.GetProp<FloatProperty>("Z"));

        public static StructProperty Rotator(Rotator rot, NameReference? name = null) => Rotator(rot.Pitch,rot.Yaw, rot.Roll, name);
        public static StructProperty Rotator(int pitch, int yaw, int roll, NameReference? name = null)
        {
            return new StructProperty("Rotator", new PropertyCollection
            {
                new IntProperty(pitch, "Pitch"),
                new IntProperty(yaw, "Yaw"),
                new IntProperty(roll, "Roll")
            }, name, true);
        }

        public static StructProperty Matrix(Matrix m, NameReference? name = null)
        {
            return new StructProperty("Matrix", new PropertyCollection
            {
                new StructProperty("Plane", new PropertyCollection
                {
                    new FloatProperty(m.M14, "W"),
                    new FloatProperty(m.M11, "X"),
                    new FloatProperty(m.M12, "Y"),
                    new FloatProperty(m.M13, "Z")
                }, "X", true),
                new StructProperty("Plane", new PropertyCollection
                {
                    new FloatProperty(m.M24, "W"),
                    new FloatProperty(m.M21, "X"),
                    new FloatProperty(m.M22, "Y"),
                    new FloatProperty(m.M23, "Z")
                }, "Y", true),
                new StructProperty("Plane", new PropertyCollection
                {
                    new FloatProperty(m.M34, "W"),
                    new FloatProperty(m.M31, "X"),
                    new FloatProperty(m.M32, "Y"),
                    new FloatProperty(m.M33, "Z")
                }, "Z", true),
                new StructProperty("Plane", new PropertyCollection
                {
                    new FloatProperty(m.M44, "W"),
                    new FloatProperty(m.M41, "X"),
                    new FloatProperty(m.M42, "Y"),
                    new FloatProperty(m.M43, "Z")
                }, "W", true)
            }, name, true);
        }
    }
}
