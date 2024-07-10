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
