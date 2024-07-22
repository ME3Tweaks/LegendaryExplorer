using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class UField : ObjectBinary
    {
        public UIndex SuperClass; //actually a member of UStruct in UDK
        public UIndex Next;
        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game is not MEGame.UDK)
            {
                sc.Serialize(ref SuperClass);
            }
            sc.Serialize(ref Next);
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            if (game is not MEGame.UDK)
            {
                Unsafe.AsRef(in action).Invoke(ref SuperClass, nameof(SuperClass));
            }
            Unsafe.AsRef(in action).Invoke(ref Next, nameof(Next));
        }
    }
}
