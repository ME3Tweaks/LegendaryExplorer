using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SpeedTreeComponent : ObjectBinary
    {
        public int unk1;
        public int unk2;
        public int unk3;
        public int unk4;
        public int unk5; //not ME1 or ME2
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref unk1);
            sc.Serialize(ref unk2);
            sc.Serialize(ref unk3);
            sc.Serialize(ref unk4);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref unk5);
            }
        }

        public static SpeedTreeComponent Create() => new SpeedTreeComponent();
    }
}
