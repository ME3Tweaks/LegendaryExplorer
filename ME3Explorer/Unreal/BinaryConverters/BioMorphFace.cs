using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class BioMorphFace : ObjectBinary
    {
        public Vector3[][] LODs;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref LODs, (SerializingContainer2 sc2, ref Vector3[] lod) =>
            {
                sc2.BulkSerialize(ref lod, SCExt.Serialize, 12);
            });
        }
    }
}
