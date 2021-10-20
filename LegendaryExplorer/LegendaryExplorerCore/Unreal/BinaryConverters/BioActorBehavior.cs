using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioActorBehavior : ObjectBinary
    {
        public int unk;
        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game.IsGame1())
            {
                sc.Serialize(ref unk);
            }
        }

        public static BioActorBehavior Create() => new BioActorBehavior();
    }
}
