using System.Numerics;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Unreal;

// Taken entirely from ME2R
// I don't like structs, makes code very ugly.
// And where these are used is not performance critical.
// Mgamerz
namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    /// <summary>
    /// Class version of Vector4. Easier to manipulate than a struct.
    /// </summary>
    class CVector4
    {
        public float W { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public static CVector4 FromVector4(Vector4 vector)
        {
            return new CVector4()
            {
                W = vector.W,
                X = vector.X,
                Y = vector.Y,
                Z = vector.Z
            };
        }

        public static CVector4 FromStructProperty(StructProperty sp, string wKey, string xKey, string yKey, string zKey)
        {
            return new CVector4()
            {
                W = sp.GetProp<FloatProperty>(wKey),
                X = sp.GetProp<FloatProperty>(xKey),
                Y = sp.GetProp<FloatProperty>(yKey),
                Z = sp.GetProp<FloatProperty>(zKey)
            };
        }

        public Vector4 ToVector4()
        {
            return new Vector4()
            {
                W = W,
                X = X,
                Y = Y,
                Z = Z
            };
        }
    }

    /// <summary>
    /// Class version of Integer Vector3. Easier to manipulate than a struct.
    /// </summary>
    public class CIVector3
    {
        public CIVector3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public CIVector3() { }

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }

        public static CIVector3 FromRotator(StructProperty sp)
        {
            return FromStructProperty(sp, "Pitch", "Yaw", "Roll");
        }
        public static CIVector3 FromStructProperty(StructProperty sp, string xKey, string yKey, string zKey)
        {
            return new CIVector3()
            {
                X = sp.GetProp<IntProperty>(xKey),
                Y = sp.GetProp<IntProperty>(yKey),
                Z = sp.GetProp<IntProperty>(zKey)
            };
        }
        //public static CIVector3 FromVector3(Vector3 vector)
        //{
        //    return new CIVector3()
        //    {
        //        X = vector.X,
        //        Y = vector.Y,
        //        Z = vector.Z
        //    };
        //}
        //public Vector3 ToVector3()
        //{
        //    return new Vector3()
        //    {
        //        X = X,
        //        Y = Y,
        //        Z = Z
        //    };
        //}

        internal StructProperty ToRotatorStructProperty(string propName = null)
        {
            return ToStructProperty("Pitch", "Yaw", "Roll", propName, true);
        }

        internal StructProperty ToStructProperty(string xName, string yName, string zName, string propName = null, bool isImmutable = true)
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new IntProperty(X, xName));
            props.Add(new IntProperty(Y, yName));
            props.Add(new IntProperty(Z, zName));

            return new StructProperty("Rotator", props, propName, isImmutable);
        }
    }

    /// <summary>
    /// Class version of a Float Vector4. Easier to manipulate than a struct.
    /// </summary>
    public class CFVector4 : NotifyPropertyChangedBase
    {
        private float _w;
        private float _x;
        private float _y;
        private float _z;
        public float W { get => _w; set => SetProperty(ref _w, value); }
        public float X { get => _x; set => SetProperty(ref _x, value); }
        public float Y { get => _y; set => SetProperty(ref _y, value); }
        public float Z { get => _z; set => SetProperty(ref _z, value); }

        public static CFVector4 FromStructProperty(StructProperty sp, string wKey, string xKey, string yKey, string zKey)
        {
            return new CFVector4()
            {
                W = sp.GetProp<FloatProperty>(wKey),
                X = sp.GetProp<FloatProperty>(xKey),
                Y = sp.GetProp<FloatProperty>(yKey),
                Z = sp.GetProp<FloatProperty>(zKey)
            };
        }
        public static CFVector4 FromVector4(Vector4 vector)
        {
            return new CFVector4()
            {
                W = vector.W,
                X = vector.X,
                Y = vector.Y,
                Z = vector.Z
            };
        }
        public Vector4 ToVector4()
        {
            return new Vector4()
            {
                W = W,
                X = X,
                Y = Y,
                Z = Z
            };
        }

        internal StructProperty ToStructProperty(string wName, string xName, string yName, string zName, string propName = null, bool isImmutable = true)
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new FloatProperty(W, wName));
            props.Add(new FloatProperty(X, xName));
            props.Add(new FloatProperty(Y, yName));
            props.Add(new FloatProperty(Z, zName));

            return new StructProperty("LinearColor", props, propName, isImmutable);
        }

        public StructProperty ToLinearColorStructProperty(string propName = null)
        {
            return ToStructProperty("R", "G", "B", "A", propName, true);
        }
    }


    /// <summary>
    /// Class version of a Float Vector3. Easier to manipulate than a struct.
    /// </summary>
    public class CFVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public static CFVector3 FromStructProperty(StructProperty sp, string xKey, string yKey, string zKey)
        {
            return new CFVector3()
            {
                X = sp.GetProp<FloatProperty>(xKey),
                Y = sp.GetProp<FloatProperty>(yKey),
                Z = sp.GetProp<FloatProperty>(zKey)
            };
        }
        public static CFVector3 FromVector3(Vector3 vector)
        {
            return new CFVector3()
            {
                X = vector.X,
                Y = vector.Y,
                Z = vector.Z
            };
        }
        public Vector3 ToVector3()
        {
            return new Vector3()
            {
                X = X,
                Y = Y,
                Z = Z
            };
        }

        internal StructProperty ToStructProperty(string xName, string yName, string zName, string propName = null, bool isImmutable = true)
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new FloatProperty(X, xName));
            props.Add(new FloatProperty(Y, yName));
            props.Add(new FloatProperty(Z, zName));

            return new StructProperty("Vector", props, propName, isImmutable);
        }

        public StructProperty ToLocationStructProperty(string propName = null)
        {
            return ToStructProperty("X", "Y", "Z", propName, true);
        }
    }
}
