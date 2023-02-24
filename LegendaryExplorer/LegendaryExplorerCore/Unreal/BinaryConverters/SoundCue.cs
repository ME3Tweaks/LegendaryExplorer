using LegendaryExplorerCore.Packages;
using System.Drawing;
using LegendaryExplorerCore.Unreal.Collections;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SoundCue : ObjectBinary
    {
        public UMultiMap<UIndex, Point> EditorData; //Worthless info, but it didn't get cooked out //TODO: Replace with UMap

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref EditorData, SCExt.Serialize, SCExt.Serialize);
        }

        public static SoundCue Create()
        {
            return new()
            {
                EditorData = new()
            };
        }
        
        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ForEachUIndexKeyInMultiMap(action, EditorData, nameof(EditorData));
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref Point p)
        {
            if (sc.IsLoading)
            {
                p = new Point(sc.ms.ReadInt32(), sc.ms.ReadInt32());
            }
            else
            {
                sc.ms.Writer.WriteInt32(p.X);
                sc.ms.Writer.WriteInt32(p.Y);
            }
        }
    }
}