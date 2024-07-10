using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioGamePropertyEventDispatcher : ObjectBinary
    {
        public int unk1;
        public int unk2;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game.IsGame1() || sc.Game.IsGame2())
            {
                sc.Serialize(ref unk1);
                sc.Serialize(ref unk2);
            }
        }

        public static BioGamePropertyEventDispatcher Create() => new BioGamePropertyEventDispatcher();
    }
}
