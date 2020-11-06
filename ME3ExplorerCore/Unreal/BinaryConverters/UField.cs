using System.Collections.Generic;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
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
            new List<(UIndex, string)>
            {
                (SuperClass, "SuperClass"),
                (Next, "NextItemInCompilingChain"),
            };
    }
}
