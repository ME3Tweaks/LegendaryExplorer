using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class ObjectRedirector : ObjectBinary
    {
        public UIndex DestinationObject;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref DestinationObject);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game) => new List<(UIndex, string)>{(DestinationObject, nameof(DestinationObject))};
    }
}
