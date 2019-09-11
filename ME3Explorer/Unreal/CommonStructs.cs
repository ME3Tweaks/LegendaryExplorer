using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;

namespace ME3Explorer.Unreal
{
    public static class CommonStructs
    {
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
    }
}
