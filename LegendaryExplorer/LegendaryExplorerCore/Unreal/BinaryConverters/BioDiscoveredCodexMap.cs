using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioDiscoveredCodexMap : ObjectBinary
    {
        public int unk;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref unk);
        }

        public static BioDiscoveredCodexMap Create() => new();
    }
}
