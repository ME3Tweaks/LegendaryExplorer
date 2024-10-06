using LegendaryExplorerCore.Packages;
using System.Drawing;
using LegendaryExplorerCore.Unreal.Collections;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SoundCue : ObjectBinary
    {
        public UMultiMap<UIndex, Point> EditorData; //Worthless info, but it didn't get cooked out //TODO: Replace with UMap

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref EditorData, sc.Serialize, sc.Serialize);
        }

        public static SoundCue Create()
        {
            return new()
            {
                EditorData = []
            };
        }
        
        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ForEachUIndexKeyInMultiMap(action, EditorData, nameof(EditorData));
        }
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref Point p)
        {
            if (IsLoading)
            {
                p = new Point(ms.ReadInt32(), ms.ReadInt32());
            }
            else
            {
                ms.Writer.WriteInt32(p.X);
                ms.Writer.WriteInt32(p.Y);
            }
        }
    }
}