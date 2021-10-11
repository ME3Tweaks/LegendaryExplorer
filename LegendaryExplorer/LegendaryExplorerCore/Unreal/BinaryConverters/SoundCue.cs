using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using System.Drawing;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SoundCue : ObjectBinary
    {
        public OrderedMultiValueDictionary<UIndex, Point> EditorData; //Worthless info, but it didn't get cooked out

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref EditorData, SCExt.Serialize, SCExt.Serialize);
        }

        public static SoundCue Create()
        {
            return new()
            {
                EditorData = new OrderedMultiValueDictionary<UIndex, Point>()
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game) => EditorData.Keys().Select((u, i) => (u, $"EditorData[{i}].SoundNode")).ToList();
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