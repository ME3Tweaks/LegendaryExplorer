using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class ObjectRedirector : ObjectBinary
    {
        public UIndex DestinationObject;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref DestinationObject);
        }

        public static ObjectRedirector Create()
        {
            return new()
            {
                DestinationObject = 0
            };
        }
        
        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            Unsafe.AsRef(in action).Invoke(ref DestinationObject, nameof(DestinationObject));
        }
    }
}
