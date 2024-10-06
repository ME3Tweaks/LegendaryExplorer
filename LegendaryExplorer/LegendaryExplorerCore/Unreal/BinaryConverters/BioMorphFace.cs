using System;
using System.Numerics;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioMorphFace : ObjectBinary
    {
        public Vector3[][] LODs;
        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref LODs, (ref Vector3[] lod) =>
            {
                sc.BulkSerialize(ref lod, sc.Serialize, 12);
            });
        }

        public static BioMorphFace Create()
        {
            return new()
            {
                LODs = []
            };
        }
    }
}
