using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
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

        public static FluidSurfaceComponent Create()
        {
            return new()
            {
                LightMap = new LightMap()
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game) => game >= MEGame.ME3 ? LightMap.GetUIndexes(game) : new List<(UIndex, string)>();
    }
}
