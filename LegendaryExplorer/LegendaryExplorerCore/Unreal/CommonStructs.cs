using System;
using System.Numerics;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal
{
    //Methods for working with common StructProperty types
    public static class CommonStructs
    {
        public static StructProperty Vector3Prop(Vector3 vec, NameReference? name = null) => Vector3Prop(vec.X, vec.Y, vec.Z, name);
        public static StructProperty Vector3Prop(float x, float y, float z, NameReference? name = null)
        {
            return new StructProperty("Vector", new PropertyCollection
            {
                new FloatProperty(x, "X"),
                new FloatProperty(y, "Y"),
                new FloatProperty(z, "Z")
            }, name, true);
        }
        public static Vector3 GetVector3(StructProperty vecProp) =>
            new Vector3(vecProp.GetProp<FloatProperty>("X"), vecProp.GetProp<FloatProperty>("Y"), vecProp.GetProp<FloatProperty>("Z"));

        public static Vector3 GetVector3(ExportEntry exp, string propName, Vector3 defaultValue)
        {
            var vecProp = exp.GetProperty<StructProperty>(propName);
            return vecProp != null ? GetVector3(vecProp) : defaultValue;
        }

        public static StructProperty Vector2Prop(Vector2 vec, NameReference? name = null) => Vector2Prop(vec.X, vec.Y, name);
        public static StructProperty Vector2Prop(float x, float y, NameReference? name = null)
        {
            return new StructProperty("Vector2D", new PropertyCollection
            {
                new FloatProperty(x, "X"),
                new FloatProperty(y, "Y")
            }, name, true);
        }
        public static Vector2 GetVector2(StructProperty vecProp) =>
            new Vector2(vecProp.GetProp<FloatProperty>("X"), vecProp.GetProp<FloatProperty>("Y"));

        public static StructProperty RotatorProp(Rotator rot, NameReference? name = null) => RotatorProp(rot.Pitch,rot.Yaw, rot.Roll, name);
        public static StructProperty RotatorProp(int pitch, int yaw, int roll, NameReference? name = null)
        {
            return new StructProperty("Rotator", new PropertyCollection
            {
                new IntProperty(pitch, "Pitch"),
                new IntProperty(yaw, "Yaw"),
                new IntProperty(roll, "Roll")
            }, name, true);
        }
        public static Rotator GetRotator(StructProperty rotProp) =>
            new Rotator(rotProp.GetProp<IntProperty>("Pitch"), rotProp.GetProp<IntProperty>("Yaw"), rotProp.GetProp<IntProperty>("Roll"));

        public static StructProperty MatrixProp(Matrix4x4 m, NameReference? name = null)
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

        public static StructProperty GuidProp(Guid guid, NameReference? name = null)
        {
            byte[] guidBytes = guid.ToByteArray();
            return new StructProperty("Guid", new PropertyCollection
            {
                new IntProperty(BitConverter.ToInt32(guidBytes, 0), "A"),
                new IntProperty(BitConverter.ToInt32(guidBytes, 4), "B"),
                new IntProperty(BitConverter.ToInt32(guidBytes, 8), "C"),
                new IntProperty(BitConverter.ToInt32(guidBytes, 12), "D")
            }, name, true);
        }

        public static Guid GetGuid(StructProperty guidProp)
        {
            int a = guidProp.GetProp<IntProperty>("A");
            int b = guidProp.GetProp<IntProperty>("B");
            int c = guidProp.GetProp<IntProperty>("C");
            int d = guidProp.GetProp<IntProperty>("D");
            var ms = MemoryManager.GetMemoryStream(16);
            ms.WriteInt32(a);
            ms.WriteInt32(b);
            ms.WriteInt32(c);
            ms.WriteInt32(d);
            return new Guid(ms.ToArray());
        }

        public static StructProperty ColorProp(System.Drawing.Color color, NameReference? name = null)
        {
            return new StructProperty("Color", new PropertyCollection
            {
                new ByteProperty(color.B, "B"),
                new ByteProperty(color.G, "G"),
                new ByteProperty(color.R, "R"),
                new ByteProperty(color.A, "A"),
            }, name, true);
        }

        public static System.Drawing.Color GetColor(StructProperty prop) => System.Drawing.Color.FromArgb(prop.GetProp<ByteProperty>("A").Value, prop.GetProp<ByteProperty>("R").Value,
                                                                            prop.GetProp<ByteProperty>("G").Value, prop.GetProp<ByteProperty>("B").Value);
    }
}
