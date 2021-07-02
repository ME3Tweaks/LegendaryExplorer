using System.Collections.Generic;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
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
