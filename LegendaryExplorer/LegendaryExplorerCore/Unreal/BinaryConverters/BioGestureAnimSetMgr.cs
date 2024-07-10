using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioGestureAnimSetMgr : ObjectBinary
    {
        public int unk; //ME3/LE3, probably a count, but its never > 0, so who knows for what

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game.IsGame3())
            {
                sc.Serialize(ref unk);
            }
        }

        public static BioGestureAnimSetMgr Create() => new();
    }
}
