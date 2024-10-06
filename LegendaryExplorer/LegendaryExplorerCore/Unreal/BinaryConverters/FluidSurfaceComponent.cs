using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class FluidSurfaceComponent : ObjectBinary
    {
        public LightMap LightMap;

        protected override void Serialize(SerializingContainer sc)
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

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            if (game >= MEGame.ME3)
            {
                LightMap.ForEachUIndex(game, action);
            }
        }
    }
}
