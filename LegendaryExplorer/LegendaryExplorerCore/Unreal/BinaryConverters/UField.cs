using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class UField : ObjectBinary
    {
        public UIndex SuperClass;
        public UIndex Next;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SuperClass);
            sc.Serialize(ref Next);
        }
        public override List<(UIndex, string)> GetUIndexes(MEGame game) =>
            new()
            {
                (SuperClass, "SuperClass"),
                (Next, "NextItemInCompilingChain"),
            };
    }
}
