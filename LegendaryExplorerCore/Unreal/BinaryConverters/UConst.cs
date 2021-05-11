namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UConst : UField
    {
        public string Value;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Value);
        }
    }
}
