namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioQuestProgressionMap : ObjectBinary
    {
        public int unk;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref unk);
        }

        public static BioQuestProgressionMap Create() => new();
    }
}
