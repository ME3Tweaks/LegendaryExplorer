namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UConst : UField
    {
        public string Value;
        protected override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Value);
        }

        public static UConst Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Value = ""
            };
        }
    }
}
