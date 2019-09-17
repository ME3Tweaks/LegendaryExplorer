using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class FluidSurfaceComponent : ObjectBinary
    {
        public LightMap LightMap;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref LightMap);
            }
        }
        public override List<(UIndex, string)> GetUIndexes(MEGame game) => game >= MEGame.ME3 ? LightMap.GetUIndexes(game) : new List<(UIndex, string)>();
    }
}
