using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

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

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            Unsafe.AsRef(action).Invoke(ref SuperClass, nameof(SuperClass));
            Unsafe.AsRef(action).Invoke(ref Next, nameof(Next));
        }
    }
}
